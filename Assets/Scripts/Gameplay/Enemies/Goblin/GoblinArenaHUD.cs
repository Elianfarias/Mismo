using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    public sealed class GoblinArenaHUD : MonoBehaviour
    {
        private Health player;
        private GoblinController goblin;
        private void Start()
        {
            var controller = FindFirstObjectByType<PlayerController>();
            player = controller != null ? controller.GetComponent<Health>() : null;
            goblin = FindFirstObjectByType<GoblinController>();
        }
        private void OnGUI()
        {
            if(Mismo.Gameplay.Player.Presentation.PlayerHUD.Active!=null)return;
            GUILayout.BeginArea(new Rect(16, 16, 420, 165), GUI.skin.box);
            GoblinEliteVisual elite = goblin != null ? goblin.GetComponent<GoblinEliteVisual>() : null;
            GUILayout.Label(elite != null ? "GOBLIN ELITE | Arena de combate" : "GOBLIN BASE | Arena de combate");
            GUILayout.Label("WASD mover · Mouse cámara · C dash · E parry");
            GUILayout.Label("Click combo · Q estocada · R giro · Shift correr");
            if (player != null) GUILayout.Label($"Vida: {player.Current:0} / {player.Maximum:0}");
            if (goblin != null) GUILayout.Label("Goblin: " + goblin.State);
            if ((player != null && player.IsDead) || (goblin != null && goblin.State == GoblinState.Dead))
                GUILayout.Label("Salí y volvé a entrar en Play para repetir el encuentro.");
            GUILayout.EndArea();
        }
    }
}
