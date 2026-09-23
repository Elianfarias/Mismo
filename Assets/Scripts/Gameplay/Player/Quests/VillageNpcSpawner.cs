using Mismo.Gameplay.Player.World;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    public static class VillageNpcSpawner
    {
        public static void Populate(GameObject village,ExplorationTerrain terrain,bool startingVillage)
        {
            var settings=QuestCatalog.Load()?.villageNpcs;
            if(settings==null||!settings.enabled||settings.startingVillageOnly&&!startingVillage||village.transform.Find("Quest residents")!=null)return;
            var group=new GameObject("Quest residents");group.transform.SetParent(village.transform,false);
            int index=0;
            foreach(var resident in settings.residents)
            {
                if(resident==null||resident.prefab==null)continue;
                var position=village.transform.TransformPoint(resident.localPosition);
                position.y=terrain.Height(position.x,position.z)+resident.localPosition.y;
                // Preserve human size when the village prefab is scaled up.
                var npc=Object.Instantiate(resident.prefab,position,village.transform.rotation*Quaternion.Euler(0,resident.yaw,0));
                npc.transform.localScale*=Mathf.Max(.1f,settings.residentScale);
                npc.transform.SetParent(group.transform,true);
                if(npc.TryGetComponent<QuestGiver>(out var giver)){giver.interactionRange=settings.interactionRange;giver.markerSettings=settings;}
                if(npc.TryGetComponent<VillageNpcRoutine>(out var routine))
                {
                    var route=resident.route ?? System.Array.Empty<Vector3>();
                    var stops=new Vector3[route.Length+1];stops[0]=position;
                    for(int i=0;i<route.Length;i++)
                    {stops[i+1]=village.transform.TransformPoint(route[i]);stops[i+1].y=terrain.Height(stops[i+1].x,stops[i+1].z)+route[i].y;}
                    routine.Initialize(stops,index++);
                }
            }
        }
    }
}
