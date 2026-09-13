using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Aligns the visual with physical terrain without moving the navigation agent.</summary>
    [Serializable]
    public sealed class EnemyGroundSupport
    {
        [Tooltip("Distancia máxima entre el suelo físico y la altura del agente.")]
        [SerializeField, Min(.05f)] float probeDistance=.8f;
        [Tooltip("Altura de la suela en reposo respecto al origen del modelo, antes de su escala. Goblin Concept: 0,0325.")]
        [SerializeField, Min(0)] float soleClearance=.0325f;
        readonly RaycastHit[] hits=new RaycastHit[32];

        public bool Apply(Transform actor,Transform visual,float modelScale, bool visualAlreadyGrounded=false)
        {
            Vector3 origin=actor.position+Vector3.up*probeDistance;
            int count=Physics.RaycastNonAlloc(origin,Vector3.down,hits,probeDistance*2,~0,QueryTriggerInteraction.Ignore);
            float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.normal.y<.5f||hit.collider.transform.IsChildOf(actor)||hit.collider.GetComponentInParent<Health>()!=null)continue;
                distance=Mathf.Min(distance,hit.distance);
            }
            if(float.IsPositiveInfinity(distance))return false;
            float ground=origin.y-distance;
            float clearance=visualAlreadyGrounded?0f:soleClearance*Mathf.Abs(modelScale);
            visual.position+=Vector3.up*(ground-actor.position.y-clearance);
            return true;
        }
    }
}
