using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    // Incremental local A*: cached cell probes, a fixed grid and bounded work per frame.
    // It remains usable while the world navmesh is rebuilding after a crater.
    internal sealed class SoulEaterPathfinding
    {
        const int Side=41,Count=Side*Side;
        readonly float[] cost=new float[Count],score=new float[Count];
        readonly int[] parents=new int[Count],heap=new int[Count],heapIndex=new int[Count];
        readonly byte[] state=new byte[Count],walkable=new byte[Count];
        readonly Vector3[] points=new Vector3[Count];
        readonly List<Vector3> route=new List<Vector3>();
        readonly Func<Vector3,Vector3> floor;
        readonly Func<Vector3,Vector3,bool> clear;
        readonly Func<Vector3,Vector3,bool> sight;
        Vector3 origin,goal;
        float cell,stopDistance,maxStep;
        int size,best,routeIndex;
        bool joinRoute;
        public bool Searching {get;private set;}
        public bool HasRoute=>routeIndex<route.Count;
        public SoulEaterPathfinding(Func<Vector3,Vector3> floor,Func<Vector3,Vector3,bool> clear,Func<Vector3,Vector3,bool> sight)
        {this.floor=floor;this.clear=clear;this.sight=sight;}
        public void Clear(){Searching=false;route.Clear();routeIndex=0;joinRoute=false;}
        public void Begin(Vector3 start,Vector3 destination,float spacing,float stop,float step)
        {
            // Keep following the current route while its replacement is being calculated.
            origin=start;goal=destination;cell=Mathf.Max(.5f,spacing);stopDistance=stop;maxStep=step;
            Array.Clear(state,0,Count);Array.Clear(walkable,0,Count);size=0;
            int index=(Side/2)*Side+Side/2;points[index]=start;walkable[index]=1;
            cost[index]=0;score[index]=Distance(start,goal);parents[index]=-1;best=index;
            Push(index);Searching=true;
        }
        public void Step(int budget)
        {
            while(Searching && budget-->0)
            {
                if(size==0){Finish(best);break;}
                int current=Pop();state[current]=2;var p=points[current];float remaining=Distance(p,goal);
                if(remaining<Distance(points[best],goal))best=current;
                if(remaining<=stopDistance && sight(p,goal)){Finish(current);break;}
                int x=current%Side,z=current/Side;
                // A far-away target need not exhaust the grid: continue from its reachable edge.
                if((x==0||z==0||x==Side-1||z==Side-1)&&remaining<Distance(origin,goal)-cell){Finish(current);break;}
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;
                    if(nx<0||nz<0||nx>=Side||nz>=Side)continue;
                    int next=nz*Side+nx;if(state[next]==2||!Walkable(next))continue;
                    // No diagonal cutting across the inflated corner of a tree or a rock.
                    if(dx!=0&&dz!=0&&(!Walkable(z*Side+nx)||!Walkable(nz*Side+x)))continue;
                    if(Mathf.Abs(points[next].y-p.y)>maxStep)continue;
                    float g=cost[current]+cell*(dx!=0&&dz!=0?1.414214f:1);
                    if(state[next]==1 && g>=cost[next])continue;
                    cost[next]=g;score[next]=g+Distance(points[next],goal);parents[next]=current;
                    if(state[next]==0)Push(next);else Up(heapIndex[next]);
                }
            }
        }
        bool Walkable(int i)
        {
            if(walkable[i]!=0)return walkable[i]==1;
            var p=origin+new Vector3(i%Side-Side/2,0,i/Side-Side/2)*cell;
            points[i]=floor(p);bool valid=!float.IsNaN(points[i].y)&&clear(origin,points[i]);
            walkable[i]=(byte)(valid?1:2);return valid;
        }
        void Finish(int i)
        {
            Searching=false;route.Clear();
            while(parents[i]>=0){route.Add(points[i]);i=parents[i];}
            route.Reverse();routeIndex=0;joinRoute=true;
        }
        public bool Waypoint(Vector3 position,out Vector3 waypoint)
        {
            if(joinRoute && HasRoute)
            {
                // The dragon may have advanced along the old route during the search.
                float nearest=float.PositiveInfinity;
                for(int i=0;i<route.Count;i++){float d=Distance(position,route[i]);if(d<nearest){nearest=d;routeIndex=i;}}
                joinRoute=false;
            }
            while(HasRoute&&Distance(position,route[routeIndex])<.5f)routeIndex++;
            waypoint=HasRoute?route[routeIndex]:position;return HasRoute;
        }
        static float Distance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        void Push(int node){state[node]=1;heapIndex[node]=size;heap[size]=node;Up(size++);}
        void Up(int i){while(i>0){int parent=(i-1)/2;if(score[heap[parent]]<=score[heap[i]])break;Swap(i,parent);i=parent;}}
        int Pop()
        {
            int result=heap[0];size--;if(size==0)return result;heap[0]=heap[size];heapIndex[heap[0]]=0;
            int i=0;while(i*2+1<size){int child=i*2+1;if(child+1<size&&score[heap[child+1]]<score[heap[child]])child++;if(score[heap[i]]<=score[heap[child]])break;Swap(i,child);i=child;}
            return result;
        }
        void Swap(int a,int b){int t=heap[a];heap[a]=heap[b];heap[b]=t;heapIndex[heap[a]]=a;heapIndex[heap[b]]=b;}
    }

    public sealed partial class SoulEaterPhaseOneController
    {
        SoulEaterPathfinding localPath;
        readonly Collider[] pathObstacles=new Collider[128];
        float nextPath,movingUntil;
        Vector3 pathGoal;
        bool routeMoving;
        public bool FindingPath=>localPath?.Searching==true;
        Vector3 PathFloor(Vector3 p)
        {if(battlefield!=null&&battlefield.TryTerrainPoint(p,out var floor))return floor;return Ground(p,out floor)?floor:new Vector3(p.x,float.NaN,p.z);}
        bool PathSight(Vector3 from,Vector3 to)=>ClearLine(from+Vector3.up*2,to+Vector3.up,target,true);
        bool PathCellClear(Vector3 start,Vector3 p)
        {
            // Inflate obstacles by the full turning footprint, not the small humanoid navmesh agent.
            var size=groundBody!=null?groundBody.size:new Vector3(body.radius*2,body.height,body.radius*2);
            float radius=groundBody!=null?new Vector2(size.x*.5f,Mathf.Abs(groundBody.center.z)+size.z*.5f).magnitude+.15f:body.radius;
            var center=p+Vector3.up*(settings.groundStepHeight+size.y*.5f);
            int n=Physics.OverlapBoxNonAlloc(center,new Vector3(radius,size.y*.5f,radius),pathObstacles,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(n==pathObstacles.Length)return false;
            for(int i=0;i<n;i++)
            {
                var obstacle=pathObstacles[i];if(OwnOrTarget(obstacle.transform)||PropCanBeCleared(obstacle))continue;
                var nearest=obstacle.ClosestPoint(center);float distance=Planar(nearest-center).magnitude;
                if(distance>=radius)continue;
                // A newly blocked start can escape its existing overlap, but never move deeper into it.
                var startCenter=start+Vector3.up*(settings.groundStepHeight+size.y*.5f);
                float startDistance=Planar(obstacle.ClosestPoint(startCenter)-startCenter).magnitude;
                if(startDistance<radius && distance>startDistance+.1f)continue;
                return false;
            }
            return true;
        }
        bool GroundStepClear(Vector3 step)
        {
            if(step.sqrMagnitude<.000001f)return true;
            return GroundSweep(step,transform.position.y,false,true);
        }
        void SetRouteMotion(bool moved)
        {routeMoving=moved;if(moved)movingUntil=clock+settings.locomotionStopDelay;}
        bool TryPursuitCharge(Vector3 delta)
        {
            if(!settings.enablePursuitCharge || clock<nextPursuitCharge || delta.magnitude<settings.pursuitChargeTriggerDistance)return false;
            // Natural props break on contact. Buildings and protected props still require an A* detour.
            if(clock<nextPursuitProbe)return false;
            nextPursuitProbe=clock+.2f;
            var heading=Quaternion.LookRotation(delta);
            if(Quaternion.Angle(transform.rotation,heading)>12)return false;
            var step=delta.normalized*Mathf.Min(delta.magnitude,settings.pursuitChargeMaxDistance);
            if(!PathSight(transform.position,transform.position+step) || !GroundStepClear(step))return false;
            pursuitCharge=true;localPath?.Clear();followingTarget=false;
            Enter(SoulEaterState.ChargeWindup,settings.pursuitChargeWindup);
            return true;
        }
        void FollowGroundPath(Vector3 destination,float dt)
        {
            localPath??=new SoulEaterPathfinding(PathFloor,PathCellClear,PathSight);
            var direct=Planar(destination-transform.position);
            if(!localPath.Searching&&!localPath.HasRoute && PathSight(transform.position,destination) && GroundStepClear(direct))
            {Face(destination,dt);SetRouteMotion(MoveGround(direct.normalized*Mathf.Min(direct.magnitude,settings.movementSpeed*dt)));return;}
            if(!localPath.Searching && (!localPath.HasRoute || Planar(destination-pathGoal).sqrMagnitude>16)&&clock>=nextPath)
            {pathGoal=destination;localPath.Begin(transform.position,destination,settings.pathCellSize,settings.biteRange*.8f,settings.groundStepHeight);nextPath=clock+settings.pathReplanInterval;}
            localPath.Step(settings.pathNodesPerFrame);
            if(localPath.Waypoint(transform.position,out var point))
            {
                Face(point,dt);var step=Planar(point-transform.position);
                SetRouteMotion(MoveGround(step.normalized*Mathf.Min(step.magnitude,settings.movementSpeed*dt)));
                if(!routeMoving && clock>=nextPath){localPath.Clear();nextPath=clock+.2f;}
            }
            else routeMoving=false;
        }
    }
}
