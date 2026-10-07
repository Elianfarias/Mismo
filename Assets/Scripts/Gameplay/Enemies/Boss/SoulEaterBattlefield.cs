using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    /// <summary>Bounded temporary hazards and telegraphs. Only world impacts are persistent.</summary>
    public sealed class SoulEaterBattlefield : MonoBehaviour
    {
        sealed class Patch { public Vector3 point; public float age; public GameObject visual; public ParticleSystem[] particles; public bool fading; }
        readonly Stack<Patch> firePool=new Stack<Patch>();
        Transform fireRoot;
        public int FirePoolCount=>firePool.Count;
        readonly List<Patch> patches=new List<Patch>();
        ExplorationChunks world;
        SoulEaterPhaseOneController boss;SoulEaterPhaseOneSettings settings;Material flameMaterial,markerMaterial;
        bool ownsMarkerMaterial;
        LineRenderer marker,wave;float fireTick,waveAge;bool waveActive;Vector3 wavePoint;long waveId;
        public int FireCount=>patches.Count;
        public Vector3 MarkedPoint {get;private set;}
        public bool MarkerLocked {get;private set;}
        public bool MarkerVisible=>marker!=null&&marker.enabled;
        string saveError;float errorUntil;
        public void Initialize(SoulEaterPhaseOneController owner,Material fire)
        {
            boss=owner;settings=owner.Settings;world=FindFirstObjectByType<ExplorationChunks>();flameMaterial=fire;
            markerMaterial=settings.telegraphMaterial!=null?settings.telegraphMaterial:settings.telegraphPrefab!=null?settings.telegraphPrefab.sharedMaterial:null;
            if(markerMaterial==null){markerMaterial=RuntimeParticleMaterial.Create("Soul Eater attack warnings",Color.white);ownsMarkerMaterial=true;}
            marker=Line("Attack warning",settings.telegraphWidth);wave=Line("Impact shockwave",settings.shockwaveWidth);
            // A stationary scene-owned pool: hazards must not follow the moving dragon.
            fireRoot=new GameObject("Soul Eater ground fire pool").transform;
            if(settings.groundFirePrefab!=null)
                for(int i=0;i<settings.maximumGroundFires;i++)
                {
                    var go=Instantiate(settings.groundFirePrefab,fireRoot);go.name="Soul Eater ground fire (pooled)";
                    var particles=go.GetComponentsInChildren<ParticleSystem>(true);
                    foreach(var ps in particles){var main=ps.main;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
                    go.SetActive(false);firePool.Push(new Patch{visual=go,particles=particles});
                }
        }
        LineRenderer Line(string name,float width)
        {
            var line=settings.telegraphPrefab!=null?Instantiate(settings.telegraphPrefab,transform):new GameObject(name).AddComponent<LineRenderer>();
            line.name=name;line.transform.SetParent(transform,false);line.sharedMaterial=markerMaterial;line.useWorldSpace=true;line.widthMultiplier=width;line.loop=true;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        public bool CanFellTree(WorldDestructible tree)=>tree!=null&&tree.IsTree&&tree.gameObject.activeInHierarchy&&(world==null||world.CanDragonFellTree(tree));
        public bool CanDestroyProp(WorldDestructible prop)=>prop!=null&&WorldDestructible.Allowed(prop.Kind)&&prop.gameObject.activeInHierarchy&&(world==null||world.CanDragonDestroyProp(prop));
        public bool TryDestroyProps(IReadOnlyList<WorldDestructible> trees,Vector3 origin)
        {
            if(world!=null)
            {
                if(world.TryDragonDestroyProps(trees,origin))return true;
                saveError="No se pudo guardar la destrucción de los objetos. "+WorldSession.LastError;errorUntil=Time.unscaledTime+8;return false;
            }
            foreach(var tree in trees)tree.Break(origin,true);
            Physics.SyncTransforms();return true;
        }
        public bool TryTerrainPoint(Vector3 p,out Vector3 floor)
        {floor=p;if(world==null)return false;floor.y=world.SurfaceHeight(p);return true;}
        public float GroundY(Vector3 p)
        {
            if(world!=null)return world.SurfaceHeight(p);
            float best=float.PositiveInfinity,y=p.y;
            foreach(var hit in Physics.RaycastAll(new Vector3(p.x,boss.transform.position.y+40,p.z),Vector3.down,100,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.GetComponentInParent<Health>()==null && hit.normal.y>.7f && hit.distance<best){best=hit.distance;y=hit.point.y;}
            return y;
        }
        void Ring(LineRenderer line,Vector3 p,float radius,Color color)
        {
            int segments=Mathf.Clamp(settings.telegraphSegments,16,128);line.enabled=true;line.positionCount=segments;line.startColor=line.endColor=color;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;var q=p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;q.y=GroundY(q)+settings.telegraphGroundOffset;line.SetPosition(i,q);}
        }
        public void Circle(Vector3 p,float radius,bool locked)
        {MarkedPoint=p;MarkerLocked=locked;Ring(marker,p,radius,locked?settings.lockedTelegraphColor:settings.trackingTelegraphColor);}
        public void Strip(Vector3 a,Vector3 b,float radius,bool locked)
        {
            MarkedPoint=(a+b)*.5f;MarkerLocked=locked;
            var side=Vector3.Cross(Vector3.up,(b-a).normalized)*radius;marker.enabled=true;marker.positionCount=4;
            var points=new[]{a-side,a+side,b+side,b-side};
            for(int i=0;i<4;i++){points[i].y=GroundY(points[i])+settings.telegraphGroundOffset;marker.SetPosition(i,points[i]);}
            marker.startColor=marker.endColor=locked?settings.lockedTelegraphColor:settings.trackingTelegraphColor;
        }
        public void HideMarker(){if(marker!=null)marker.enabled=false;MarkerLocked=false;}
        public void AddFire(Vector3 p)
        {
            p.y=GroundY(p)+.1f;
            foreach(var patch in patches)if(Vector3.Distance(patch.point,p)<settings.groundFireSpacing)return;
            if(patches.Count>=settings.maximumGroundFires)return;
            if(firePool.Count==0)return;
            var next=firePool.Pop();next.point=p;next.age=0;next.fading=false;
            next.visual.transform.SetPositionAndRotation(p,Quaternion.identity);
            next.visual.transform.localScale=Vector3.one*(settings.groundFireRadius/Mathf.Max(.1f,settings.groundFirePrefabRadius));
            next.visual.SetActive(true);
            foreach(var ps in next.particles)ps.Play(false);
            patches.Add(next);
        }
        public void Impact(Vector3 p)
        {
            HideMarker();wavePoint=p;waveAge=0;waveActive=true;waveId=AttackIdentity.Next();
            if(world!=null && world.CanDragonImpact(p,settings.impactRadius) && !world.TryDragonImpact(p,settings.impactRadius,settings.craterDepth))
            {saveError="No se pudo guardar la destrucción del impacto. "+WorldSession.LastError;errorUntil=Time.unscaledTime+8;}
            WorldImpactDebris.Emit(p+Vector3.up*.5f,new Color(.3f,.22f,.13f),80);
            DamageCircle(p,0,settings.impactRadius,settings.diveDamage,waveId);
            Ring(wave,p,settings.impactRadius,settings.shockwaveColor);
        }
        void DamageCircle(Vector3 p,float inner,float outer,float amount,long id)
        {
            if(boss.Target==null)return;var target=boss.Target;float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(target.position.x,target.position.z));
            if(distance<inner || distance>outer || Mathf.Abs(target.position.y-GroundY(target.position))>settings.impactDamageHeight)return;
            target.GetComponent<DamageReceiver>()?.ReceiveDamage(new DamageInfo(amount,boss.gameObject,target.position,target.position-p,id,area:true,origin:p,parryable:false));
        }
        public void Tick(float dt)
        {
            if(settings==null || dt<=0)return;
            if(boss.Health.IsDead || boss.Target==null || boss.Target.GetComponent<Health>()?.IsDead==true){Clear();return;}
            if(waveActive)
            {
                float old=Mathf.Lerp(settings.impactRadius,settings.shockwaveRadius,Mathf.Clamp01(waveAge/settings.shockwaveTime));waveAge+=dt;
                float radius=Mathf.Lerp(settings.impactRadius,settings.shockwaveRadius,Mathf.Clamp01(waveAge/settings.shockwaveTime));
                DamageCircle(wavePoint,old,radius,settings.shockwaveDamage,waveId);
                var color=settings.shockwaveColor;color.a*=Mathf.Clamp01((settings.shockwaveTime+settings.shockwaveFadeTime-waveAge)/Mathf.Max(.01f,settings.shockwaveFadeTime));
                Ring(wave,wavePoint,radius,color);
                if(waveAge>=settings.shockwaveTime+settings.shockwaveFadeTime){waveActive=false;wave.enabled=false;}
            }
            bool damage=(fireTick-=dt)<=0;if(damage)fireTick=settings.groundFireInterval;
            // One shared attack identity per tick prevents stacked patches multiplying damage.
            long tick=damage?AttackIdentity.Next():0;
            for(int i=patches.Count-1;i>=0;i--)
            {
                var patch=patches[i];patch.age+=dt;
                if(patch.age>=settings.groundFireLifetime){Recycle(patch);patches.RemoveAt(i);continue;}
                if(!patch.fading && patch.age>=settings.groundFireLifetime-settings.groundFireFadeTime)
                {patch.fading=true;foreach(var ps in patch.particles)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);}
                if(damage)DamageCircle(patch.point,0,settings.groundFireRadius,settings.groundFireDamage,tick);
            }
        }
        void Recycle(Patch patch)
        {foreach(var ps in patch.particles)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);patch.visual.SetActive(false);firePool.Push(patch);}
        public void Clear()
        {HideMarker();if(wave!=null)wave.enabled=false;waveActive=false;foreach(var p in patches)Recycle(p);patches.Clear();}
        void OnGUI(){if(!GameplayPause.InterfaceHidden&&Time.unscaledTime<errorUntil)GUI.Box(new Rect(20,Screen.height-130,Mathf.Min(650,Screen.width-40),60),saveError);}
        void OnDisable()=>Clear();
        void OnDestroy(){if(fireRoot!=null)Destroy(fireRoot.gameObject);if(ownsMarkerMaterial && markerMaterial!=null)Destroy(markerMaterial);}
    }
}
