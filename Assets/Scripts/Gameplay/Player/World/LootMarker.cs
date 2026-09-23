using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mismo.Gameplay.Player.World
{
    /// <summary>Visual only. InventoryWorldAccess owns pickup, persistence and lifetime.</summary>
    public sealed class LootMarker : MonoBehaviour
    {
        Transform glow;
        UnityEngine.Camera view;
        public static int Kind(PlayerInventory inventory, PendingInventoryLoot loot)
        {
            if(loot.HasWeapon)return 0;
            foreach(var stack in loot.materials)
            {
                var material=inventory.Material(stack.id);
                if(material!=null&&(material.IsConsumable||material.category==InventoryItemCategory.Consumable))return 2;
            }
            return 1;
        }
        public static Color ColorFor(InventorySettings settings, int kind, int tier)
        {
            if(kind==1)return settings.componentLootColor;
            if(kind==2)return settings.consumableLootColor;
            var colors=settings.weaponLootColors;
            return colors!=null&&colors.Length>0?colors[Mathf.Clamp(tier-1,0,colors.Length-1)]:Color.white;
        }
        public void Configure(InventorySettings settings,int kind,int tier)
        {
            if(settings.lootGlowMaterial==null)return;
            var tint=ColorFor(settings,kind,tier);
            float beam=kind==0?Mathf.Lerp(.2f,1,Mathf.Clamp01((tier-1)/4f)):0;
            float seed=Mathf.Repeat(transform.position.x*.73f+transform.position.z*1.17f,20);
            glow=Part("Loot glow",settings.lootGlowMaterial,tint,kind,beam,seed,false);
            glow.localPosition=Vector3.up*.8f;glow.localScale=new Vector3(.95f,1.65f,1);
            var ring=Part("Ground halo",settings.lootGlowMaterial,tint,kind,beam,seed,true);
            ring.localPosition=Vector3.up*.035f;ring.localRotation=Quaternion.Euler(90,0,0);ring.localScale=Vector3.one*.8f;
        }
        Transform Part(string name,Material material,Color tint,int kind,float beam,float seed,bool ground)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Quad);part.name=name;part.transform.SetParent(transform,false);
            var collider=part.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var renderer=part.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var properties=new MaterialPropertyBlock();properties.SetColor("_Tint",tint);
            properties.SetFloat("_Kind",kind);properties.SetFloat("_Beam",beam);properties.SetFloat("_Seed",seed);properties.SetFloat("_Ground",ground?1:0);
            renderer.SetPropertyBlock(properties);return part.transform;
        }
        void LateUpdate()
        {
            if(view==null||!view.isActiveAndEnabled)view=UnityEngine.Camera.main;
            if(view!=null&&glow!=null)glow.rotation=view.transform.rotation;
        }
    }
}
