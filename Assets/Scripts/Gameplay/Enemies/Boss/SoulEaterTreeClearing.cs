using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Enemies
{
    public sealed partial class SoulEaterPhaseOneController
    {
        readonly Collider[] treeContacts=new Collider[128];
        readonly List<WorldDestructible> treesToFell=new List<WorldDestructible>(16);
        float treeRetryAt;
        bool TreeCanBeCleared(Collider obstacle)
        {
            if(!settings.crushTrees||clock<treeRetryAt||battlefield==null||
                (State!=SoulEaterState.Hunting&&State!=SoulEaterState.Charging&&State!=SoulEaterState.Braking))return false;
            return battlefield.CanFellTree(obstacle.GetComponentInParent<WorldDestructible>());
        }
        void ClearTreesForStep(Vector3 step,float groundY)
        {
            if(!settings.crushTrees||clock<treeRetryAt||battlefield==null||
                (State!=SoulEaterState.Hunting&&State!=SoulEaterState.Charging&&State!=SoulEaterState.Braking))return;
            var travel=step+step.normalized*Mathf.Clamp(settings.treeBreakReach,0,2);
            var size=groundBody!=null?groundBody.size:new Vector3(body.radius*2,body.height,body.radius*2);
            var center=groundBody!=null?transform.TransformPoint(groundBody.center):transform.position+body.center;
            center.y=Mathf.Max(transform.position.y,groundY)+settings.groundStepHeight+size.y*.5f;
            var local=Quaternion.Inverse(transform.rotation)*travel;
            var half=size*.5f+new Vector3(Mathf.Abs(local.x),0,Mathf.Abs(local.z))*.5f;
            int count=Physics.OverlapBoxNonAlloc(center+travel*.5f,half,treeContacts,transform.rotation,~0,QueryTriggerInteraction.Ignore);
            if(count==treeContacts.Length)return;
            treesToFell.Clear();
            for(int i=0;i<count;i++)
            {
                var obstacle=treeContacts[i];if(!TreeCanBeCleared(obstacle))continue;
                var tree=obstacle.GetComponentInParent<WorldDestructible>();
                if(Vector3.Dot(Planar(tree.transform.position-transform.position),step.normalized)<0||treesToFell.Contains(tree))continue;
                treesToFell.Add(tree);
            }
            if(treesToFell.Count==0)return;
            if(!battlefield.TryFellTrees(treesToFell,transform.position))treeRetryAt=clock+1;
            localPath?.Clear();
        }
    }
}
