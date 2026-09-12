using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>HUD de prueba del encuentro; se puede retirar cuando exista UI definitiva.</summary>
    public sealed class BossArenaHUD : MonoBehaviour
    {
        private BossController boss;
        private BossEncounter encounter;

        private void Start()
        {
            boss = FindAnyObjectByType<BossController>();
            encounter = FindAnyObjectByType<BossEncounter>();
        }

        private void OnGUI()
        {
            if(Mismo.Gameplay.Player.Presentation.PlayerHUD.Active!=null)return;
            GUILayout.BeginArea(new Rect(16f, 16f, 470f, 190f), GUI.skin.box);
            GUILayout.Label("FIRST BOSS | Arena de combate");
            GUILayout.Label("WASD mover · Mouse cámara · C dash · E parry");
            GUILayout.Label("Click combo · Q estocada · R giro · Shift correr");
            if (boss != null)
            {
                GUILayout.Label($"Vida: {boss.Health.Current:0} / {boss.Health.Maximum:0}");
                string pattern = boss.CurrentPattern != null ? boss.CurrentPattern.id.ToString() : "-";
                string attack = boss.CurrentAttack != null ? boss.CurrentAttack.label : "-";
                GUILayout.Label($"Estado: {boss.State} | Patrón: {pattern} | Ataque: {attack}");
            }
            if (encounter != null && encounter.IsCompleted) GUILayout.Label(encounter.DefeatMessage);
            GUILayout.EndArea();
        }
    }
}
