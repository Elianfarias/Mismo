using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mismo.Gameplay.Player
{
    /// <summary>Controles explícitos de pruebas disponibles también en la build del juego.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-75)]
    public sealed class PlayerCheats : MonoBehaviour
    {
        private PlayerMotor motor;
        private string notice;
        private float noticeUntil;
        public bool Active { get; private set; }

        private void Awake() => motor = GetComponent<PlayerMotor>();
        private bool MenusOpen => Presentation.GameplayPause.BlocksInput || Presentation.WorldMapPanel.BlocksGameplay ||
            InventoryPanel.AnyOpen || GetComponent<World.GatheringPlayer>()?.BlocksGameplay == true;

        private void Update()
        {
            var health = GetComponent<Mismo.Gameplay.Combat.Health>();
            if (health != null && health.IsDead) { Active = false; if (motor.IsFlying) motor.SetFlight(false); return; }
            if (MenusOpen || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (keys.f8Key.wasPressedThisFrame) SetActive(!Active);
            if (!Active) return;
            if (keys.f9Key.wasPressedThisFrame)
            {
                if (World.CompanionPlayer.IsRiding(gameObject)) Notify("Desmontá antes de activar el vuelo.");
                else motor.SetFlight(!motor.IsFlying);
            }
            if (keys.f10Key.wasPressedThisFrame) GrantWeapons();
        }

        public bool SetActive(bool active)
        {
            var health = GetComponent<Mismo.Gameplay.Combat.Health>();
            if (motor == null || active && (health != null && health.IsDead || World.CompanionPlayer.IsRiding(gameObject)))
            { Notify("Desmontá antes de activar el modo cheat."); return false; }
            Active = active;
            var loadout = GetComponent<EquipmentLoadout>();
            loadout?.Runner?.Cancel(); loadout?.Belt?.Cancel();
            GetComponent<TreeClimbing>()?.Release();
            motor.SetFlight(active);
            if (active) GrantWeapons();
            else Notify("Modo cheat desactivado. Las armas obtenidas permanecen en el inventario.");
            return true;
        }

        private void GrantWeapons()
        {
            var inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TryGrantCheatWeapons())
                Notify("No se pudieron guardar las armas. Reintentá con F10 cuando el inventario esté listo.");
            else Notify(inventory.Notice);
        }
        private void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 8; }
        private void OnDisable() { Active = false; if (motor != null && motor.IsFlying) motor.SetFlight(false); }
        private void OnGUI()
        {
            if (MenusOpen) return;
            float width = Mathf.Min(650, Screen.width - 24);
            if (Active)
                GUI.Box(new Rect((Screen.width - width) / 2, 42, width, 52),
                    "CHEAT · [F8] Salir · [F9] Vuelo " + (motor.IsFlying ? "ON" : "OFF") + " · [F10] Todas las armas\n" +
                    "WASD: mover · Espacio: subir · Ctrl: bajar · Shift: acelerar");
            if (Time.unscaledTime < noticeUntil)
                GUI.Box(new Rect((Screen.width - width) / 2, Active ? 98 : 42, width, 48), notice);
        }
    }
}
