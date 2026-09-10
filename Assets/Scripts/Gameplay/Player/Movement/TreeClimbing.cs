using UnityEngine;
using Mismo.Gameplay.Player.World;
namespace Mismo.Gameplay.Player.Movement
{
    public sealed class TreeClimbing : MonoBehaviour
    {
        [SerializeField,Min(1)] float staminaPerSecond=35;
        [SerializeField,Min(.1f)] float speed=2.3f;
        private Collider trunk;
        private float retryAt;
        public bool IsClimbing=>trunk!=null;
        public void Release(){trunk=null;retryAt=Time.time+.4f;}
        public bool Step(PlayerMotor motor,Stamina stamina,Vector3 approach,float vertical,bool held,float dt)
        {
            if(!held){if(IsClimbing)Release();return false;}
            if(trunk==null)
            {
                if(Time.time<retryAt||approach.sqrMagnitude<.1f||stamina.Current<10)return false;
                if(!Physics.SphereCast(transform.position+Vector3.up*.9f,.15f,approach.normalized,out var hit,.85f,~0,QueryTriggerInteraction.Ignore)||hit.collider.GetComponent<ClimbableTree>()==null)return false;
                trunk=hit.collider;
            }
            Vector3 near=trunk.ClosestPoint(transform.position+Vector3.up*.9f);
            if(Vector3.Distance(near,transform.position+Vector3.up*.9f)>1||!trunk.gameObject.activeInHierarchy){Release();return false;}
            float cost=staminaPerSecond*dt;
            if(!stamina.TrySpend(cost)){stamina.TrySpend(stamina.Current);Release();return false;}
            Vector3 toward=Vector3.ProjectOnPlane(trunk.bounds.center-transform.position,Vector3.up).normalized;
            motor.RequestControlledDisplacement(new ControlledMovementRequest(Vector3.up*(vertical*speed*dt)+toward*(.5f*dt),toward,20,true,true));
            return true;
        }
    }
}
