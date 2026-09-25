using System;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class CombatFeedbackChecks
    {
        static int checks;
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static void Set(object target, string field, object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static void Delete(Object value) { if (value != null) Object.DestroyImmediate(value); }
#if UNITY_EDITOR
        [MenuItem("Mismo/Combate/Verificar feedback espada-goblin")]
        public static void RunBatch()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Ejecutar las comprobaciones fuera de Play Mode.");
            Run(AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset"));
        }
#endif
        public static void Run(CombatFeedbackProfile profile)
        {
            checks = 0;
            Check(profile != null,"Missing serialized sword profile");
            var family = ScriptableObject.CreateInstance<WeaponFamilyDefinition>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var copy = Object.Instantiate(profile);
            var actor = new GameObject("Feedback check receiver");
            var source = new GameObject("Feedback check attacker");
            var poolObject = new GameObject("Feedback check pool");
            var settings = ScriptableObject.CreateInstance<GoblinSettings>();
            try
            {
                family.feedback=profile;weapon.family=family;
                Check(weapon.FeedbackProfile==profile,"Family inheritance");
                weapon.feedbackOverride=copy;copy.impact.scale=profile.impact.scale+1;
                Check(weapon.FeedbackProfile==copy && profile.impact.scale!=copy.impact.scale,"Override isolation");
                weapon.feedbackOverride=null;Check(weapon.FeedbackProfile==profile,"Return to inheritance");
                foreach(CombatCue kind in new[]{CombatCue.Impact,CombatCue.Heavy,CombatCue.PostureBreak,CombatCue.Block,CombatCue.Parry})
                {
                    var cue=profile.Get(kind);Check(cue.prefab!=null && cue.sound!=null,"Missing cue dependency "+kind);
                    Check(cue.prefab.GetComponentsInChildren<ParticleSystem>(true).Length>0,"Missing particles");
                    foreach(var renderer in cue.prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        foreach(var material in renderer.sharedMaterials) Check(material!=null && material.shader!=null && material.shader.name=="Mismo/Combat Impact","Particle material shader");
                }
                var visual=new GameObject("Visual");visual.transform.SetParent(actor.transform);
                var health=actor.AddComponent<Health>();health.ConfigureMaximum(1000);health.Revive();
                var receiver=actor.AddComponent<DamageReceiver>();
                if(!Application.isPlaying)Call(receiver,"Awake");
                var state=actor.GetComponent<CombatState>();if(!Application.isPlaying)Call(state,"Awake");state.ConfigurePosture(70);state.ConfigureBreakRecovery(1.5f);
                var defense=actor.GetComponent<DefenseWindow>();
                int published=0;receiver.Resolved+=(d,r)=>published++;
                Func<float,float,DamageInfo> hit=(amount,posture)=>new DamageInfo(amount,source,Vector3.up,Vector3.back,AttackIdentity.Next(),posture,feedbackProfile:profile);
                var light=hit(10,5);var result=receiver.Resolve(light);
                Check(result.HealthDamage>0 && published==1 && profile.Select(light,result)==CombatCue.Impact,"Confirmed damage impact");
                Check(receiver.Resolve(light).Outcome==HitOutcome.Ignored && published==1,"Duplicate contact publishes nothing");
                defense.OpenDodge(1);defense.Tick(.2f);result=receiver.Resolve(hit(10,5));
                Check(result.Outcome==HitOutcome.Dodge && profile.Select(light,result)==CombatCue.None,"Dodge has no damage cue");defense.CloseDodge();
                defense.OpenDodge(1);result=receiver.Resolve(hit(10,5));
                Check(result.Outcome==HitOutcome.PerfectDodge && profile.Select(light,result)==CombatCue.None,"Perfect dodge has no damage cue");defense.CloseDodge();CombatTimeFeedback.CancelForPause();
                defense.OpenGuard(1);result=receiver.Resolve(hit(10,5));
                Check(result.Outcome==HitOutcome.Block && profile.Select(light,result)==CombatCue.Block,"Block cue");defense.CloseGuard();
                defense.OpenParry(1);defense.Tick(.2f);result=receiver.Resolve(hit(10,5));
                Check(result.Outcome==HitOutcome.Parry && profile.Select(light,result)==CombatCue.Parry,"Parry cue");
                defense.OpenParry(1);result=receiver.Resolve(hit(10,5));
                Check(result.Outcome==HitOutcome.PerfectParry && profile.Select(light,result)==CombatCue.Parry,"Perfect parry cue");CombatTimeFeedback.CancelForPause();
                var invulnerability=actor.AddComponent<Invulnerability>();Set(receiver,"invulnerability",invulnerability);invulnerability.StartWindow(1);
                result=receiver.Resolve(hit(10,5));Check(result.Outcome==HitOutcome.Invulnerable && profile.Select(light,result)==CombatCue.None,"Invulnerable cue rejected");Set(receiver,"invulnerability",null);
                var heavy=hit(24,16);result=receiver.Resolve(heavy);Check(profile.Select(heavy,result)==CombatCue.Heavy,"Heavy cue");CombatTimeFeedback.CancelForPause();
                int breaks=0;state.PostureBroken+=seconds=>breaks++;
                result=receiver.Resolve(hit(10,90));Check(result.PostureBroken && state.Broken && breaks==1 && profile.Select(light,result)==CombatCue.PostureBreak,"Posture break result");CombatTimeFeedback.CancelForPause();
                receiver.Resolve(hit(10,90));Check(breaks==1,"Broken target cannot refresh break");
                state.Tick(2.01f);Check(!state.Broken && state.RecoveringFromBreak,"Break expires into resistance");
                result=receiver.Resolve(hit(10,90));Check(!result.PostureBroken && result.HealthDamage>0 && result.PostureDamage==0,"Resistance permits damage but prevents stun chaining");
                state.Tick(1.51f);receiver.Resolve(hit(10,90));Check(breaks==2,"Posture damage returns after resistance");CombatTimeFeedback.CancelForPause();state.ResetCombat();

                var goblin=actor.AddComponent<GoblinController>();goblin.Configure(settings,null);
                if(!Application.isPlaying){Call(goblin,"Awake");Call(goblin,"OnEnable");}
                Call(goblin,"Enter",GoblinState.Attack,1f);receiver.Resolve(hit(10,5));Check(goblin.State==GoblinState.Attack,"Light hit preserves attack");
                receiver.Resolve(hit(24,16));Check(goblin.State==GoblinState.Attack,"Heavy hit preserves active attack");CombatTimeFeedback.CancelForPause();
                state.ResetCombat();Call(goblin,"Enter",GoblinState.Recovery,.8f);receiver.Resolve(hit(24,16));
                Check(goblin.State==GoblinState.Stagger && (float)goblin.GetType().GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(goblin)>=.8f,"Heavy recovery interruption preserves punish window");CombatTimeFeedback.CancelForPause();
                Call(goblin,"Enter",GoblinState.Recovery,.8f);receiver.Resolve(hit(24,16));Check(goblin.State==GoblinState.Recovery,"Flinch cooldown prevents repeated cancel");CombatTimeFeedback.CancelForPause();
                state.ResetCombat();Call(goblin,"Enter",GoblinState.Attack,1f);receiver.Resolve(hit(10,90));Check(goblin.State==GoblinState.Stagger,"Posture break really cancels attack");CombatTimeFeedback.CancelForPause();
                var reaction=actor.GetComponent<GoblinHitReaction>();
                if(!Application.isPlaying){Call(reaction,"Awake");Call(reaction,"OnEnable");}
                state.ResetCombat();var before=visual.transform.localRotation;receiver.Resolve(hit(10,5));Call(reaction,"LateUpdate");
                Check(Quaternion.Angle(before,visual.transform.localRotation)>1,"Visible body recoil");Call(reaction,"Restore");Check(Quaternion.Angle(before,visual.transform.localRotation)<.001f,"Recoil restores authored transform");

                var pool=poolObject.AddComponent<CombatImpactPool>();pool.Play(profile.impact,Vector3.zero,Vector3.forward);pool.Play(profile.heavy,Vector3.right,Vector3.left);
                Check(pool.ActiveCount==2,"Simultaneous impacts coexist");pool.Tick(4);Check(pool.ActiveCount==0,"Particles and audio bounded lifetime");
                pool.Play(profile.impact,Vector3.one,Vector3.forward);Check(pool.ActiveCount==1 && pool.transform.childCount==2,"Pool reuses inactive voice");
                GameplayPause.Pause();pool.Tick(4);Check(pool.ActiveCount==1 && Time.timeScale==0,"Pause does not expire particles");GameplayPause.Resume();pool.Tick(4);
                for(int i=0;i<32;i++)Check(pool.Play(profile.impact,Vector3.right*i,Vector3.forward),"Pool admits bounded simultaneous voices");
                Check(!pool.Play(profile.impact,Vector3.zero,Vector3.forward) && pool.ActiveCount==32,"Saturation does not cut existing impacts");pool.Tick(4);
                CombatTimeFeedback.HitStop(.035f);Check(Time.timeScale==0,"Heavy contact freezes briefly");CombatTimeFeedback.PerfectDefense();
                var timing=Object.FindAnyObjectByType<CombatTimeFeedback>();Check(!(bool)timing.GetType().GetField("impactOnly",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(timing),"Perfect defense preempts hitstop");
                GameplayPause.Pause();Delete(timing);GameplayPause.Resume();Check(Mathf.Approximately(Time.timeScale,1),"Pause and destroyed coordinator cannot leave time frozen");
                GameplayPause.Pause();CombatTimeFeedback.HitStop(.04f);CombatTimeFeedback.PerfectDefense();Check(Time.timeScale==0,"Feedback cannot steal gameplay pause");GameplayPause.Resume();
                CheckInterruptResistance(goblin, state, receiver, source, settings, hit);
                state.ResetCombat();result=receiver.Resolve(hit(5000,90));
                Check(health.IsDead && goblin.State==GoblinState.Dead && !result.PostureBroken,"Lethal hit cannot replace death with posture stagger");
                Debug.Log("COMBAT_FEEDBACK_PASS: "+checks+" checks");
            }
            finally
            {
                GameplayPause.Resume();CombatTimeFeedback.CancelForPause();Delete(actor);Delete(source);Delete(poolObject);Delete(family);Delete(weapon);Delete(copy);Delete(settings);
                Delete(Object.FindAnyObjectByType<CombatImpactPool>());Delete(Object.FindAnyObjectByType<CombatTimeFeedback>());
            }
        }
        static void CheckInterruptResistance(GoblinController goblin, CombatState state, DamageReceiver receiver,
            GameObject source, GoblinSettings settings, Func<float,float,DamageInfo> hit)
        {
            source.AddComponent<BoxCollider>();
            source.AddComponent<AttackHitbox>();
            state.ResetCombat();
            foreach (var phase in new[] { GoblinState.Telegraph, GoblinState.Attack })
            {
                Set(goblin,"attack",new GoblinAttack());Call(goblin,"Enter",phase,.4f);
                var result=receiver.Resolve(hit(10,1));
                Check(result.HealthDamage>0 && result.PostureDamage>0 && goblin.State==GoblinState.Stagger && goblin.CurrentAttack==null,
                    "Allowed mini-interruption cancels and discards "+phase);
            }
            Check(state.InterruptImmune,"Second interruption activates immunity");
            var action=new GoblinAttack();Set(goblin,"attack",action);Call(goblin,"Enter",GoblinState.Attack,.4f);
            for(int i=0;i<2;i++)
            {
                var result=receiver.Resolve(hit(10,1));
                Check(result.HealthDamage>0 && result.PostureDamage>0 && goblin.State==GoblinState.Attack && goblin.CurrentAttack==action,
                    "Immune hit damages health/posture without cancelling attack");
            }
            state.Tick(1.49f);Check(state.InterruptImmune,"Immunity lasts configured duration");
            state.Tick(.02f);receiver.Resolve(hit(10,1));
            Check(!state.InterruptImmune && goblin.State==GoblinState.Stagger,"Interruption returns after expiry");
            receiver.Resolve(hit(10,1));Check(state.InterruptImmune,"Fresh cycle permits two interruptions");
            Set(goblin,"attack",action);Call(goblin,"Enter",GoblinState.Attack,.4f);
            var broken=receiver.Resolve(hit(10,1000));
            Check(broken.PostureBroken && state.Broken && goblin.State==GoblinState.Stagger && goblin.CurrentAttack==null,
                "Posture break overrides interrupt immunity and cancels attack");
            float stun=(float)typeof(GoblinController).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(goblin);
            receiver.Resolve(hit(10,1));
            Check(stun>=2 && stun==(float)typeof(GoblinController).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(goblin),
                "Mini hits cannot shorten or renew posture-break stun");
            state.ResetCombat();Check(!state.InterruptImmune,"Reset clears interrupt immunity");
            action.CanBeInterrupted=false;
            foreach(var phase in new[]{GoblinState.Telegraph,GoblinState.Attack})
            {
                Set(goblin,"attack",action);Call(goblin,"Enter",phase,.4f);
                var result=receiver.Resolve(hit(10,1));
                Check(result.HealthDamage>0 && result.PostureDamage>0 && goblin.State==phase && !state.InterruptImmune,
                    "Super armor preserves "+phase+" without consuming interrupt budget");
            }
            broken=receiver.Resolve(hit(10,1000));
            Check(broken.PostureBroken && goblin.CurrentAttack==null && goblin.State==GoblinState.Stagger,"Break overrides super armor");
            state.ResetCombat();Set(goblin,"attack",action);Call(goblin,"Enter",GoblinState.Attack,.4f);
            goblin.OnAttackParried(hit(10,1));Check(goblin.CurrentAttack==null,"Parry still overrides super armor");
            state.ResetCombat();settings.maxConsecutiveInterrupts=1;settings.interruptImmunityDuration=.3f;
            Set(goblin,"attack",new GoblinAttack());Call(goblin,"Enter",GoblinState.Attack,.4f);receiver.Resolve(hit(10,1));
            Check(state.InterruptImmune,"Custom interrupt limit is honored");state.Tick(.31f);
            Check(!state.InterruptImmune,"Custom immunity duration is honored");
            settings.maxConsecutiveInterrupts=2;settings.interruptImmunityDuration=1.5f;
        }
    }
}
