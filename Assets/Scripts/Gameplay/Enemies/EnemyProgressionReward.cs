using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Single-player rewards from effective contribution, once per enemy life.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyProgressionReward : MonoBehaviour, ICombatContribution
    {
        public MaterialLootTable materialLoot;
        Dictionary<string,int> rolledMaterials;
        Health health;
        DamageReceiver receiver;
        PlayerInventory participant;
        readonly Dictionary<string,float> contributions=new Dictionary<string,float>();
        bool pending,claimed;
        int experience;
        Dictionary<string,int> mastery;
        OwnedWeapon drop;
        float retryAt;
        void Awake()
        {
            health=GetComponent<Health>();receiver=GetComponent<DamageReceiver>();
            if(materialLoot==null&&GetComponent<GoblinController>()!=null)materialLoot=Resources.Load<Mismo.Gameplay.Player.World.GatheringSettings>("GatheringSettings")?.monsterLoot;
        }
        void OnEnable(){if(receiver!=null)receiver.Resolved+=OnResolved;}
        void OnDisable(){if(receiver!=null)receiver.Resolved-=OnResolved;}
        public void RecordDefense(PlayerInventory player,string family)
        {
            if(claimed||health==null||health.IsDead||player==null||!player.IsReady||string.IsNullOrEmpty(family))return;
            if(participant!=null&&participant!=player)return;
            participant=player;
            contributions.TryGetValue(family,out float old);contributions[family]=Mathf.Min(health.Maximum,old+5);
        }
        void OnResolved(DamageInfo damage,HitResult result)
        {
            if(claimed||pending||result.Outcome!=HitOutcome.Hit||result.HealthDamage<=0)return;
            var player=damage.Source!=null?damage.Source.GetComponentInParent<PlayerInventory>():null;
            if(player==null||!player.IsReady||participant!=null&&participant!=player)return;
            participant=player;
            if(!string.IsNullOrEmpty(damage.WeaponFamilyId))
            {
                contributions.TryGetValue(damage.WeaponFamilyId,out float old);
                contributions[damage.WeaponFamilyId]=old+result.HealthDamage;
            }
            if(!health.IsDead)return;
            var rules=player.Rules;bool boss=GetComponent<BossController>()!=null||GetComponent<GoblinController>()?.Settings?.isBoss==true;
            experience=boss?rules.bossExperience:rules.enemyExperience;
            int masteryPool=boss?rules.bossMasteryExperience:rules.enemyMasteryExperience;
            float total=0;foreach(float contribution in contributions.Values)total+=contribution;
            mastery=new Dictionary<string,int>();
            foreach(var pair in contributions)mastery[pair.Key]=Mathf.FloorToInt(masteryPool*pair.Value/Mathf.Max(1,total));
            drop=!boss&&Random.value<rules.dropChance?player.RollDrop():null;
            rolledMaterials=materialLoot!=null?materialLoot.Roll():null;
            pending=true;
            var identity=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>();if(identity!=null)identity.PendingReward=true;
            TryCommit();
        }
        void Update()
        {
            if(claimed&&health!=null&&!health.IsDead){claimed=false;participant=null;contributions.Clear();}
            if(pending&&Time.unscaledTime>=retryAt)TryCommit();
        }
        void TryCommit()
        {
            retryAt=Time.unscaledTime+2;
            string worldId=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.Id;
            if(participant!=null&&participant.TryGrantVictory(experience,mastery,drop,worldId,rolledMaterials,transform.position))
            {claimed=true;pending=false;var identity=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>();if(identity!=null)identity.PendingReward=false;}
        }
    }
}
