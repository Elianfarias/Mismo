using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed class InventoryWorldAccess : MonoBehaviour
    {
        PlayerInventory inventory;
        GameObject chest;
        Material wood,metal;
        Vector3 arrival;
        float nextRefresh;
        readonly Dictionary<string,GameObject> lootMarkers=new Dictionary<string,GameObject>();
        public bool AtChest=>chest!=null&&Vector3.Distance(transform.position,chest.transform.position)<=Mathf.Max(1,InventorySettings.Current.chestRange);
        void Start()
        {
            inventory=GetComponent<PlayerInventory>();
            var world=WorldSession.Current;
            arrival=world!=null?new Vector3(world.spawnX,world.spawnY,world.spawnZ):transform.position;
            wood=MakeMaterial(new Color(.24f,.115f,.055f));metal=MakeMaterial(new Color(.68f,.52f,.26f));
        }
        static Material MakeMaterial(Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            return shader!=null?new Material(shader){color=color}:null;
        }
        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(inventory==null||!inventory.IsReady)return;
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+1;
                if(chest==null&&Vector3.Distance(transform.position,arrival)<50)CreateChest();
                var ids=new HashSet<string>();
                foreach(var loot in inventory.PendingLoot)
                {
                    ids.Add(loot.id);
                    if(!lootMarkers.ContainsKey(loot.id))
                    {
                        var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name="Botín pendiente";
                        marker.transform.position=new Vector3(loot.x,loot.y+.35f,loot.z);
                        marker.transform.localScale=new Vector3(.45f,.45f,.45f);
                        marker.GetComponent<Collider>().enabled=false;marker.GetComponent<Renderer>().sharedMaterial=metal;
                        lootMarkers.Add(loot.id,marker);
                    }
                }
                var removed=new List<string>();
                foreach(var pair in lootMarkers)if(!ids.Contains(pair.Key)){Destroy(pair.Value);removed.Add(pair.Key);}
                foreach(var id in removed)lootMarkers.Remove(id);
            }
            if(GetComponent<GatheringPlayer>()?.Busy==true||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.BlocksGameplay||Keyboard.current==null||!Keyboard.current.fKey.wasPressedThisFrame)return;
            if(AtChest){GetComponent<InventoryPanel>()?.OpenChest();return;}
            foreach(var loot in inventory.PendingLoot)
                if(Vector3.Distance(transform.position,new Vector3(loot.x,loot.y,loot.z))<=3){inventory.CollectPending(loot.id);break;}
        }
        void CreateChest()
        {
            var settings=InventorySettings.Current;
            for(int i=0;i<8;i++)
            {
                var offset=Quaternion.Euler(0,i*45,0)*settings.chestOffset;
                var candidate=arrival+offset;
                if(!Physics.Raycast(candidate+Vector3.up*8,Vector3.down,out var hit,24,~0,QueryTriggerInteraction.Ignore)||hit.normal.y<.8f)continue;
                if(Physics.CheckBox(hit.point+Vector3.up*.65f,new Vector3(.9f,.55f,.7f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
                chest=settings.chestPrefab!=null?Instantiate(settings.chestPrefab):new GameObject("Cofre personal");
                chest.transform.position=hit.point;
                if(settings.chestPrefab==null)
                {
                    Part("Madera",new Vector3(0,.4f,0),new Vector3(1.4f,.8f,.8f),wood);
                    Part("Tapa",new Vector3(0,.86f,0),new Vector3(1.48f,.12f,.88f),wood);
                    Part("Cierre",new Vector3(0,.62f,-.43f),new Vector3(.18f,.3f,.08f),metal);
                    foreach(float x in new[]{-.5f,.5f})Part("Banda",new Vector3(x,.41f,0),new Vector3(.09f,.84f,.84f),metal);
                }
                return;
            }
        }
        void Part(string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(chest.transform,false);
            part.transform.localPosition=position;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=material;
        }
        void OnGUI()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput) return;
            if(inventory==null||!inventory.CanManage||InventoryPanel.AnyOpen||Presentation.WorldMapPanel.AnyOpen)return;
            string prompt=AtChest?"[F] Abrir cofre personal":null;
            if(prompt==null)foreach(var loot in inventory.PendingLoot)
                if(Vector3.Distance(transform.position,new Vector3(loot.x,loot.y,loot.z))<=3){prompt="[F] Recoger botín";break;}
            if(prompt!=null)GUI.Box(new Rect(Screen.width/2-170,Screen.height-130,340,40),prompt,new GUIStyle(GUI.skin.box){font=Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Body,fontSize=20});
        }
        void OnDestroy()
        {
            if(chest!=null)Destroy(chest);if(wood!=null)Destroy(wood);if(metal!=null)Destroy(metal);
            foreach(var marker in lootMarkers.Values)if(marker!=null)Destroy(marker);
        }
    }
}
