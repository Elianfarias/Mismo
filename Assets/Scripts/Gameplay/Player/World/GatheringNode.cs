using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class GatheringNode:MonoBehaviour
    {
        public static readonly HashSet<GatheringNode> Loaded=new HashSet<GatheringNode>();
        public ResourceNodeDefinition definition;
        public string persistentId;
        GameObject visual;
        GameObject worldPrefab;
        Collider[] interactionColliders=System.Array.Empty<Collider>();
        bool usesWorldPrefab;
        bool depleted,initialized;
        PlayerInventory inventory;
        Dictionary<string,int> loot;
        public bool Available=>initialized&&!depleted;
        public void Configure(ResourceNodeDefinition data,string id){definition=data;persistentId=id;}
        public void UseWorldPrefab(GameObject prefab){worldPrefab=prefab;usesWorldPrefab=true;}
        void OnEnable(){Loaded.Add(this);interactionColliders=GetComponentsInChildren<Collider>();}
        void OnDisable()=>Loaded.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>Loaded.Clear();
        void Update()
        {
            if(definition==null)return;
            if(inventory==null)inventory=FindFirstObjectByType<PlayerInventory>();
            if(inventory==null||!inventory.IsReady)return;
            if(!initialized){initialized=true;depleted=inventory.NodeReadyAt(persistentId)>inventory.WorldPlaySeconds;Show();}
            if(depleted&&inventory.NodeReadyAt(persistentId)<=inventory.WorldPlaySeconds&&Vector3.Distance(inventory.transform.position,transform.position)>definition.restoreClearance)
            {depleted=false;loot=null;Show();}
        }
        void Show()
        {
            bool changedObstacle=visual!=null&&visual.GetComponentInChildren<Collider>()!=null;
            if(visual!=null){visual.SetActive(false);Destroy(visual);}
            var prefab=usesWorldPrefab?(depleted?null:worldPrefab):(depleted?definition.depletedPrefab:definition.availablePrefab);
            if(prefab!=null){visual=Instantiate(prefab,transform);visual.transform.localPosition=Vector3.zero;visual.SetActive(true);}
            interactionColliders=visual!=null?visual.GetComponentsInChildren<Collider>():GetComponents<Collider>();
            if(changedObstacle||visual!=null&&visual.GetComponentInChildren<Collider>()!=null)
                FindFirstObjectByType<ExplorationChunks>()?.RefreshResourceNavigation();
        }
        // Reach the visible obstacle from any side, rather than its potentially buried pivot.
        public Vector3 InteractionPoint(Vector3 from)
        {
            var closest=transform.position+Vector3.up*.5f;
            float best=float.MaxValue;
            foreach(var collider in interactionColliders)
            {
                if(collider==null||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;
                var point=collider is MeshCollider mesh&&!mesh.convex?collider.bounds.ClosestPoint(from):collider.ClosestPoint(from);
                float distance=(point-from).sqrMagnitude;
                if(distance<best){best=distance;closest=point;}
            }
            return closest;
        }
        public bool InRange(Transform player)
        {
            if(definition==null||player==null||Vector3.Distance(player.position,InteractionPoint(player.position))>definition.interactionRange)return false;
            var from=player.position+Vector3.up;var to=InteractionPoint(from);
            foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(player)&&!hit.transform.IsChildOf(transform))return false;
            return true;
        }
        public bool Complete(PlayerInventory owner)
        {
            if(!Available||!InRange(owner.transform)||definition.rewards==null)return false;
            if(loot==null)loot=definition.rewards.Roll();
            if(!owner.TryHarvest(persistentId,definition,loot))return false;
            if(definition.kind==ResourceNodeKind.Tree&&visual!=null)
            {
                var falling=Instantiate(visual,visual.transform.position,visual.transform.rotation);
                foreach(var collider in falling.GetComponentsInChildren<Collider>())collider.enabled=false;
                falling.AddComponent<HarvestFall>();
            }
            depleted=true;loot=null;Show();
            if(definition.completionSound!=null)AudioSource.PlayClipAtPoint(definition.completionSound,transform.position);
            return true;
        }
    }
    public sealed class HarvestFall:MonoBehaviour
    {
        float age;Quaternion start;
        void Awake(){start=transform.rotation;}
        void Update(){age+=Time.deltaTime;transform.rotation=start*Quaternion.Euler(0,0,Mathf.SmoothStep(0,85,age/1.1f));if(age>1.8f)Destroy(gameObject);}
    }
}
