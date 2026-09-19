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
            if(materialLoot==null&&GetComponent<GoblinController>()!=null)materialLoot=Mismo.Core.ProjectAssets.Load<Mismo.Gameplay.Player.World.GatheringSettings>("GatheringSettings")?.monsterLoot;
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
            Observe();
            var rules=player.Rules;bool boss=GetComponent<BossController>()!=null||GetComponent<DragonBossController>()!=null||GetComponent<GoblinController>()?.Settings?.isBoss==true;
            experience=boss?rules.bossExperience:rules.enemyExperience;
            int masteryPool=boss?rules.bossMasteryExperience:rules.enemyMasteryExperience;
            float total=0;foreach(float contribution in contributions.Values)total+=contribution;
            mastery=new Dictionary<string,int>();
            foreach(var pair in contributions)mastery[pair.Key]=Mathf.FloorToInt(masteryPool*pair.Value/Mathf.Max(1,total));
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
            if(claimed&&health!=null&&!health.IsDead){claimed=false;participant=null;contributions.Clear();}
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
