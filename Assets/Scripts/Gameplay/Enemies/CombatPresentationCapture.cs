#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    // Opt-in capture harness for development builds only. Never runs in normal play.
    public sealed class CombatPresentationCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(System.Environment.GetCommandLineArgs().Contains("-presentation-capture"))
                new GameObject("Development capture").AddComponent<CombatPresentationCapture>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(2);
            var player=FindFirstObjectByType<PlayerController>();player.enabled=false;
            foreach(var goblin in FindObjectsByType<GoblinController>(FindObjectsSortMode.None))goblin.enabled=false;
            player.GetComponent<PlayerMotor>().ResetPosition(new Vector3(-30,6,-34));
            player.GetComponent<Health>().ApplyDamage(new DamageInfo(25,null,Vector3.zero,Vector3.forward));
            player.GetComponent<Stamina>().TrySpend(35);
            yield return new WaitForSecondsRealtime(1);
            yield return new WaitForEndOfFrame();Save("combat-hud.png");
            var combo=player.GetComponentInChildren<BasicSwordCombo>();combo.RequestAttack();
            var hitbox=player.GetComponentInChildren<AttackHitbox>();float timeout=Time.time+1;
            while(!hitbox.IsWindowOpen && Time.time<timeout){combo.Tick(Time.deltaTime);yield return null;}
            Time.timeScale=0;yield return null;yield return new WaitForEndOfFrame();Save("combat-swing.png");
            Debug.Log("PRESENTATION_CAPTURE_OK");Application.Quit();
        }
        private static void Save(string name)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"..",name),image.EncodeToPNG());Destroy(image);
        }
    }
}
#endif
