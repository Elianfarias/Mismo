using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    public sealed partial class SoulEaterPhaseOneController
    {
        readonly Collider[] propContacts=new Collider[128];
        readonly List<WorldDestructible> propsToBreak=new List<WorldDestructible>(16);
        readonly List<WorldDestructible> nearbyProps=new List<WorldDestructible>(64);
        float propRetryAt;
        bool PropCanBeCleared(Collider obstacle)
        {
            if(!settings.crushWorldProps||clock<propRetryAt||battlefield==null||
                (State!=SoulEaterState.Hunting&&State!=SoulEaterState.Charging&&State!=SoulEaterState.Braking))return false;
            return battlefield.CanDestroyProp(obstacle.GetComponentInParent<WorldDestructible>());
        }
        void ClearPropsForStep(Vector3 step,float groundY)
        {
            if(!settings.crushWorldProps||clock<propRetryAt||battlefield==null||
                (State!=SoulEaterState.Hunting&&State!=SoulEaterState.Charging&&State!=SoulEaterState.Braking))return;
            var travel=step+step.normalized*Mathf.Clamp(settings.propBreakReach,0,2);
            var size=groundBody!=null?groundBody.size:new Vector3(body.radius*2,body.height,body.radius*2);
            var center=groundBody!=null?transform.TransformPoint(groundBody.center):transform.position+body.center;
            center.y=Mathf.Max(transform.position.y,groundY)+settings.groundStepHeight+size.y*.5f;
            var local=Quaternion.Inverse(transform.rotation)*travel;
            var half=size*.5f+new Vector3(Mathf.Abs(local.x),0,Mathf.Abs(local.z))*.5f;
            int count=Physics.OverlapBoxNonAlloc(center+travel*.5f,half,propContacts,transform.rotation,~0,QueryTriggerInteraction.Ignore);
            propsToBreak.Clear();
            for(int i=0;i<count;i++)
            {
                var obstacle=propContacts[i];if(!PropCanBeCleared(obstacle))continue;
                var tree=obstacle.GetComponentInParent<WorldDestructible>();
                if(Vector3.Dot(Planar(tree.transform.position-transform.position),step.normalized)<0||propsToBreak.Contains(tree))continue;
                propsToBreak.Add(tree);
            }
            // Small decoration often has no collider. Query only nearby spatial cells, not the entire world.
            var sweepCenter=center+travel*.5f;
            WorldDestructible.Query(sweepCenter,half.magnitude+1,nearbyProps);
            foreach(var prop in nearbyProps)
            {
                if(!battlefield.CanDestroyProp(prop)||propsToBreak.Contains(prop))continue;
                var p=prop.transform.position;
                if(Vector3.Dot(Planar(p-transform.position),step.normalized)<0)continue;
                var offset=Quaternion.Inverse(transform.rotation)*(p-sweepCenter);
                if(Mathf.Abs(offset.x)>half.x+.3f||Mathf.Abs(offset.z)>half.z+.3f||p.y<groundY-2||p.y>center.y+half.y)continue;
                propsToBreak.Add(prop);
            }
            if(propsToBreak.Count==0)return;
            if(!battlefield.TryDestroyProps(propsToBreak,transform.position))propRetryAt=clock+1;
            localPath?.Clear();
        }
    }
}
