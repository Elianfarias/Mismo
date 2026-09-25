using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    // A retrying volume: opening a menu while crossing it cannot lose the lesson.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialTrigger : MonoBehaviour
    {
        public TutorialGuide guide;
        public TutorialSequence sequence;
        BoxCollider volume;
        void Awake(){volume=GetComponent<BoxCollider>();volume.isTrigger=true;}
        void Update()
        {
            if(guide==null||sequence==null||guide.WasDismissed(sequence))return;
            var local=transform.InverseTransformPoint(guide.transform.position)-volume.center;
            var half=volume.size*.5f;
            if(Mathf.Abs(local.x)<=half.x&&Mathf.Abs(local.y)<=half.y&&Mathf.Abs(local.z)<=half.z)guide.TryBegin(sequence);
        }
        void OnDrawGizmosSelected()
        {
            var box=GetComponent<BoxCollider>();if(box==null)return;
            var old=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(1,.8f,.3f,.25f);Gizmos.DrawCube(box.center,box.size);Gizmos.matrix=old;
        }
    }
}
