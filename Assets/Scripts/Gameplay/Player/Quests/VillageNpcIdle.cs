using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    /// <summary>A gentle rest motion for this pack, which contains rigs but no animation clips.</summary>
    public sealed class VillageNpcIdle:MonoBehaviour
    {
        public Transform spine,head;
        [Range(0,5)] public float breathingDegrees=.8f;
        [Range(0,20)] public float headTurnDegrees=5;
        [Min(.1f)] public float period=5;
        public float phase;
        Quaternion spineRest,headRest;
        bool initialized;
        void Awake(){if(spine!=null)spineRest=spine.localRotation;if(head!=null)headRest=head.localRotation;initialized=true;}
        void LateUpdate()
        {
            float t=Time.time*Mathf.PI*2/Mathf.Max(.1f,period)+phase;
            if(spine!=null)spine.localRotation=spineRest*Quaternion.Euler(Mathf.Sin(t)*breathingDegrees,0,0);
            if(head!=null)head.localRotation=headRest*Quaternion.Euler(0,Mathf.Sin(t*.37f)*headTurnDegrees,0);
        }
        void OnDisable(){if(!initialized)return;if(spine!=null)spine.localRotation=spineRest;if(head!=null)head.localRotation=headRest;}
    }
}
