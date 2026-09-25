using UnityEngine;
using UnityEngine.AI;
using Mismo.Gameplay.Player.Equipment.Inventory;

namespace Mismo.Gameplay.Player.World.Structures
{
    // The serialized slot survives rebakes; scene positions and list order never form the save key.
    public sealed class StructureContentPoint : MonoBehaviour
    {
        public string roomId;
        public StructureContentEntry data;
        public string PersistentId {get;private set;}
        public GameObject Spawned {get;private set;}
        public StructureInstance Owner {get;private set;}
        bool initialized,reported;
        float waiting;
        public void Populate(StructureInstance owner,PlayerInventory inventory,ExplorationWorldSettings settings,string region,bool navigationReady)
        {
            if(initialized)return;
            Owner=owner;if(PersistentId==null)PersistentId=owner.PersistentId+":content:"+data.id;
            if(inventory==null||!inventory.IsReady)return;
            if(data.kind==StructureContentKind.Enemy)
            {
                if(inventory.IsWorldEnemyDefeated(PersistentId)){initialized=true;return;}
                if(!navigationReady)return;
                var agent=data.prefab.GetComponent<NavMeshAgent>();
                var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                if(!NavMesh.SamplePosition(transform.position,out var hit,.8f,filter)||Mathf.Abs(hit.position.y-transform.position.y)>.6f)
                {
                    waiting+=Time.deltaTime;
                    if(waiting>15&&!reported){reported=true;Debug.LogWarning("No hay suelo navegable para "+data.label+" en "+owner.title,this);}
                    return;
                }
                // Configure identity before any controller's Awake/Start can grant rewards or scale health.
                var holder=new GameObject(data.label);holder.transform.SetParent(transform,false);holder.SetActive(false);
                Spawned=Instantiate(data.prefab,hit.position,transform.rotation,holder.transform);Spawned.transform.localScale*=data.scale;
                var identity=Spawned.GetComponent<WorldEnemyIdentity>()??Spawned.AddComponent<WorldEnemyIdentity>();
                identity.Configure(PersistentId,region,data.label,inventory,settings,data.enemyLevel);
                initialized=true;holder.SetActive(true);return;
            }
            if(data.kind==StructureContentKind.Resource)
            {
                if(!owner.RequirementMet(data.requirement,roomId,inventory))return;
                var node=GatheringDistribution.Place(data.resource,PersistentId,transform.position,transform);
                node.transform.localRotation=Quaternion.identity;node.transform.localScale=Vector3.one*data.scale;node.oneTime=data.resourceOneTime;Spawned=node.gameObject;
            }
            else
            {
                Spawned=Instantiate(data.prefab,transform);Spawned.transform.localPosition=Vector3.zero;Spawned.transform.localRotation=Quaternion.identity;Spawned.transform.localScale*=data.scale;
                if(data.kind==StructureContentKind.Chest)
                {var chest=gameObject.AddComponent<StructureChest>();chest.Configure(this,inventory,Spawned.transform);}
                else foreach(var collider in Spawned.GetComponentsInChildren<Collider>())collider.enabled=false;
            }
            initialized=true;
        }
        void OnDrawGizmosSelected()
        {
            if(data==null)return;
            Gizmos.color=data.kind==StructureContentKind.Enemy?new Color(1,.3f,.3f):data.kind==StructureContentKind.Chest?Color.yellow:Color.cyan;
            Gizmos.DrawWireSphere(transform.position+Vector3.up*.5f,.5f);
            Gizmos.DrawLine(transform.position,transform.position+transform.forward*1.3f);
        }
    }
}
