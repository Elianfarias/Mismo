using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Isolated phase-one workshop. Does not grant rewards or modify the world encounter.</summary>
    public sealed class SoulEaterArena : MonoBehaviour
    {
        [SerializeField] SoulEaterPhaseOneController boss;
        [SerializeField] PlayerController player;
        Vector3 spawn;
        Quaternion facing;
        Health playerHealth;
        bool finished;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string smokePath;
        float smokeTime;
        bool smokeJump,smokeCharge,smokeFlame,smoke75,smoke50;
#endif
        public void Configure(SoulEaterPhaseOneController enemy, PlayerController hero) { boss=enemy;player=hero; }
        void Start()
        {
            spawn=player.transform.position;facing=player.transform.rotation;playerHealth=player.GetComponent<Health>();
            boss.Defeated+=Complete;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args=System.Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-soul-smoke-check")smokePath=args[i+1];
            if(smokePath!=null){player.enabled=false;player.GetComponent<Invulnerability>()?.StartWindow(120);}
#endif
        }
        void Update()
        {
            var keys=UnityEngine.InputSystem.Keyboard.current;
            if(keys!=null&&!Mismo.Gameplay.Player.Presentation.GameplayPause.BlocksInput)
            {
                if(keys.f5Key.wasPressedThisFrame)Restart();
                if(keys.f6Key.wasPressedThisFrame)Threshold(.75f);
                if(keys.f7Key.wasPressedThisFrame&&player.GetComponent<PlayerCheats>()?.Active!=true)Threshold(.5f);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(smokePath==null)return;smokeTime+=Time.unscaledDeltaTime;
            smokeJump|=boss.State==SoulEaterState.RetreatJump;smokeCharge|=boss.State==SoulEaterState.Charging;
            smokeFlame|=boss.GetComponent<SoulEaterEffects>().Flame.Emitting;
            if(smokeTime>5&&!smoke75){smoke75=true;Threshold(.75f);}
            if(smokeCharge&&boss.State==SoulEaterState.Hunting&&!smoke50){smoke50=true;Threshold(.5f);}
            if(boss.Phase!=2&&smokeTime<45)return;
            bool passed=boss.Phase==2&&smokeJump&&smokeCharge&&smokeFlame;
            System.IO.File.WriteAllText(smokePath,$"{(passed?"PASS":"FAIL")} standalone arena\nFlame: {smokeFlame}\nJump: {smokeJump}\nCharge: {smokeCharge}\nPhase one complete: {finished}\n");
            smokePath=null;Application.Quit(passed?0:1);
#endif
        }
        void OnDestroy(){if(boss!=null)boss.Defeated-=Complete;}
        void Complete()
        {
            finished=true;

        }
        public void Restart()
        {
            finished=false;boss.GetComponent<Invulnerability>().Cancel();boss.ResetEncounter();
            player.GetComponent<Mismo.Gameplay.Player.Equipment.AbilityRunner>()?.Cancel();
            player.GetComponent<Mismo.Gameplay.Player.Dash.BeltDash>()?.Cancel();
            var controller=player.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;
            player.transform.SetPositionAndRotation(spawn,facing);
            if(controller!=null)controller.enabled=true;
            playerHealth.Revive();player.enabled=true;player.GetComponent<CombatState>()?.ResetCombat();
            player.GetComponent<Invulnerability>()?.Cancel();
        }
        void Threshold(float value)
        {
            if(boss.Health==null||boss.Health.IsDead||finished)return;
            float damage=boss.Health.Current-boss.Health.Maximum*value;
            if(damage>0)boss.Health.ApplyDamage(new DamageInfo(damage,player.gameObject,boss.transform.position,Vector3.forward,postureDamage:0));
        }
        void OnGUI()
        {
            if(boss==null||playerHealth==null||Mismo.Gameplay.Player.Presentation.GameplayPause.InterfaceHidden)return;
            GUILayout.BeginArea(new Rect(Screen.width-400,110,384,164),GUI.skin.box);
            GUILayout.Label("SOUL EATER · DOS FASES (100–0 %)");
            string status=finished?"Soul Eater derrotado":playerHealth.IsDead?"Has caído · reiniciá la prueba":Status();
            GUILayout.Label(status);
            GUILayout.Label("WASD · Click atacar · E parry · C esquivar");
            GUILayout.Label("F5 reinicia · F6 prueba 75 % · F7 prueba 50 %");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Reiniciar"))Restart();
            if(GUILayout.Button("Probar 75 %"))Threshold(.75f);
            if(GUILayout.Button("Probar 50 %"))Threshold(.5f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        string Status()
        {
            switch(boss.State)
            {
                case SoulEaterState.Windup:return boss.Action==SoulEaterAction.Breath?"Está inhalando: buscá un flanco":boss.Action==SoulEaterAction.Tail?"¡Cuidado con la cola!":"Está preparando una mordida";
                case SoulEaterState.Active:return boss.Action==SoulEaterAction.Breath?"Aliento activo: salí del cono":"¡Ataque!";
                case SoulEaterState.SpecialRoar:return "Rugido del 75 %: preparate para la carga";
                case SoulEaterState.RetreatJump:return "Está tomando distancia";
                case SoulEaterState.ChargeWindup:return "Está apuntando la carga";
                case SoulEaterState.Charging:return "¡Carga! Esquivá hacia un costado";
                case SoulEaterState.Recovery:case SoulEaterState.Braking:case SoulEaterState.Staggered:return "Expuesto: aprovechá para atacar";
                case SoulEaterState.PhaseTransition:return "Rugido del 50 %: comienza la segunda fase";
                case SoulEaterState.Ascending:return "¡Levanta vuelo! Mirá la marca del suelo";
                case SoulEaterState.AerialAim:return "Objetivo fijado: salí de la marca";
                case SoulEaterState.Diving:return "¡Picada devastadora!";
                case SoulEaterState.AerialBreath:return "Pasada de fuego: buscá un costado";
                case SoulEaterState.ImpactRecovery:return "Agotado: castigá la cabeza";
                default:return "Buscá sus aperturas y evitá el frente";
            }
        }
    }
}
