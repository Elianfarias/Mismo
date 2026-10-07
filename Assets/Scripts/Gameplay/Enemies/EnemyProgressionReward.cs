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
        bool pending,claimed;
        bool recognized;
        Mismo.Gameplay.Player.World.CreatureSpecies species;
        int experience;
        Dictionary<string,int> mastery;
        OwnedWeapon drop;
        float retryAt;
        float observeAt;
        PlayerInventory observer;
        void Awake()
        {
            health=GetComponent<Health>();receiver=GetComponent<DamageReceiver>();
            if(materialLoot==null&&GetComponent<EnemyController>()!=null)materialLoot=Mismo.Core.ProjectAssets.Load<Mismo.Gameplay.Player.World.GatheringSettings>("GatheringSettings")?.monsterLoot;
        }
        void OnEnable(){if(receiver!=null)receiver.Resolved+=OnResolved;}
        void OnDisable(){if(receiver!=null)receiver.Resolved-=OnResolved;}
        public void RecordDefense(PlayerInventory player,string family)
        {
            if(claimed||health==null||health.IsDead||player==null||!player.IsReady||string.IsNullOrEmpty(family))return;
            if(participant!=null&&participant!=player)return;
            participant=player;
            // Defenses can train the skill used, but never manufacture weapon damage EXP.
            var cast=player.GetComponent<AbilityRunner>()?.Current;
            if(cast!=null&&cast.Began&&!cast.Ended)RecordSkillUse(player,cast.WeaponFamilyId,cast.Definition.Id,cast.UseId);
        }
        public void RecordSkillUse(PlayerInventory player,string family,string abilityId,long useId)
        {
            if(player==null||!player.IsReady||GetComponent<TrainingDummy>()!=null)return;
            player.RecordMonsterSkillUse(family,abilityId,useId);
        }
        void OnResolved(DamageInfo damage,HitResult result)
        {
            if(claimed||pending||result.Outcome!=HitOutcome.Hit||result.HealthDamage<=0)return;
            var player=damage.Source!=null?damage.Source.GetComponentInParent<PlayerInventory>():null;
            if(player==null||!player.IsReady||participant!=null&&participant!=player)return;
            participant=player;
            if(GetComponent<TrainingDummy>()==null)player.RecordMonsterDamage(damage.WeaponFamilyId,result.HealthDamage,damage.AbilityId,damage.AbilityUseId);
            if(!health.IsDead)return;
            Observe();
            var rules=player.Rules;bool boss=GetComponent<BossController>()!=null||GetComponent<DragonBossController>()!=null||GetComponent<EnemyController>()?.Settings?.isBoss==true;
            experience=boss?rules.bossExperience:rules.enemyExperience;
            mastery=null; // Weapon EXP is credited from actual health damage, including nonlethal hits.
            drop=!boss&&Random.value<rules.dropChance?player.RollDrop():null;
            rolledMaterials=materialLoot!=null?materialLoot.Roll():null;
            species=Mismo.Gameplay.Player.World.CreatureSpecies.For(gameObject);
            recognized=species!=null&&species.domesticable&&species.mountable&&!player.OwnsMount(species.id,gameObject.name.Replace("(Clone)","").Trim())&&Random.value<species.recognitionChance;
            pending=true;
            var identity=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>();if(identity!=null)identity.PendingReward=true;
            TryCommit();
        }
        void Update()
        {
            if(Time.unscaledTime>=observeAt){observeAt=Time.unscaledTime+.75f;Observe();}
            if(claimed&&health!=null&&!health.IsDead){claimed=false;participant=null;}
            if(pending&&Time.unscaledTime>=retryAt)TryCommit();
        }
        void Observe()
        {
            if(health==null)return;
            if(species==null)species=Mismo.Gameplay.Player.World.CreatureSpecies.For(gameObject);
            if(species==null)return;if(observer==null)observer=FindFirstObjectByType<PlayerInventory>();
            if(observer==null||!observer.IsReady||observer.HasSeenSpecies(species.id))return;
            if(!Mismo.Gameplay.Player.World.CreatureSpecies.Visible(transform,observer.transform))return;
            observer.DiscoverSpecies(species.id);
        }
        void TryCommit()
        {
            retryAt=Time.unscaledTime+2;
            string worldId=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>()?.Id;
            if(participant!=null&&participant.TryGrantVictory(experience,mastery,drop,worldId,rolledMaterials,transform.position,species?.id,recognized,gameObject.name.Replace("(Clone)","").Trim()))
            {claimed=true;pending=false;var identity=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>();if(identity!=null)identity.PendingReward=false;}
        }
    }
}
