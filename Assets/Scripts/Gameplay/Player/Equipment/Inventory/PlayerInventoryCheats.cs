using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        /// <summary>Maximiza las maestrías de las armas propias, incluidas sus variantes de dos manos.</summary>
        public bool TryMaxCheatWeaponMasteries()
        {
            if(!IsReady||catalog==null)return false;
            var next=profile.Copy();
            var families=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var item in next.weapons)Include(catalog.Find(item.definitionId));
            foreach(var loot in next.pendingLoot)if(loot.HasWeapon)Include(catalog.Find(loot.weapon.definitionId));
            if(families.Count==0)return false;
            int cap=Mathf.Clamp(Rules.masteryMaxLevel,2,1000);
            foreach(string family in families)
            {
                var mastery=next.progression.GetOrCreate(family);
                mastery.level=Mathf.Max(mastery.level,cap);mastery.experience=0;mastery.damageExperienceRemainder=0;
            }
            // Commit saves atomically and includes any pending combat training.
            return Commit(next,"Cheat: maestría máxima en "+families.Count+" familias de armas. Distribuí los puntos en Armas y elegí habilidades en K.",false);

            void Include(WeaponDefinition weapon)
            {
                if(weapon==null)return;
                families.Add(weapon.MasteryId);
                if(weapon.dualSwordFamily!=null)families.Add(weapon.dualSwordFamily.progressionId);
                if(weapon.dualAxeFamily!=null)families.Add(weapon.dualAxeFamily.progressionId);
                if(weapon.swordShieldFamily!=null)families.Add(weapon.swordShieldFamily.progressionId);
            }
        }

        /// <summary>Añade las copias faltantes de cada arma (dos si puede ir en ambas manos), contando posesiones y recompensas pendientes.</summary>
        public bool TryGrantCheatWeapons()
        {
            if (!IsReady || catalog == null) return false;
            var next = profile.Copy();
            int added = 0, stored = 0, pending = 0;
            Vector3 dropPosition = transform.position;
            // Overflow remains recoverable at ground level even when the command is used in flight.
            var hits = Physics.RaycastAll(transform.position + Vector3.up, Vector3.down, 2000, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f)
                { dropPosition = hit.point; break; }
            foreach (var definition in catalog.weapons)
            {
                // Dual styles need a second copy of the same weapon for the off hand.
                int wanted = definition.dualAxeFamily != null || definition.dualSwordFamily != null ? 2 : 1;
                int owned = next.weapons.FindAll(w => w.definitionId == definition.Id).Count +
                    next.pendingLoot.FindAll(p => p.HasWeapon && p.weapon.definitionId == definition.Id).Count;
                for (; owned < wanted; owned++)
                {
                    var item = new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = definition.Id };
                    var candidate = next.Copy(); candidate.weapons.Add(item);
                    if (candidate.weapons.Count <= 256 && HasGridRoom(candidate, false)) next = candidate;
                    else
                    {
                        item.inChest = true;
                        if (candidate.weapons.Count <= 256 && HasGridRoom(candidate, true)) { next = candidate; stored++; }
                        else
                        {
                            item.inChest = false;
                            if (next.pendingLoot.Count >= 4096) return false;
                            next.pendingLoot.Add(new PendingInventoryLoot { id = Guid.NewGuid().ToString("N"), weapon = item, expiresAt = WorldPlaySeconds + GroundLootLifetimeSeconds,
                                x = dropPosition.x, y = dropPosition.y, z = dropPosition.z });
                            pending++;
                        }
                    }
                    added++;
                }
            }
            if (added == 0) { Notice = "Ya tenés todas las armas del catálogo (incluye cofre y botín pendiente)."; return true; }
            return Commit(next, "Cheat: " + added + " armas guardadas. [I] Inventario." +
                (stored > 0 ? " " + stored + " en el cofre." : "") +
                (pending > 0 ? " " + pending + " como botín en el suelo." : ""), false);
        }
    }
}
