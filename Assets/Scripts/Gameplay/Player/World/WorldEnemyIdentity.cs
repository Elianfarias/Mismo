using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    // Cross-assembly contract used by existing enemy controllers and reward transactions.
    public sealed class WorldEnemyIdentity : MonoBehaviour
    {
        public string Id {get;private set;}
        public string RegionId {get;private set;}
        public string DisplayName {get;private set;}
        public bool PendingReward {get;set;}
        public int Level=>fixedLevel>0?fixedLevel:inventory==null?1:settings!=null&&settings.UsesFiniteWorld?Mathf.Max(1,inventory.RegionMinimum(RegionId)):inventory.RegionLevel(RegionId);
        public float HealthMultiplier=>1+Mathf.Max(0,Level-1)*(settings!=null?settings.healthPerLevel:.025f);
        public float DamageMultiplier=>1+Mathf.Max(0,Level-1)*(settings!=null?settings.damagePerLevel:.015f);
        PlayerInventory inventory;
        ExplorationWorldSettings settings;
        int appliedLevel;
        int fixedLevel;
        float baseHealth;
        float nextSave;
        public void Configure(string id,string region,string displayName,PlayerInventory player,ExplorationWorldSettings world,int level=0)
        {Id=id;RegionId=region;DisplayName=displayName;inventory=player;settings=world;fixedLevel=Mathf.Max(0,level);}
        void LateUpdate()
        {
            var health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health==null)return;
            if(health.IsDead)
            {
                // Non-player/environment deaths have no contribution reward but still persist.
                if(inventory!=null&&!PendingReward&&!inventory.IsWorldEnemyDefeated(Id)&&Time.unscaledTime>=nextSave)
                {inventory.TryGrantVictory(0,null,null,Id);nextSave=Time.unscaledTime+2;}
                return;
            }
            if(appliedLevel==0){baseHealth=health.Maximum/HealthMultiplier;appliedLevel=Level;return;}
            if(appliedLevel==Level)return;
            float ratio=health.Normalized;health.ConfigureMaximum(baseHealth*HealthMultiplier);
            health.Heal(Mathf.Max(0,health.Maximum*ratio-health.Current));appliedLevel=Level;
        }
    }
}
