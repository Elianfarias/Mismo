using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    /// <summary>Immutable flight used by both the preview and the thrown object.</summary>
    public readonly struct GroundThrowPath
    {
        public readonly Vector3 start, end, normal;
        public readonly float height, duration;
        public readonly bool valid;
        public GroundThrowPath(Vector3 start, Vector3 end, Vector3 normal, float height, float duration, bool valid)
        { this.start=start;this.end=end;this.normal=normal;this.height=height;this.duration=duration;this.valid=valid; }
        public Vector3 Sample(float t)
        {t=Mathf.Clamp01(t);return Vector3.Lerp(start,end,t)+Vector3.up*(4*height*t*(1-t));}
        public Quaternion LandingRotation(Vector3 forward)
        {
            forward=Vector3.ProjectOnPlane(forward,normal);
            if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(Vector3.forward,normal);
            return Quaternion.LookRotation(forward,normal);
        }
    }

    public static class GroundThrowTrajectory
    {
        public const int Segments=32;
        const float Clearance=.16f, Footprint=.43f;
        static readonly RaycastHit[] hits=new RaycastHit[96];
        static readonly Collider[] overlaps=new Collider[48];
        static bool Obstacle(Collider collider,GameObject owner)=>collider!=null&&!collider.transform.IsChildOf(owner.transform)&&collider.GetComponentInParent<Health>()==null;
        static bool Nearest(GameObject owner,Ray ray,float distance,out RaycastHit nearest)
        {
            nearest=default;float best=float.MaxValue;
            int count=Physics.RaycastNonAlloc(ray,hits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Obstacle(hits[i].collider,owner)&&hits[i].distance<best){nearest=hits[i];best=hits[i].distance;}
            return best<float.MaxValue;
        }
        public static bool Ground(GameObject owner,Vector3 point,float above,float below,out RaycastHit hit)=>
            Nearest(owner,new Ray(point+Vector3.up*above,Vector3.down),above+below,out hit);
        public static GroundThrowPath Aim(GameObject owner,Ray ray,float range,TrapAction settings)
        {
            float distance=range+Vector3.Distance(ray.origin,owner.transform.position)+2;
            Vector3 aim=Nearest(owner,ray,distance,out var sight)?sight.point:ray.GetPoint(distance);
            Vector3 offset=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(aim-owner.transform.position,Vector3.up),range);
            Vector3 point=owner.transform.position+offset;
            // Search from above the caster; the first surface wins, including an invalid steep one.
            bool found=Ground(owner,point,range+2,range+5,out var ground);
            if(found)point=ground.point;
            Vector3 normal=found?ground.normal:Vector3.up;
            return ToPoint(owner,point,normal,range,settings,found);
        }
        public static GroundThrowPath ToPoint(GameObject owner,Vector3 point,Vector3 normal,float range,TrapAction settings,bool found=true)
        {
            var collider=owner.GetComponent<Collider>();
            Vector3 start=collider!=null?collider.bounds.center+Vector3.up*collider.bounds.size.y*.18f:owner.transform.position+Vector3.up*1.1f;
            float distance=Vector3.Distance(start,point);
            float height=settings!=null?Mathf.Max(.2f,settings.arcHeight)+distance*.08f:0;
            float duration=settings!=null?Mathf.Clamp(distance/Mathf.Max(1,settings.throwSpeed),.25f,1.5f):0;
            bool valid=found&&normal.y>=.65f&&Vector3.Distance(owner.transform.position,point)<=range+.03f;
            if(valid&&settings!=null)valid=Supported(owner,point,normal);
            var path=new GroundThrowPath(start,point+normal*.012f,normal,height,duration,valid);
            if(valid&&settings!=null)
                for(int i=0;i<Segments;i++)if(Blocked(owner,path.Sample(i/(float)Segments),path.Sample((i+1f)/Segments),path.end,out _)){valid=false;break;}
            if(valid&&settings==null&&Nearest(owner,new Ray(start,(point-start).normalized),distance,out var obstruction))
                valid=Vector3.Distance(obstruction.point,point)<.15f;
            return new GroundThrowPath(path.start,path.end,normal,height,duration,valid);
        }
        public static bool Supported(GameObject owner,Vector3 point,Vector3 normal)
        {
            if(!Ground(owner,point,.25f,.3f,out var center)||center.normal.y<.65f||Vector3.Distance(center.point,point)>.12f)return false;
            var right=Vector3.Cross(normal,Vector3.forward).normalized;
            var forward=Vector3.Cross(right,normal).normalized;
            for(int i=0;i<4;i++)
            {
                var offset=(i<2?right:forward)*(i%2==0?Footprint:-Footprint);
                if(!Ground(owner,point+offset,.35f,.5f,out var edge)||edge.normal.y<.65f||Mathf.Abs(Vector3.Dot(edge.point-point,normal))>.2f)return false;
            }
            int count=Physics.OverlapSphereNonAlloc(point+normal*.35f,.3f,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(Obstacle(overlaps[i],owner))return false;
            return true;
        }
        // The same swept clearance is checked when aiming and again during flight for moving obstacles.
        public static bool Blocked(GameObject owner,Vector3 from,Vector3 to,Vector3 landing,out Vector3 point)
        {
            point=to;var delta=to-from;if(delta.sqrMagnitude<.000001f)return false;
            int touching=Physics.OverlapSphereNonAlloc(from,Clearance,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(touching==overlaps.Length)return true;
            for(int i=0;i<touching;i++)if(Obstacle(overlaps[i],owner)&&!LandingContact(overlaps[i],from,landing))return true;
            int count=Physics.SphereCastNonAlloc(from,Clearance,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return true;
            float best=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(!Obstacle(hit.collider,owner))continue;
                // PhysX reports a zero point/normal when a sweep starts touching its floor.
                if(hit.distance<=.0001f&&LandingContact(hit.collider,from,landing))continue;
                // Permit final contact with the supporting floor, never a wall beside it.
                if(hit.normal.y>=.65f&&Vector3.Distance(hit.point,landing)<.32f)continue;
                if(hit.distance<best){best=hit.distance;point=hit.point;}
            }
            return best<float.MaxValue;
        }
        static bool LandingContact(Collider collider,Vector3 position,Vector3 landing)=>
            Vector3.Distance(position,landing)<.4f&&Vector3.Distance(collider.ClosestPoint(landing),landing)<.035f;
    }
}
