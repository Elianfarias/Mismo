using System.Collections;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Presentation
{
    [DefaultExecutionOrder(-120)]
    public sealed class WorldMapPanel : MonoBehaviour
    {
        static WorldMapPanel active;
        public static bool AnyOpen=>active!=null&&active.IsOpen;
        public static bool BlocksGameplay=>active!=null&&(active.IsOpen||active.closedFrame==Time.frameCount);
        public bool IsOpen{get;private set;}
        public bool Ready=>mesh!=null&&builder==null;
        public RenderTexture Preview=>texture;
        public float Zoom{get;private set;}=180;
        public Vector2 Center{get;private set;}
        public float Yaw{get;private set;}=35;
        public float Pitch{get;private set;}=60;
        const float MapDepth=-12000;
        ExplorationWorldSettings settings;
        ExplorationTerrain terrain;
        UnityEngine.Camera mapCamera;
        GameObject surface;
        Mesh mesh;
        Material material;
        RenderTexture texture;
        Coroutine builder;
        readonly List<WorldSite> sites=new List<WorldSite>();
        bool dirty=true;
        float nextBuild;
        int closedFrame=-1;
        CursorLockMode previousLock;
        bool previousVisible;
        Rect View=>new Rect(24,88,Mathf.Max(100,Screen.width-48),Mathf.Max(100,Screen.height-148));
        public void Initialize(ExplorationWorldSettings value){settings=value;terrain=new ExplorationTerrain(value);dirty=true;}
        public bool Open()
        {
            if(GetComponent<World.GatheringPlayer>()?.Busy==true)return false;
            if(IsOpen)return true;
            if(terrain==null||InventoryPanel.AnyOpen)return false;
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead)return false;
            var equipment=GetComponent<Equipment.EquipmentLoadout>();equipment?.Runner.Cancel();equipment?.Belt?.Cancel();
            previousLock=Cursor.lockState;previousVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            active=this;IsOpen=true;GameAudio.Play(GameSound.MenuOpen);Recenter();EnsureView();return true;
        }
        public void Close(){if(!IsOpen)return;GameAudio.Play(GameSound.MenuClose);IsOpen=false;closedFrame=Time.frameCount;Cursor.lockState=previousLock;Cursor.visible=previousVisible;}
        public void Recenter(){Center=new Vector2(transform.position.x,transform.position.z);dirty=true;}
        public void SetZoom(float value){Zoom=Mathf.Clamp(value,40,2400);dirty=true;}
        public void Pan(Vector2 delta){Center+=delta;dirty=true;}
        public void Orbit(Vector2 delta){Yaw=Mathf.Repeat(Yaw+delta.x,360);Pitch=Mathf.Clamp(Pitch+delta.y,35,89);dirty=true;}
        void EnsureView()
        {
            if(mapCamera!=null)return;
            var cameraObject=new GameObject("World map camera");mapCamera=cameraObject.AddComponent<UnityEngine.Camera>();mapCamera.enabled=false;
            mapCamera.orthographic=true;mapCamera.nearClipPlane=.1f;mapCamera.farClipPlane=16000;mapCamera.cullingMask=1<<31;mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=new Color(.055f,.09f,.12f);mapCamera.allowHDR=false;
            texture=new RenderTexture(1280,960,16){name="Interactive world map"};texture.Create();mapCamera.targetTexture=texture;
            surface=new GameObject("World map relief");surface.layer=31;surface.transform.position=Vector3.up*MapDepth;surface.AddComponent<MeshFilter>();
            material=new Material(Shader.Find("Mismo/World Map"));surface.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.mKey.wasPressedThisFrame){if(IsOpen)Close();else Open();}
            else if(IsOpen&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)Close();
            if(!IsOpen)return;
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null&&health.IsDead){Close();return;}
            var mouse=Mouse.current;
            if(mouse!=null)
            {
                var p=mouse.position.ReadValue();p.y=Screen.height-p.y;
                if(View.Contains(p))
                {
                    float scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)SetZoom(Zoom*Mathf.Exp(-scroll*.0015f));
                    Vector2 delta=mouse.delta.ReadValue();
                    if(mouse.rightButton.isPressed)Orbit(new Vector2(delta.x*.25f,-delta.y*.2f));
                    else if(mouse.leftButton.isPressed||mouse.middleButton.isPressed)
                    {
                        var right=Quaternion.Euler(0,Yaw,0)*Vector3.right;var forward=Quaternion.Euler(0,Yaw,0)*Vector3.forward;
                        var move=(-right*delta.x-forward*delta.y/Mathf.Sin(Pitch*Mathf.Deg2Rad))*(Zoom*2/View.height);
                        Pan(new Vector2(move.x,move.z));
                    }
                }
            }
            if(keyboard!=null)
            {
                if(keyboard.homeKey.wasPressedThisFrame)Recenter();
                var direction=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                if(direction.sqrMagnitude>0){var move=Quaternion.Euler(0,Yaw,0)*new Vector3(direction.x,0,direction.y)*Zoom*Time.unscaledDeltaTime;Pan(new Vector2(move.x,move.z));}
            }
            if(dirty&&builder==null&&Time.unscaledTime>=nextBuild){dirty=false;builder=StartCoroutine(Build());nextBuild=Time.unscaledTime+.15f;}
        }
        IEnumerator Build()
        {
            const int resolution=64;
            var center=Center;float extent=Zoom*Mathf.Max(1,View.width/View.height)*1.7f;float step=extent*2/resolution;
            var heights=new float[resolution+1,resolution+1];
            for(int z=0;z<=resolution;z++)
            {for(int x=0;x<=resolution;x++)heights[x,z]=terrain.Height(center.x-extent+x*step,center.y-extent+z*step);if(z%8==0)yield return null;}
            var geometry=new VoxelRegionGeometry();
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {
                float px=center.x-extent+x*step,pz=center.y-extent+z*step,y=heights[x,z];
                Color color=terrain.Top(px,pz,y);
                geometry.Quad(new Vector3(px,y,pz),new Vector3(px,y,pz+step),new Vector3(px+step,y,pz+step),new Vector3(px+step,y,pz),color);
                float other=heights[x+1,z];geometry.Quad(new Vector3(px+step,other,pz),new Vector3(px+step,y,pz),new Vector3(px+step,y,pz+step),new Vector3(px+step,other,pz+step),color*.72f);
                other=heights[x,z+1];geometry.Quad(new Vector3(px,other,pz+step),new Vector3(px,y,pz+step),new Vector3(px+step,y,pz+step),new Vector3(px+step,other,pz+step),color*.8f);
            }
            var previous=mesh;mesh=geometry.Mesh("Map relief");surface.GetComponent<MeshFilter>().sharedMesh=mesh;if(previous!=null)Destroy(previous);
            sites.Clear();int spacing=Mathf.Max(64,settings.siteSpacing);int radius=Mathf.Min(40,Mathf.CeilToInt(extent/spacing)+1);
            var cell=new Vector2Int(Mathf.FloorToInt(center.x/spacing),Mathf.FloorToInt(center.y/spacing));
            for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++){var site=terrain.Site(cell+new Vector2Int(x,z));if(site.kind!=WorldSiteKind.Clearing&&terrain.IsExterior(site.position.x,site.position.z))sites.Add(site);}
            if(settings.preserveAuthoredCenter)sites.Add(new WorldSite{kind=WorldSiteKind.Village,position=VoxelRegionHeightfield.Sites[0]});
            builder=null;
        }
        void LateUpdate()
        {
            if(!IsOpen||mapCamera==null||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var rotation=Quaternion.Euler(Pitch,Yaw,0);var target=new Vector3(Center.x,MapDepth,Center.y);
            mapCamera.transform.SetPositionAndRotation(target-rotation*Vector3.forward*6500,rotation);mapCamera.orthographicSize=Zoom;mapCamera.aspect=View.width/View.height;mapCamera.Render();
        }
        void OnGUI()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(!IsOpen)return;
            int depth=GUI.depth;GUI.depth=-100;var old=GUI.color;GUI.color=new Color(.035f,.055f,.08f,.99f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold};GUI.Label(new Rect(26,18,350,42),"MAPA DEL MUNDO",title);
            if(GUI.Button(new Rect(Screen.width-132,20,108,38),"Cerrar [M]"))Close();
            if(GUI.Button(new Rect(Screen.width-264,20,122,38),"Mi posición"))Recenter();
            if(GUI.Button(new Rect(Screen.width-365,20,90,38),"Norte ↑")){Yaw=0;Pitch=75;dirty=true;}
            if(GUI.Button(new Rect(Screen.width-453,20,34,38),"−"))SetZoom(Zoom*1.3f);
            if(GUI.Button(new Rect(Screen.width-411,20,34,38),"+"))SetZoom(Zoom/1.3f);
            if(texture!=null)GUI.DrawTexture(View,texture,ScaleMode.StretchToFill);
            if(mesh==null)GUI.Label(new Rect(View.x+20,View.y+20,280,30),"Dibujando el relieve…");
            foreach(var site in sites)
            {
                if(Zoom>700&&site.kind!=WorldSiteKind.Village)continue;
                string label=site.kind==WorldSiteKind.Village?"◆ Pueblo":site.kind==WorldSiteKind.BossArena?"▲ Arena":site.kind==WorldSiteKind.Ruin?"▪ Ruinas":"✦ Santuario";
                Marker(site.position,label,site.kind==WorldSiteKind.Village?new Color(1,.82f,.35f):new Color(.85f,.85f,.9f));
            }
            Marker(transform.position,"▲ Vos",Color.white);
            GUI.Label(new Rect(26,Screen.height-48,Screen.width-52,42),"Rueda: zoom  ·  Arrastrar: mover  ·  Botón derecho: girar/inclinar  ·  WASD: desplazar  ·  Inicio: centrar\n"+Mathf.RoundToInt(Zoom*2)+" m de alto  |  Centro: "+Mathf.RoundToInt(Center.x)+", "+Mathf.RoundToInt(Center.y));
            GUI.color=old;GUI.depth=depth;
        }
        void Marker(Vector3 position,string text,Color color)
        {
            if(mapCamera==null)return;position.y=terrain.Height(position.x,position.z)+MapDepth+2;
            var uv=mapCamera.WorldToViewportPoint(position);if(uv.z<0||uv.x<0||uv.x>1||uv.y<0||uv.y>1)return;
            var rect=new Rect(View.x+uv.x*View.width-35,View.y+(1-uv.y)*View.height-12,110,25);
            var style=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold};style.normal.textColor=Color.black;GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,style);style.normal.textColor=color;GUI.Label(rect,text,style);
        }
        void OnDisable(){Close();if(builder!=null){StopCoroutine(builder);builder=null;dirty=true;}}
        void OnDestroy(){if(active==this)active=null;if(mapCamera!=null)Destroy(mapCamera.gameObject);if(surface!=null)Destroy(surface);if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);if(texture!=null){texture.Release();Destroy(texture);}}
    }
}
