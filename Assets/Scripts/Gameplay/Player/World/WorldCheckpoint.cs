using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    public sealed class WorldCheckpoint : MonoBehaviour
    {
        PlayerMotor motor;
        Health health;
        float nextSave;
        void Start(){motor=GetComponent<PlayerMotor>();health=GetComponent<Health>();nextSave=Time.unscaledTime+5;}
        void Update(){if(Time.unscaledTime>=nextSave){Save();nextSave=Time.unscaledTime+5;}}
        void Save()
        {
            if(WorldSession.Current==null||motor==null||!motor.IsGrounded||health==null||health.IsDead)return;
            var current=WorldSession.Current;
            bool sameTime=DayNightCycle.Current==null||current.hasTimeOfDay&&Mathf.Abs(DayNightCycle.Current.hour-current.timeOfDay)<.1f;
            if(sameTime&&Vector3.Distance(transform.position,new Vector3(current.x,current.y,current.z))<.1f&&Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,current.yaw))<2)return;
            WorldSession.Checkpoint(transform.position,transform.eulerAngles.y);
        }
        void OnApplicationPause(bool paused){if(paused)Save();}
        void OnApplicationFocus(bool focused){if(!focused)Save();}
        void OnApplicationQuit()=>Save();
        void OnDestroy()=>Save();
    }
}
