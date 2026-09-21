using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
namespace Mismo.Gameplay.Player.Editor
{
    public static class FocusGenerationChecks
    {
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch validation only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool("FocusGenerationChecks", true);
            EditorApplication.EnterPlaymode();
        }
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (!SessionState.GetBool("FocusGenerationChecks", false)) return;
            SessionState.SetBool("FocusGenerationChecks", false);
            try
            {
                var owner = new GameObject("Archer");
                var focus = owner.AddComponent<CombatState>();
                var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                target.transform.position = new Vector3(0, 0, 10);
                target.transform.forward = Vector3.back;
                target.AddComponent<Health>().ConfigureMaximum(1000);
                var receiver = target.AddComponent<DamageReceiver>();
                Physics.SyncTransforms();
                var basic = AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Bow/BowShot.asset");
                var power = AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Bow/BowPower.asset");
                Check(basic.focusGainOnHit == 5 && basic.focusCost == 0, "Basic configuration");
                var action = (ProjectileAction)basic.actions[0];
                Check(action.damage == 8, "Basic damage must remain unchanged");
                for (int i = 0; i < 4; i++)
                {
                    var arrow = ProjectileInstance.Spawn(owner, Vector3.zero, Vector3.forward, action.damage, 28, 22, .09f, null, focusGainOnHit:basic.focusGainOnHit);
                    Check(focus.Focus == i * 5, "Casting alone gives no Focus");
                    arrow.Step(.5f);
                    Check(focus.Focus == (i + 1) * 5, "Frontal projectile hit generates Focus");
                    arrow.Step(.5f);
                    Check(focus.Focus == (i + 1) * 5, "Consumed projectile cannot reward twice");
                }
                Check(PlayerHUD.AbilityFocusProgress(focus.Focus,power.focusCost)==1, "Power shot perimeter is full after four hits");
                Check(PlayerHUD.AbilityFocusProgress(focus.Focus,40)==.5f && PlayerHUD.AbilityFocusProgress(focus.Focus,80)==.25f, "Each ability normalizes against its own cost");
                Check(PlayerHUD.AbilityFocusProgress(0,0)==1, "Free abilities never require Focus");
                Check(power.focusCost == 20 && focus.Spend(power.focusCost) && focus.Focus == 0, "Four hits fund power shot");
                Check(PlayerHUD.AbilityFocusProgress(focus.Focus,power.focusCost)==0, "Spending Focus empties the skill perimeter");
                var miss = ProjectileInstance.Spawn(owner, Vector3.zero, Vector3.left, 8, 28, 22, .09f, null, focusGainOnHit:5);
                miss.Step(1);
                Check(focus.Focus == 0, "Miss gives no Focus");
                var hit = new DamageInfo(8, owner, target.transform.position, Vector3.forward, AttackIdentity.Next(), focusGainOnHit:7);
                receiver.Resolve(hit); receiver.Resolve(hit);
                Check(focus.Focus == 7, "Custom amount and duplicate suppression");
                target.GetComponent<DefenseWindow>().OpenParry(1);
                receiver.Resolve(new DamageInfo(8, owner, target.transform.position, Vector3.forward, AttackIdentity.Next(), focusGainOnHit:7));
                Check(focus.Focus == 7, "Parried hit gives attacker no Focus");
                target.GetComponent<DefenseWindow>().CloseParry();
                focus.Reward(90, "TEST");
                receiver.Resolve(new DamageInfo(8, owner, target.transform.position, Vector3.forward, AttackIdentity.Next(), focusGainOnHit:7));
                Check(focus.Focus == 100, "Focus caps at 100");
                Check(PlayerHUD.AbilityFocusProgress(focus.Focus,power.focusCost)==1, "Surplus Focus never overfills a skill perimeter");
                File.WriteAllText("focus-checks.txt", "PASS: frontal projectiles, no reward on cast/miss/parry, duplicate suppression, configurable gain, four basics fund power shot, cap 100, unchanged basic damage; HUD per-ability costs, free skills, spend updates and capped perimeter.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("focus-checks.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
