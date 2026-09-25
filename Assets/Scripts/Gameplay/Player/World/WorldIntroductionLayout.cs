using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    // Version 1: fixed coordinates relative to the seed's starting village; never applied to old saves.
    public sealed class WorldIntroductionLayout
    {
        public readonly WorldIntroductionDefinition Definition;
        public readonly Vector3 Village,Cave,Grove,Exit,Spawn,Arrival;
        public readonly Vector3[] Route;
        public readonly Quaternion Facing,CaveRotation;
        public WorldIntroductionLayout(Vector2 village,WorldIntroductionDefinition definition,Vector3 arrivalOffset)
        {
            Definition=definition;Village=new Vector3(village.x,12,village.y);
            // Match the actual town's authored gate, including its offset and orientation.
            Facing=Quaternion.Euler(0,Mathf.Atan2(arrivalOffset.x,arrivalOffset.z)*Mathf.Rad2Deg+180,0);
            CaveRotation=Facing*Quaternion.Euler(0,180,0);
            Cave=Village+Facing*new Vector3(0,.05f,-330);
            Spawn=Cave+CaveRotation*definition.caveSpawn;
            Exit=Cave+Facing*new Vector3(0,0,definition.caveRadius+3);
            Grove=Village+Facing*new Vector3(26,.05f,-249);
            Arrival=Village+arrivalOffset+Vector3.up*.3f;
            Route=new Vector3[definition.encounters.Length+3];Route[0]=Exit;Route[1]=Grove;
            for(int i=0;i<definition.encounters.Length;i++)Route[i+2]=Encounter(i);
            Route[Route.Length-1]=Arrival;
        }
        public Vector3 Encounter(int index)
        {var p=Definition.encounters[index].offset;return Village+Facing*new Vector3(p.x,.2f,p.y);}
        static float FlatDistance(Vector3 a,float x,float z)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(x,z));
        public float RoadDistance(float x,float z)
        {
            var p=new Vector2(x,z);float distance=float.MaxValue;
            for(int i=1;i<Route.Length;i++)
            {
                var a=new Vector2(Route[i-1].x,Route[i-1].z);var b=new Vector2(Route[i].x,Route[i].z);var delta=b-a;
                float t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude):0;
                distance=Mathf.Min(distance,Vector2.Distance(p,a+delta*t));
            }
            return distance;
        }
        public float Clearance(float x,float z)=>Mathf.Min(FlatDistance(Cave,x,z)-Definition.caveRadius-2,
            Mathf.Min(FlatDistance(Grove,x,z)-24,RoadDistance(x,z)-10));
        public bool Reserved(float x,float z,float margin=0)=>Clearance(x,z)<margin;
        public float Flatten(float x,float z,float height)=>Mathf.Lerp(12,height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,16,Clearance(x,z))));
    }
}
