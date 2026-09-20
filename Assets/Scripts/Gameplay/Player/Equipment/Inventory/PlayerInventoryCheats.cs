using System;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public sealed partial class PlayerInventory
    {
        /// <summary>Añade una copia de cada arma faltante, sin duplicar posesiones ni recompensas pendientes.</summary>
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
                if (next.weapons.Exists(w => w.definitionId == definition.Id) ||
                    next.pendingLoot.Exists(p => p.HasWeapon && p.weapon.definitionId == definition.Id)) continue;
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
                        next.pendingLoot.Add(new PendingInventoryLoot { id = Guid.NewGuid().ToString("N"), weapon = item,
                            x = dropPosition.x, y = dropPosition.y, z = dropPosition.z });
                        pending++;
                    }
                }
                added++;
            }
            if (added == 0) { Notice = "Ya tenés todas las armas del catálogo (incluye cofre y botín pendiente)."; return true; }
            return Commit(next, "Cheat: " + added + " armas guardadas. [I] Inventario." +
                (stored > 0 ? " " + stored + " en el cofre." : "") +
                (pending > 0 ? " " + pending + " como botín en el suelo." : ""), false);
        }
    }
}
