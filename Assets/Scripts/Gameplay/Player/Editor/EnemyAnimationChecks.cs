using System;
using System.Reflection;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class EnemyAnimationChecks
    {
        private static int count;
        private static void Check(bool ok, string message)
        { if (!ok) throw new Exception(message); count++; Debug.Log("ENEMY_ANIMATION_CHECK " + message); }
        private static bool Near(float a, float b) => Mathf.Abs(a-b)<.01f;
        private static PlayableGraph Graph(EnemyActionPlayback player)
        {
            var inner=typeof(EnemyActionPlayback).GetField("playback",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(player);
            return (PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(inner);
        }
        public static void RunBatch()
        {
            try { Run(); Debug.Log("ENEMY_ANIMATION_PASS " + count); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Run()
        {
            var settings=ScriptableObject.CreateInstance<GoblinSettings>();
            var boss=ScriptableObject.CreateInstance<BossSettings>();
            var clip=new AnimationClip { name="Enemy authored attack" };
            var other=new AnimationClip { name="Enemy second attack" };
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Probe",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,2,1,6));
            AnimationUtility.SetEditorCurve(other,EditorCurveBinding.FloatCurve("Probe",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,10,1,14));
            string clipPath=AssetDatabase.GenerateUniqueAssetPath("Assets/EnemyAnimationCheckClip.anim");
            AssetDatabase.CreateAsset(clip,clipPath);AssetDatabase.SaveAssets();
            var binding=settings.slash.animation;
            binding.clip=clip; binding.activeStartsAt=.25f;binding.recoveryStartsAt=.75f;binding.blendSeconds=0;
            Check(Near(binding.Sample(EnemyAttackPhase.Preparation,.5f),.125f),"Preparation follows configured segment");
            Check(Near(binding.Sample(EnemyAttackPhase.Active,.5f),.5f),"Active follows configured segment");
            Check(Near(binding.Sample(EnemyAttackPhase.Recovery,1f),1f),"Recovery reaches final pose");
            binding.recoveryStartsAt=.1f;
            Check(Near(binding.Sample(EnemyAttackPhase.Active,1),.25f),"Invalid boundaries cannot reverse playback");
            binding.recoveryStartsAt=.75f;
            Check(Near(binding.Sample(EnemyAttackPhase.Preparation,-5),0),"Progress clamps below zero");
            Check(Near(binding.Sample(EnemyAttackPhase.Recovery,5),1),"Progress clamps above one");
            Check(settings.charge.animation.clip==null && boss.frontSlash.animation.clip==null,"Old assets retain optional legacy fallback");
            boss.overheadSmash.animation.clip=other;
            Check(boss.frontSlash.animation.clip==null && boss.straightCharge.animation.clip==null,"Boss attacks have independent bindings");
            string json=EditorJsonUtility.ToJson(settings);
            var restored=ScriptableObject.CreateInstance<GoblinSettings>();EditorJsonUtility.FromJsonOverwrite(json,restored);
            Check(restored.slash.animation.clip==clip && Near(restored.slash.animation.activeStartsAt,.25f),"Settings serialize clip references and phase boundaries");
            var root=new GameObject("Enemy animation fixture");
            var probe=new GameObject("Probe");probe.transform.SetParent(root.transform,false);
            var animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/GoblinConcept/Goblin.controller");
            Check(animator.runtimeAnimatorController!=null,"Existing enemy controller is available");
            var playback=new EnemyActionPlayback();
            try
            {
                playback.Tick(animator,null,EnemyAttackPhase.Preparation,0,3,.2f,0,0);
                Check(playback.ActionClip==null && animator.GetInteger("Motion")==3,"Unconfigured attack uses legacy controller");
                playback.Tick(animator,binding,EnemyAttackPhase.Preparation,.5f,3,0,0,0);
                var graph=Graph(playback);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Evaluate(0);
                Check(Near(probe.transform.localPosition.x,2.5f),"Arbitrary clip evaluates on enemy hierarchy");
                graph.Evaluate(.2f);
                Check(Near(probe.transform.localPosition.x,2.5f),"Render time does not advance authored combat pose");
                playback.Tick(animator,binding,EnemyAttackPhase.Active,.5f,3,0,0,0);graph.Evaluate(0);
                Check(Near(probe.transform.localPosition.x,4f),"Active phase evaluates correct impact pose");
                playback.Tick(animator,binding,EnemyAttackPhase.Recovery,1,3,0,0,0);graph.Evaluate(0);
                Check(Near(probe.transform.localPosition.x,6f),"Recovery ends without rewinding");
                playback.Tick(animator,boss.overheadSmash.animation,EnemyAttackPhase.Active,0,3,0,0,.1f);graph.Evaluate(0);
                Check(playback.ActionClip==other,"Consecutive boss attack selects a different clip");
                playback.Tick(animator,null,EnemyAttackPhase.Recovery,0,4,1,0,0);graph.Evaluate(0);
                Check(playback.ActionClip==null,"Stagger or death releases authored layer immediately");
                Check(Near(settings.slash.windup,.65f) && Near(settings.slash.damage,12f),"Presentation does not mutate attack rules");
                playback.Dispose();Check(!graph.IsValid(),"Disable cleanup destroys graph");
                playback.Tick(animator,binding,EnemyAttackPhase.Active,0,3,0,0,0);
                Check(playback.ActionClip==clip && Graph(playback).IsValid(),"Reenable recreates playback");
                animator.runtimeAnimatorController=null;
                playback.Tick(animator,binding,EnemyAttackPhase.Active,0,3,0,0,0);
                Check(playback.ActionClip==null,"Missing controller safely releases playback");
            }
            finally
            {
                playback.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(settings);
                Object.DestroyImmediate(restored);Object.DestroyImmediate(boss);AssetDatabase.DeleteAsset(clipPath);Object.DestroyImmediate(other);
            }
        }
    }
}
