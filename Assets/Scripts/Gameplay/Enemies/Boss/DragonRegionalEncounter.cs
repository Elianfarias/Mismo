using System.Collections;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Enemies
{
    public sealed class DragonRegionalEncounter : MonoBehaviour, IDragonEncounter
    {
        public SoulEaterPhaseOneController bossPrefab;
        public GameObject arrivalVisual;
        public AnimationClip flight;
        public AudioClip landingSound, roarSound;
        public float PresentationTime {get;private set;}
        public SoulEaterPhaseOneController Boss {get;private set;}
        public bool Ready=>bossPrefab!=null && bossPrefab.Settings!=null && bossPrefab.Settings.land!=null && bossPrefab.Settings.roar!=null && arrivalVisual!=null && flight!=null && bossPrefab.Settings.telegraphPrefab!=null && bossPrefab.Settings.telegraphPrefab.sharedMaterial!=null && bossPrefab.Settings.breathMaterial!=null;
        public bool IsActive=>pending || Boss!=null;
        public bool AtPhaseBoundary=>Boss!=null && Boss.PhaseOneComplete;
        Transform target;
        Health targetHealth;
        Vector3 arena;
        float radius;
        bool pending,arrivalImpactApplied,skipRequested;
        GameObject visual;
        SoulEaterAnimation animation;
        UnityEngine.Camera view;
        Vector3 savedCameraPosition;
        Quaternion savedCameraRotation;
        float savedFov;
        bool ownsInput;
        AudioSource voice;
        public void Initialize(Transform player,Vector3 center,float arenaRadius)
        {target=player;targetHealth=player.GetComponent<Health>();arena=center;radius=arenaRadius;pending=true;skipRequested=false;}
        public void Skip()=>skipRequested=true;
        IEnumerator Start()
        {
            if(!Ready || target==null){Abort();yield break;}
            var world=FindFirstObjectByType<ExplorationChunks>();if(world!=null)arena.y=world.SurfaceHeight(arena);
            // The confirmation closes its dialogue before this sequence acquires input.
            yield return null;
            ownsInput=GameplayPause.TryBlockInput(this);
            if(!ownsInput){Abort();yield break;}
            view=UnityEngine.Camera.main;
            if(view!=null){savedCameraPosition=view.transform.position;savedCameraRotation=view.transform.rotation;savedFov=view.fieldOfView;}
            var direction=Vector3.ProjectOnPlane(target.position-arena,Vector3.up).normalized;
            if(direction.sqrMagnitude<.1f)direction=Vector3.left;
            var rotation=Quaternion.LookRotation(direction);
            var reference=bossPrefab.GetComponentInChildren<Mismo.Gameplay.Player.Voxels.VoxelRigInstance>();
            Vector3 offset=rotation*reference.transform.localPosition;
            visual=Instantiate(arrivalVisual,arena-direction*35+Vector3.up*22+offset,rotation,transform);visual.transform.localScale=reference.transform.localScale;
            voice=visual.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.minDistance=15;voice.maxDistance=90;voice.volume=.7f;
            foreach(var collider in visual.GetComponentsInChildren<Collider>())collider.enabled=false;
            animation=new SoulEaterAnimation(visual.GetComponentInChildren<Animator>());
            var settings=bossPrefab.Settings;
            float landTime=Mathf.Clamp(settings.land.length,.8f,2),roarTime=Mathf.Clamp(settings.roar.length,1.4f,2.5f);
            float duration=3+landTime+roarTime;
            bool landed=false,roared=false;
            var arrivalFeet=world!=null?new SoulEaterFootSupport(visual.GetComponentInChildren<Mismo.Gameplay.Player.Voxels.VoxelRigInstance>(),world.SurfaceHeight):null;
            for(float t=0;t<duration && pending;t+=Time.deltaTime)
            {
                PresentationTime=t;
                if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Skip();
                if(skipRequested)break;
                if(t<3)
                {
                    visual.transform.position=Vector3.Lerp(arena-direction*35+Vector3.up*22,arena+Vector3.up*7,Mathf.SmoothStep(0,1,t/3))+offset;
                    animation.Sample(flight,Mathf.Repeat(t/flight.length,1),Time.deltaTime);
                }
                else if(t<3+landTime)
                {
                    float p=(t-3)/landTime;visual.transform.position=arena+offset+Vector3.up*Mathf.Lerp(7,0,Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.65f)));
                    animation.Sample(settings.land,p,Time.deltaTime);
                    if(p>=.65f && !landed)
                    {
                        if(!ApplyArrivalImpact(world,settings)){Abort();yield break;}
                        landed=true;visual.transform.position=arena+offset;
                        if(landingSound!=null)voice.PlayOneShot(landingSound);
                    }
                }
                else
                {
                    visual.transform.position=arena+offset;animation.Sample(settings.roar,(t-3-landTime)/roarTime,Time.deltaTime);
                    if(!roared){roared=true;if(roarSound!=null)voice.PlayOneShot(roarSound);}
                }
                if(landed)arrivalFeet?.Apply(settings.soleClearance);
                if(view!=null)
                {
                    var look=Quaternion.LookRotation(visual.transform.position+Vector3.up*2-savedCameraPosition);
                    float back=Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-.7f,duration,t));
                    view.fieldOfView=Mathf.Lerp(Mathf.Lerp(savedFov,Mathf.Min(savedFov,30),Mathf.SmoothStep(0,1,t)),savedFov,back);
                    view.transform.rotation=Quaternion.Slerp(Quaternion.Slerp(savedCameraRotation,look,Mathf.Clamp01(t)),savedCameraRotation,back);
                }
                yield return null;
            }
            // Skipping the camera sequence must still clear the landing footprint before spawning.
            if(pending && targetHealth!=null && !targetHealth.IsDead && !ApplyArrivalImpact(world,settings)){Abort();yield break;}
            ClearPresentation();
            if(!pending || targetHealth==null || targetHealth.IsDead)yield break;
            Boss=Instantiate(bossPrefab,arena,Quaternion.LookRotation(Vector3.ProjectOnPlane(target.position-arena,Vector3.up)),transform);
            Boss.SetAutomaticEngagement(false);
            Boss.SetArenaRadius(radius);

            yield return null; // SoulEater.Start initializes health, home and the animation graph.
            if(Boss==null || target==null){Abort();yield break;}
            pending=false;Boss.BeginEncounter(target);
        }
        bool ApplyArrivalImpact(ExplorationChunks world,SoulEaterPhaseOneSettings settings)
        {
            if(arrivalImpactApplied || world==null)return true;
            if(!world.TryDragonImpact(arena,settings.arrivalImpactRadius,settings.arrivalCraterDepth))
            {Debug.LogWarning("No se pudo guardar o despejar el aterrizaje de Soul Eater. El ritual se puede reintentar.");return false;}
            arrivalImpactApplied=true;arena.y=world.SurfaceHeight(arena);
            WorldImpactDebris.Emit(arena+Vector3.up*.3f,new Color(.3f,.24f,.16f),80);
            return true;
        }
        float deadTime;
        void Update()
        {
            if(target==null || targetHealth==null || targetHealth.IsDead)Abort();
        }
        void LateUpdate()
        {
            if(Boss!=null && Boss.Health.IsDead)
            {deadTime+=Time.deltaTime;if(deadTime>Mathf.Max(3,Boss.Settings.die!=null?Boss.Settings.die.length:3))Abort();}
            else deadTime=0;
        }
        public void Abort()
        {
            pending=false;StopAllCoroutines();ClearPresentation();
            if(Boss!=null){Boss.gameObject.SetActive(false);Destroy(Boss.gameObject);Boss=null;}
        }
        void ClearPresentation()
        {
            animation?.Dispose();animation=null;if(visual!=null){visual.SetActive(false);Destroy(visual);visual=null;}
            if(ownsInput)
            {
                if(view!=null){view.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);view.fieldOfView=savedFov;}
                GameplayPause.ReleaseInput(this);ownsInput=false;
            }
        }
        void OnGUI()
        {
            if(!ownsInput)return;
            GUI.depth=-440;
            PlayerHUD.Fill(new Rect(0,Screen.height-70,Screen.width,70),new Color(0,0,0,.85f));
            QuietFantasyUI.Text(new Rect(30,Screen.height-55,Screen.width-60,40),"SOUL EATER · La llamada fue escuchada                                      Esc · Omitir",21,PlayerHUD.Gold);
        }
        void OnDisable()=>Abort();
    }
}
