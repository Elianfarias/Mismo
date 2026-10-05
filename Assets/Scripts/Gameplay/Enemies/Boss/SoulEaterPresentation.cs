using System.Collections.Generic;
using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    public sealed partial class SoulEaterPhaseOneController
    {
        SoulEaterFootSupport footSupport;
        Vector3 visualPosition,headForward;
        Transform head;
        bool breathReleased;
        void InitializePresentation()
        {
            effects?.ConfigureDust(settings);
            visualPosition=rig.transform.localPosition;
            foreach(var bone in rig.surface.bones)if(bone.name=="Head")head=bone;
            if(head!=null)headForward=head.InverseTransformDirection(transform.forward);
            if(settings.breathMaterial!=null)effects?.Flame?.Configure(settings.breathMaterial);
            effects?.Flame?.ConfigurePrefab(settings.breathPrefab,settings.breathParticleBudget);
            footSupport=new SoulEaterFootSupport(rig,SupportGroundHeight);
        }
        float SupportGroundHeight(Vector3 point)
        {
            point.y=transform.position.y;
            return Ground(point,out var floor)&&Mathf.Abs(floor.y-transform.position.y)<=settings.groundStepHeight?floor.y:float.NaN;
        }
        void SupportFeet()
        {
            // The controller owns the retreat arc. Imported takeoff/landing also contain vertical
            // body travel; anchor the soles to the moving arc so that lift is not applied twice.
            if(State==SoulEaterState.RetreatJump){footSupport?.ApplyAtHeight(settings.soleClearance,transform.position.y);return;}
            if(Airborne || State==SoulEaterState.Dead)return;
            footSupport?.Apply(settings.soleClearance);
        }
        bool chargeDustArmed,chargeDustDone;
        void ChargeDustContact()
        {
            bool rearing=State==SoulEaterState.ChargeWindup||State==SoulEaterState.Braking;
            bool settling=State==SoulEaterState.Charging||State==SoulEaterState.Hunting&&elapsed<.4f;
            if(footSupport==null||(!rearing&&!settling)){chargeDustArmed=false;return;}
            if(chargeDustDone)return;
            if(rearing&&footSupport.FrontClearance>=settings.chargeDustLiftHeight)chargeDustArmed=true;
            if(chargeDustArmed&&footSupport.FrontClearance<=settings.chargeDustContactHeight)
            {chargeDustDone=true;chargeDustArmed=false;effects?.DustImpact(footSupport.FrontContactPoint,SoulEaterDustKind.Charge);}
        }
        bool GroundBreath=>Action==SoulEaterAction.Breath && (State==SoulEaterState.Windup || State==SoulEaterState.Active);
        void TrackBreathBody(float dt)
        {
            if(!GroundBreath || target==null || !phaseTwo&&breathReleased)return;
            var d=Planar(target.position-transform.position);
            if(d.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),Mathf.Max(0,settings.breathTurnSpeed)*dt);
        }
        Vector3 GroundBreathDirection()
        {
            var flat=target!=null?Planar(target.position-MouthPosition):transform.forward;
            if(flat.sqrMagnitude<.001f)flat=transform.forward;
            return Quaternion.LookRotation(flat.normalized)*Quaternion.Euler(settings.breathPitch,0,0)*Vector3.forward;
        }
        void AimBreathHead(float dt)
        {
            if(!GroundBreath)return;
            bool tracking=phaseTwo||!breathReleased;
            if(tracking)
            {
                var desiredAim=GroundBreathDirection();
                aim=phaseTwo?Vector3.RotateTowards(aim,desiredAim,Mathf.Max(0,settings.phaseTwoBreathTrackingSpeed)*Mathf.Deg2Rad*dt,0).normalized:desiredAim;
            }
            Vector3 desired=aim;
            if(head!=null)
            {
                var local=transform.InverseTransformDirection(desired);
                float yaw=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-settings.breathHeadYaw,settings.breathHeadYaw);
                float pitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-settings.breathHeadPitch,settings.breathHeadPitch);
                var direction=transform.rotation*Quaternion.Euler(pitch,yaw,0)*Vector3.forward;
                head.rotation=Quaternion.FromToRotation(head.TransformDirection(headForward),direction)*head.rotation;
            }
        }
    }
    internal sealed class SoulEaterFootSupport
    {
        struct Sole { public Vector3 vertex; public BoneWeight weight; public int foot; }
        sealed class Geometry
        {
            public Sole[] soles;
            public Matrix4x4[] bindPoses;
            public int[] feet;
        }
        static readonly Dictionary<Mesh,Geometry> geometryCache=new Dictionary<Mesh,Geometry>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache()=>geometryCache.Clear();

        readonly Mismo.Gameplay.Player.Voxels.VoxelRigInstance rig;
        readonly System.Func<Vector3,float> groundHeight;
        readonly Sole[] soles;
        readonly Transform[] bones,feet;
        readonly Vector3[] footPositions;
        readonly Vector3[] supports=new Vector3[16];
        readonly Matrix4x4[] skinMatrices,bindPoses;
        public SoulEaterFootSupport(Mismo.Gameplay.Player.Voxels.VoxelRigInstance rig,System.Func<Vector3,float> groundHeight)
        {
            this.rig=rig;this.groundHeight=groundHeight;
            // Renderer.bones returns a copy. Read it once per rig, never inside a vertex loop.
            bones=rig.surface.bones;
            var mesh=rig.surface.sharedMesh;
            if(!geometryCache.TryGetValue(mesh,out var geometry))
            {geometry=BuildGeometry(mesh,bones);geometryCache.Add(mesh,geometry);}
            soles=geometry.soles;bindPoses=geometry.bindPoses;skinMatrices=new Matrix4x4[bindPoses.Length];
            feet=new Transform[geometry.feet.Length];footPositions=new Vector3[feet.Length];
            for(int i=0;i<feet.Length;i++)feet[i]=bones[geometry.feet[i]];
        }
        static Geometry BuildGeometry(Mesh mesh,Transform[] bones)
        {
            var feet=new List<int>(4);
            foreach(string name in new[]{"Hand_Left","Hand_Right","Feet_Left","Feet_Right"})
            {int foot=System.Array.FindIndex(bones,t=>t.name==name);if(foot>=0)feet.Add(foot);}
            // Classify the skeleton once, then scan the mesh once. Templates contain no scene transforms
            // and can be shared by the arrival visual, combat boss and subsequent summons.
            var group=new int[bones.Length];
            for(int b=0;b<bones.Length;b++)
            {
                group[b]=-1;
                for(int f=0;f<feet.Count;f++)
                    if(bones[b]==bones[feet[f]]||bones[b].IsChildOf(bones[feet[f]])){group[b]=f;break;}
            }
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var result=new List<Sole>();var unique=new HashSet<(Vector3,BoneWeight)>();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];int foot=group[w.boneIndex0];
                if(foot>=0 && unique.Add((vertices[i],w)))result.Add(new Sole{vertex=vertices[i],weight=w,foot=foot});
            }
            return new Geometry{soles=result.ToArray(),bindPoses=mesh.bindposes,feet=feet.ToArray()};
        }
        public float FrontClearance {get;private set;}
        public Vector3 FrontContactPoint {get;private set;}
        public void Apply(float clearance)=>ApplySupport(clearance,false,0);
        public void ApplyAtHeight(float clearance,float height)=>ApplySupport(clearance,true,height);
        void ApplySupport(float clearance,bool usePlane,float height)
        {
            if((!usePlane&&groundHeight==null)||soles.Length==0)return;
            for(int i=0;i<skinMatrices.Length;i++)skinMatrices[i]=bones[i].localToWorldMatrix*bindPoses[i];
            for(int i=0;i<feet.Length;i++)footPositions[i]=feet[i].position;
            for(int i=0;i<supports.Length;i++)supports[i]=new Vector3(0,float.PositiveInfinity,0);
            foreach(var sole in soles)
            {
                var p=sole.vertex;var w=sole.weight;
                p=skinMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+skinMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+
                    skinMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+skinMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                var center=footPositions[sole.foot];int sector=sole.foot*4+(p.x>center.x?1:0)+(p.z>center.z?2:0);
                if(p.y<supports[sector].y)supports[sector]=p;
            }
            float lift=float.NegativeInfinity;
            FrontClearance=float.PositiveInfinity;
            for(int i=0;i<supports.Length;i++)
            {
                var p=supports[i];if(float.IsPositiveInfinity(p.y))continue;float ground=usePlane?height:groundHeight(p);
                if(float.IsNaN(ground)||float.IsInfinity(ground))continue;
                lift=Mathf.Max(lift,ground+clearance-p.y);
                if(i<8&&p.y-ground<FrontClearance){FrontClearance=p.y-ground;FrontContactPoint=new Vector3(p.x,ground,p.z);}
            }
            if(!float.IsNegativeInfinity(lift)){rig.transform.position+=Vector3.up*lift;FrontClearance+=lift;}
        }
    }
}
