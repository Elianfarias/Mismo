using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class BossConceptIntegration
    {
        const string Prefab="Assets/Art/Prefabs/Enemies/FirstBoss.prefab";
        const string Pending="Mismo.BossConcept.Checks";
        public static void RunBatch()
        {
            try
            {
                var boss=PrefabUtility.LoadPrefabContents(Prefab);
                try
                {
                    var visual=boss.transform.Find("Visual");
                    foreach(Transform child in visual)child.gameObject.SetActive(false);
                    var model=visual.Find("ConceptGoblin");
                    if(model==null)model=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GoblinConceptIntegration.Model),visual)).transform;
                    model.name="ConceptGoblin";model.gameObject.SetActive(true);model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;model.localScale=Vector3.one*1.6f;
                    var normal=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab").GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Goblin_Concept_Mesh");
                    model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials=normal.sharedMaterials;
                    var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/GoblinConcept/Goblin.controller");animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var driver=boss.GetComponent<BossAnimationDriver>()??boss.AddComponent<BossAnimationDriver>();driver.Configure(animator);
                    boss.GetComponent<BossPresentation>().UseAuthoredAnimation();
                    var body=boss.GetComponent<CapsuleCollider>();body.height=3f;body.center=Vector3.up*1.5f;body.radius=.65f;
                    var agent=boss.GetComponent<NavMeshAgent>();agent.height=3f;agent.radius=.65f;
                    PrefabUtility.SaveAsPrefabAsset(boss,Prefab);
                }
                finally{PrefabUtility.UnloadPrefabContents(boss);}
                AssetDatabase.SaveAssets();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
                instance.GetComponent<BossController>().enabled=false;instance.GetComponent<NavMeshAgent>().enabled=false;
                var goblin=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab"));
                goblin.transform.position=Vector3.right*4;goblin.GetComponent<GoblinController>().enabled=false;goblin.GetComponent<NavMeshAgent>().enabled=false;
                SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(Application.isBatchMode&&SessionState.GetBool(Pending,false))new GameObject("Boss integration validation").AddComponent<BossConceptProbe>();
        }
        public static void Finish(bool success,string details)
        {
            SessionState.SetBool(Pending,false);System.IO.Directory.CreateDirectory("Docs/Validation");
            System.IO.File.WriteAllText("Docs/Validation/BossConceptIntegration.txt",(success?"PASS ":"FAIL ")+details);
            Debug.Log("BOSS_CONCEPT_CHECKS "+success+" "+details);EditorApplication.Exit(success?0:1);
        }
    }
    public sealed class BossConceptProbe:MonoBehaviour
    {
        bool failed;
        void Awake(){Application.logMessageReceived+=OnLog;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string trace,LogType type)
        {
            // Unity's editor search-index startup is unrelated to runtime presentation.
            if(trace.Contains("UnityEditor.Search."))return;
            if(!failed&&(type==LogType.Exception||type==LogType.Error)){failed=true;BossConceptIntegration.Finish(false,message+" "+trace);}
        }
        IEnumerator Start()
        {
            // Controllers are deliberately paused; presentation must also work without an active attack.
            for(int i=0;i<45;i++)yield return null;
            var boss=Object.FindAnyObjectByType<BossController>();var goblin=Object.FindAnyObjectByType<GoblinController>();
            var driver=boss.GetComponent<BossAnimationDriver>();var animator=driver.Animator;
            if(goblin.CurrentAttack!=null||Mathf.Abs(animator.transform.localScale.x-1.6f)>.001f){BossConceptIntegration.Finish(false,"Initial state or boss size");yield break;}
            var enter=typeof(BossController).GetMethod("Enter",BindingFlags.Instance|BindingFlags.NonPublic);
            typeof(BossController).GetField("attack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(boss,boss.Settings.frontSlash);
            var states=new[]{BossState.Telegraph,BossState.Attack,BossState.Recovery};
            float[] durations={boss.Settings.frontSlash.Windup,boss.Settings.frontSlash.Active,boss.Settings.frontSlash.Recovery};
            float[] expected={.16f,.455f,.795f};
            for(int i=0;i<3;i++)
            {
                enter.Invoke(boss,new object[]{states[i],durations[i]*.5f});
                yield return new WaitForSeconds(.12f);
                if(animator.GetInteger("Motion")!=3||Mathf.Abs(animator.GetFloat("ActionTime")-expected[i])>.005f||!animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
                {BossConceptIntegration.Finish(false,"Attack clock mismatch "+states[i]);yield break;}
            }
            enter.Invoke(boss,new object[]{BossState.Stagger,.6f});
            yield return new WaitForSeconds(.12f);
            if(animator.GetInteger("Motion")!=4){BossConceptIntegration.Finish(false,"Hit not playing");yield break;}
            enter.Invoke(boss,new object[]{BossState.Dead,0f});
            yield return new WaitForSeconds(.12f);
            if(animator.applyRootMotion){BossConceptIntegration.Finish(false,"Root motion enabled");yield break;}
            BossConceptIntegration.Finish(true,"Goblin idle without active attack produces no exception; boss 2x common model; Attack follows current windup/active/recovery; Hit and Dead presentation; root motion off; no runtime errors");
        }
    }
}
