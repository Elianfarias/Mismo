using System;
using System.Collections.Generic;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.World.Structures
{
    [Serializable] public sealed class StructureInteriorRoom
    {
        public Vector3 center;
        public float radius,height;
    }
    // Baked data is independent of later authoring edits. One owner chunk owns the entire structure.
    public sealed class StructureInstance : MonoBehaviour
    {
        public string title;
        public float footprintRadius=28;
        public Vector3 entrance,finalRoom;
        public StructureStyle style;
        public StructureRequirement finalRequirement;
        public string finalRoomId;
        public string PersistentId {get;private set;}
        StructureContentPoint[] content;
        PlayerInventory inventory;
        ExplorationWorldSettings settings;
        string region;
        bool navigationReady;
        public float moatRadius;
        public float FoundationOffset(Vector2 p)=>StructureDefinition.MoatDepth(p,moatRadius);
        public float ambientMultiplier=.14f;
        public StructureInteriorRoom[] rooms=Array.Empty<StructureInteriorRoom>();
        public Vector3[] corridorEnds=Array.Empty<Vector3>();
        public float[] corridorRadii=Array.Empty<float>();
        static readonly HashSet<StructureInstance> loaded=new HashSet<StructureInstance>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>loaded.Clear();
        void OnEnable()=>loaded.Add(this);
        void OnDisable()=>loaded.Remove(this);
        public void ConfigureWorld(string id)
        {PersistentId=id;content=GetComponentsInChildren<StructureContentPoint>(true);foreach(var reward in GetComponentsInChildren<StructureReward>(true))reward.Configure(id);}
        public void ActivateContent(PlayerInventory player,ExplorationWorldSettings world,string regionId,bool ready=true)
        {inventory=player;settings=world;region=regionId;navigationReady|=ready;Populate();}
        void Update()=>Populate();
        void Populate()
        {if(string.IsNullOrEmpty(PersistentId)||inventory==null||!inventory.IsReady)return;if(content==null)content=GetComponentsInChildren<StructureContentPoint>(true);foreach(var point in content)if(point!=null&&point.data!=null)point.Populate(this,inventory,settings,region,navigationReady);}
        public bool RequirementMet(StructureRequirement requirement,string roomId,PlayerInventory player)
        {
            if(requirement==StructureRequirement.None)return true;
            if(player==null||!player.IsReady||string.IsNullOrEmpty(PersistentId))return false;
            if(content==null)content=GetComponentsInChildren<StructureContentPoint>(true);
            foreach(var point in content)
                if(point!=null&&point.data!=null&&point.data.kind==StructureContentKind.Enemy&&point.data.requiredForClear&&(requirement==StructureRequirement.StructureCleared||point.roomId==roomId)&&!player.IsWorldEnemyDefeated(PersistentId+":content:"+point.data.id))return false;
            return true;
        }
        public float InteriorWeight(Vector3 world)
        {
            if(style!=StructureStyle.Cave&&style!=StructureStyle.Temple)return 0;
            var p=transform.InverseTransformPoint(world);if(p.y<-.5f)return 0;
            bool inside=false;
            foreach(var r in rooms)if(p.y<r.height&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(r.center.x,r.center.z))<r.radius){inside=true;break;}
            if(!inside)for(int i=0;i<corridorRadii.Length;i++)
            {
                var a=corridorEnds[i*2];var b=corridorEnds[i*2+1];
                if(p.y<5.5f&&StructureDefinition.SegmentDistance(new Vector2(p.x,p.z),new Vector2(a.x,a.z),new Vector2(b.x,b.z))<corridorRadii[i]){inside=true;break;}
            }
            return inside?Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,p.z-entrance.z)):0;
        }
        public static float AmbientAt(Vector3 p)
        {float value=1;foreach(var instance in loaded)if(instance!=null)value=Mathf.Min(value,Mathf.Lerp(1,instance.ambientMultiplier,instance.InteriorWeight(p)));return value;}
    }
}
