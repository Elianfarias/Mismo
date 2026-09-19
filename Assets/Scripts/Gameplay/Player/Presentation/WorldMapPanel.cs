using System.Collections;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed partial class WorldMapPanel : MonoBehaviour
    {
        static WorldMapPanel active;
        public static bool AnyOpen => active != null && active.IsOpen;
        public static bool BlocksGameplay => active != null && (active.IsOpen || active.miniInteractive || active.closedFrame == Time.frameCount);
        public bool IsOpen { get; private set; }
        public bool Ready => mesh != null && builder == null;
        public RenderTexture Preview => texture;
        public float Zoom { get; private set; } = 220;
        public Vector2 Center { get; private set; }
        public float Yaw { get; private set; } = 35;
        public float Pitch { get; private set; } = 38;
        const float MapDepth = -12000;
        const float ReliefScale = 1f;
        ExplorationWorldSettings settings;
        ExplorationTerrain terrain;
        UnityEngine.Camera mapCamera;
        GameObject surface;
        Mesh mesh;
        Material material;
        RenderTexture texture;
        Coroutine builder;
        readonly List<WorldSite> sites = new List<WorldSite>();
        bool dirty = true;
        float nextBuild, miniZoom = 120, renderedYaw, renderedPitch = 38;
        float configuredMiniDistance = -1;
        Vector2 miniOrbitOffset;
        Vector2 builtCenter,miniCenter;
        float builtZoom;
        float openMapExtent=220;
        float miniMapExtent=120;
        bool builtOpen;
        int builtDiscoveryRevision = -1;
        MapExploration displayedExploration;
        Rect builtFootprint;
        bool followPlayer=true;
        MapExploration exploration;
        float nextDiscoverySave;
        float nextDiscoveryUpdate;
        string discoveryWorldId;
        Texture2D portrait;
        float nextPortrait;

        int closedFrame = -1;
        CursorLockMode previousLock;
        bool previousVisible;
        const string MiniWidthPreference="Mismo.Map.MinimapWidth";
        float miniWidth=330;
        Rect View => new Rect(0,0,Screen.width,Screen.height);
        public Rect MiniRect
        {
            get
            {
                float scale=PlayerHUD.Scale;
                float width=Mathf.Min(miniWidth*scale,Screen.width-40*scale,(Screen.height-48*scale)*330f/280f);
                return new Rect(Screen.width-20*scale-width,18*scale,width,width*280f/330f);
            }
        }
        float ViewZoom => IsOpen ? Zoom : miniZoom;
        // Zooming in moves the camera closer without cutting away the existing map sheet.
        float TerrainExtent => IsOpen ? openMapExtent : miniMapExtent;
        Vector2 ViewCenter => IsOpen ? Center : followPlayer ? new Vector2(transform.position.x,transform.position.z) : miniCenter;
        float ViewAspect => IsOpen ? (float)Screen.width/Screen.height : MiniRect.width/MiniRect.height;
        public void Initialize(ExplorationWorldSettings value)
        {
            RestartBuild();mapSamples.Clear();settings=value; terrain=new ExplorationTerrain(value); dirty=true; active=this;
            discoveryWorldId=WorldSession.Current?.id;
            exploration=new MapExploration(WorldSession.Current?.mapDiscovery);
            exploration.Reveal(transform.position,144);
            miniWidth=Mathf.Clamp(PlayerPrefs.GetFloat(MiniWidthPreference,330),220,900);
            LoadPins(); ApplyMinimapSettings(); EnsureView();
        }
        public bool Open()
        {
            if(GetComponent<GatheringPlayer>()?.Busy==true || terrain==null || InventoryPanel.AnyOpen || GameplayPause.BlocksInput) return false;
            if(IsOpen)return true;
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead)return false;
            var equipment=GetComponent<Equipment.EquipmentLoadout>();equipment?.Runner.Cancel();equipment?.Belt?.Cancel();
            if(miniInteractive)ReleaseMini(); previousLock=Cursor.lockState; previousVisible=Cursor.visible;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            active=this;IsOpen=true;Yaw=renderedYaw;Pitch=renderedPitch;Center=new Vector2(transform.position.x,transform.position.z);RestartBuild();GameAudio.Play(GameSound.MenuOpen);return true;
        }
        void RestartBuild(){if(builder!=null){StopCoroutine(builder);builder=null;}dirty=true;}
        public void Close()
        {
            if(!IsOpen)return;
            IsOpen=false;closedFrame=Time.frameCount;draft=null;selectedVillage=null;dragging=false;RestartBuild();
            Cursor.lockState=previousLock;Cursor.visible=previousVisible;GameAudio.Play(GameSound.MenuClose);
        }
        public void Recenter()
        {
            var position=new Vector2(transform.position.x,transform.position.z);
            if(IsOpen)Center=position;
            else{miniCenter=position;followPlayer=true;miniOrbitOffset=Vector2.zero;}
            dirty=true;
        }
        public void SetZoom(float value)
        {
            Zoom=Mathf.Clamp(value,40,8192);
            if(Zoom>openMapExtent){openMapExtent=Zoom;dirty=true;}
        }
        void SetMiniZoom(float value)
        {
            miniZoom=Mathf.Clamp(value,20,2048);
            if(miniZoom>miniMapExtent){miniMapExtent=miniZoom;if(!IsOpen)dirty=true;}
        }
        public void Pan(Vector2 delta){var c=ViewCenter+delta;c=new Vector2(Mathf.Clamp(c.x,-900000,900000),Mathf.Clamp(c.y,-900000,900000));if(IsOpen)Center=c;else{miniCenter=c;followPlayer=false;}}
        public void Orbit(Vector2 delta){Yaw=Mathf.Repeat(Yaw+delta.x,360);Pitch=Mathf.Clamp(Pitch+delta.y,20,89);}
        void OrbitMini(Vector2 delta)
        {
            miniOrbitOffset.x=Mathf.DeltaAngle(0,miniOrbitOffset.x+delta.x);
            var main=UnityEngine.Camera.main;
            float basePitch=main!=null?Mathf.Lerp(22,89,Mathf.InverseLerp(-10,70,Mathf.DeltaAngle(0,main.transform.eulerAngles.x))):38;
            miniOrbitOffset.y=Mathf.Clamp(basePitch+miniOrbitOffset.y+delta.y,20,89)-basePitch;
        }
        void EnsureView()
        {
            if(mapCamera!=null)return;
            mapCamera=new GameObject("Cartographic camera").AddComponent<UnityEngine.Camera>();mapCamera.enabled=false;
            mapCamera.orthographic=true;mapCamera.nearClipPlane=.1f;mapCamera.farClipPlane=16000;mapCamera.cullingMask=1<<31;
            mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=Color.clear;mapCamera.allowHDR=false;
            ResizeMapTexture();
            surface=new GameObject("Cartographic relief");surface.layer=31;surface.transform.position=Vector3.up*MapDepth;surface.transform.localScale=new Vector3(1,ReliefScale,1);surface.AddComponent<MeshFilter>();
            material=new Material(Shader.Find("Mismo/World Map"));surface.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void Update()
        {
            ApplyMinimapSettings();
            if(terrain==null)return; UpdateMiniInteraction(); if(GameplayPause.BlocksInput)return;
            if(Time.unscaledTime>=nextDiscoveryUpdate){exploration.Reveal(transform.position,144);nextDiscoveryUpdate=Time.unscaledTime+.2f;}
            if(Time.unscaledTime>=nextDiscoverySave){SaveDiscovery();nextDiscoverySave=Time.unscaledTime+5;}
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.mKey.wasPressedThisFrame&&draft==null){if(IsOpen)Close();else Open();}
            else if(IsOpen&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){if(draft!=null)draft=null;else if(selectedVillage.HasValue)selectedVillage=null;else Close();}
            if(IsOpen)
            {
                var health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead){Close();return;}
                if(keyboard!=null&&keyboard.homeKey.wasPressedThisFrame&&draft==null)Recenter();
            }
            if(builder==null&&(dirty||NeedsBuild())&&Time.unscaledTime>=nextBuild)
            {dirty=false;builder=StartCoroutine(Build());nextBuild=Time.unscaledTime+.25f;}
        }
        bool NeedsBuild()
        {
            if(mesh==null||builtOpen!=IsOpen||!Mathf.Approximately(builtZoom,TerrainExtent))return true;
            if(builtDiscoveryRevision!=exploration.Revision)
            {
                if(exploration.HasNewDiscovery(displayedExploration,builtFootprint))return true;
                builtDiscoveryRevision=exploration.Revision;
            }
            float margin=Mathf.Max(32,TerrainExtent*.5f);
            return Mathf.Max(Mathf.Abs(ViewCenter.x-builtCenter.x),Mathf.Abs(ViewCenter.y-builtCenter.y))>margin*.5f;
        }
        void ResizeMapTexture()
        {
            int width=IsOpen?1536:512,height=IsOpen?1024:512;
            if(texture!=null&&texture.width==width&&texture.height==height)return;
            mapCamera.targetTexture=null;
            if(texture!=null){texture.Release();Destroy(texture);}
            texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="World cartography",filterMode=FilterMode.Bilinear};
            texture.Create();mapCamera.targetTexture=texture;
        }
        void LateUpdate()
        {
            if(mapCamera==null||mesh==null||GameplayPause.BlocksInput||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            // Camera tracking is independent of the slower terrain generation.
            ResizeMapTexture();
            var main=UnityEngine.Camera.main;
            if(!IsOpen&&main!=null)
            {
                renderedYaw=Mathf.Repeat(main.transform.eulerAngles.y+miniOrbitOffset.x,360);
                float cameraPitch=Mathf.DeltaAngle(0,main.transform.eulerAngles.x);
                // Preserve a readable oblique view at eye level, reach overhead when looking down.
                renderedPitch=Mathf.Clamp(Mathf.Lerp(22,89,Mathf.InverseLerp(-10,70,cameraPitch))+miniOrbitOffset.y,20,89);
            }
            else if(IsOpen){renderedYaw=Yaw;renderedPitch=Pitch;}
            var rotation=Quaternion.Euler(renderedPitch,renderedYaw,0);
            var c=ViewCenter;var heightCenter=dragging&&dragButton==1?builtCenter:c;
            float height=displayedExploration.Contains(heightCenter.x,heightCenter.y)?terrain.Height(heightCenter.x,heightCenter.y):8;
            // The land has world-space edges: panning moves the sheet itself, not a fixed mask.
            material.SetVector("_MapClipRect",new Vector4(builtFootprint.xMin,builtFootprint.yMin,builtFootprint.xMax,builtFootprint.yMax));
            var target=new Vector3(c.x,MapDepth+height*ReliefScale,c.y);
            var bounds=mesh.bounds;
            float radius=bounds.extents.magnitude+Vector3.Distance(bounds.center,new Vector3(c.x,height,c.y));
            float cameraDistance=Mathf.Max(128,radius+64);
            mapCamera.farClipPlane=cameraDistance+radius+64;
            mapCamera.transform.SetPositionAndRotation(target-rotation*Vector3.forward*cameraDistance,rotation);
            // Preserve terrain pixels per metre when resizing the minimap container.
            // Expanding its rectangle adds space instead of stretching the existing sheet.
            float viewportScale=IsOpen?1f:MiniRect.height/(280f*PlayerHUD.Scale);
            mapCamera.orthographicSize=ViewZoom*1.6f*viewportScale;
            mapCamera.aspect=ViewAspect;mapCamera.Render();
            if(portrait==null&&Time.unscaledTime>=nextPortrait){nextPortrait=Time.unscaledTime+3;CapturePortrait();}
        }
        void ApplyMinimapSettings()
        {
            if(catalog==null)return;
            float distance=Mathf.Clamp(catalog.minimapDistance,20,512);
            if(Mathf.Approximately(distance,configuredMiniDistance))return;
            bool firstConfiguration=configuredMiniDistance<0;
            configuredMiniDistance=distance;
            if(firstConfiguration)miniMapExtent=distance;
            SetMiniZoom(distance);
        }
        void SaveDiscovery()
        {
            if(exploration==null||!exploration.Changed||WorldSession.Current==null||WorldSession.Current.id!=discoveryWorldId)return;
            if(WorldSession.SaveMapDiscovery(exploration.Snapshot()))exploration.MarkSaved();
            else error=WorldSession.LastError;
        }
        void OnApplicationQuit()=>SaveDiscovery();
        void OnApplicationFocus(bool focused){if(!focused){FinishMiniResize();dragging=false;}}
        void OnApplicationPause(bool paused){if(paused)SaveDiscovery();}
        bool VisibleOnMap(Vector3 p)=>displayedExploration!=null&&builtFootprint.Contains(new Vector2(p.x,p.z))&&displayedExploration.Contains(p.x,p.z);
        void CapturePortrait()
        {
            var preview=new InventoryPreview();
            try
            {
                var visual=GetComponent<PlayerMotor>()?.Visual;
                preview.Show(visual!=null?visual.gameObject:gameObject,default,true,r=>
                {
                    string name=r.name.ToLowerInvariant();
                    return name.Contains("head")||name.Contains("hood")||name.Contains("face")||name.Contains("eye")||name.Contains("hair")||name.Contains("hat")||name.Contains("scarf");
                },true);
                if(!preview.HasModel)return;
                var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=preview.Texture as RenderTexture;
                    portrait=new Texture2D(preview.Texture.width,preview.Texture.height,TextureFormat.RGBA32,false){name="Player map portrait"};
                    portrait.ReadPixels(new Rect(0,0,portrait.width,portrait.height),0,0);portrait.Apply();
                }
                finally{RenderTexture.active=previous;}
            }
            finally{preview.Dispose();}
        }
        Vector2 Project(Vector3 position,Rect rect)
        {
            position.y=terrain.Height(position.x,position.z)*ReliefScale+MapDepth+3;var uv=mapCamera.WorldToViewportPoint(position);
            return new Vector2(rect.x+uv.x*rect.width,rect.y+(1-uv.y)*rect.height);
        }
        bool Pick(Vector2 screen,Rect rect,out Vector3 position)
        {
            var ray=mapCamera.ViewportPointToRay(new Vector3((screen.x-rect.x)/rect.width,1-(screen.y-rect.y)/rect.height,0));
            // March from above the terrain and bisect the first surface crossing (works on slopes).
            float high=mesh!=null?mesh.bounds.max.y*ReliefScale+16:256;
            float low=mesh!=null?mesh.bounds.min.y*ReliefScale-16:-128;
            float start=(MapDepth+high-ray.origin.y)/ray.direction.y;
            float end=(MapDepth+low-ray.origin.y)/ray.direction.y;
            float previous=Mathf.Max(0,start);
            for(int i=1;i<=256;i++)
            {
                float t=Mathf.Lerp(Mathf.Max(0,start),end,i/256f);var p=ray.GetPoint(t);
                if(p.y<=MapDepth+terrain.Height(p.x,p.z)*ReliefScale)
                {
                    float lo=previous,hi=t;for(int j=0;j<14;j++){float mid=(lo+hi)*.5f;var q=ray.GetPoint(mid);if(q.y>MapDepth+terrain.Height(q.x,q.z)*ReliefScale)lo=mid;else hi=mid;}
                    position=ray.GetPoint((lo+hi)*.5f);position.y=terrain.Height(position.x,position.z);return VisibleOnMap(position);
                }
                previous=t;
            }
            position=default;return false;
        }
        void OnDisable(){SaveDiscovery();Close();ReleaseMini();if(builder!=null){StopCoroutine(builder);builder=null;}dirty=true;}
        void OnDestroy(){if(portrait!=null)Destroy(portrait);if(active==this)active=null;if(ownedCatalog&&catalog!=null)Destroy(catalog);if(mapCamera!=null)Destroy(mapCamera.gameObject);if(surface!=null)Destroy(surface);if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);if(texture!=null){texture.Release();Destroy(texture);}}
    }
}










