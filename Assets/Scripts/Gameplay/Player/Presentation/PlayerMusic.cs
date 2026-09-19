using Mismo.Gameplay.Combat;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public interface IBossMusicThreat
    {
        bool IsFightingPlayer(Transform player);
    }

    [DisallowMultipleComponent]
    public sealed class PlayerMusic : MonoBehaviour
    {
        static readonly HashSet<IBossMusicThreat> bosses = new HashSet<IBossMusicThreat>();
        public static void RegisterBoss(IBossMusicThreat boss) => bosses.Add(boss);
        public static void UnregisterBoss(IBossMusicThreat boss) => bosses.Remove(boss);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBosses() => bosses.Clear();

        bool FightingBoss()
        {
            foreach (var boss in bosses)
                if (boss is Behaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled && boss.IsFightingPlayer(transform)) return true;
            return false;
        }
        Health health;
        AudioClip exploration, combat, current;
        GameSoundCatalog catalog;
        ExplorationTerrain terrain;
        AudioClip zoneMusic, pendingZone;
        float nextZoneQuery, pendingSince;
        bool zoneInitialized;

        public void InitializeWorld(ExplorationTerrain value)
        {
            terrain = value;
            zoneInitialized = false;
            nextZoneQuery = 0;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            catalog = Mismo.Core.ProjectAssets.Load<GameSoundCatalog>(GameSoundCatalog.ResourcePath);
            exploration = Mismo.Core.ProjectAssets.Load<AudioClip>("Audio/Music/Exploration");
            combat = Mismo.Core.ProjectAssets.Load<AudioClip>("Audio/Music/Combat");
        }

        void LateUpdate()
        {
            UpdateZone();
            // Boss engagement owns this cue; ordinary combat keeps the current zone music.
            var next = health != null && health.IsDead ? null :
                FightingBoss() ? (catalog != null ? catalog.combatMusic : combat) :
                zoneMusic;
            if (next == current) return;
            current = next;
            if (current != null) AudioEvents.RaisePlayMusic(current);
            else AudioEvents.RaiseStopMusic();
        }

        void UpdateZone()
        {
            if (Time.unscaledTime < nextZoneQuery) return;
            nextZoneQuery = Time.unscaledTime + .25f;
            AudioClip desired = catalog != null ? catalog.explorationMusic : exploration;
            if (terrain != null && catalog != null)
            {
                var p = transform.position;
                AudioClip regional;
                if (terrain.NearVillage(p.x, p.z, catalog.villageMusicMargin)) regional = catalog.villageMusic;
                else
                {
                    switch (terrain.Biome(p.x, p.z))
                    {
                        case WorldBiome.Forest: regional = catalog.forestMusic; break;
                        case WorldBiome.Highlands: regional = catalog.highlandsMusic; break;
                        default: regional = catalog.meadowMusic; break;
                    }
                }
                if (regional != null) desired = regional;
            }
            if (!zoneInitialized)
            {
                zoneMusic = pendingZone = desired;
                zoneInitialized = true;
                pendingSince = Time.unscaledTime;
            }
            if (pendingZone != desired) { pendingZone = desired; pendingSince = Time.unscaledTime; }
            if (Time.unscaledTime - pendingSince >= (catalog != null ? Mathf.Clamp(catalog.musicZoneDelay, 0, 10) : 2f))
                zoneMusic = pendingZone;
        }

        void OnDisable()
        {
            if (current != null) AudioEvents.RaiseStopMusic();
            current = null;
        }
    }
}
