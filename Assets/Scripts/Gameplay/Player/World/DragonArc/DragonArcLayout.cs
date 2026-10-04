using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    /// <summary>Stable story locations in the introductory biome, independent of streaming order.</summary>
    public sealed class DragonArcLayout
    {
        public readonly DragonArcDefinition Definition;
        public readonly Vector3 Lookout, King, Arena, Altar, Retry;
        public readonly Vector3[] Beacons;
        public DragonArcLayout(WorldIntroductionLayout intro, DragonArcDefinition data)
        {
            Definition=data;
            Lookout=intro.Arrival+intro.Facing*new Vector3(4,0,4);Lookout.y=12;
            // The entrance road is authored clear; keep the audience out of house footprints.
            King=intro.Arrival+intro.Facing*new Vector3(-3,0,19);King.y=12;
            Arena=intro.Village+new Vector3(data.arenaOffset.x,0,data.arenaOffset.y);
            Altar=Arena+new Vector3(-data.arenaRadius+4,0,0);
            Retry=Altar+Vector3.left*7;
            Beacons=new Vector3[3];
            for(int i=0;i<3;i++) Beacons[i]=intro.Village+new Vector3(data.beaconOffsets[i].x,0,data.beaconOffsets[i].y);
        }
        static float Flat(Vector3 p,float x,float z)=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(x,z));
        public float Clearance(float x,float z)
        {
            float distance=Flat(Arena,x,z)-Definition.arenaRadius-8;
            foreach(var p in Beacons) distance=Mathf.Min(distance,Flat(p,x,z)-10);
            return distance;
        }
        public bool Reserved(float x,float z,float margin=0)=>Clearance(x,z)<margin;
        public float Flatten(float x,float z,float height)=>Mathf.Lerp(12,height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,16,Clearance(x,z))));
    }
}
