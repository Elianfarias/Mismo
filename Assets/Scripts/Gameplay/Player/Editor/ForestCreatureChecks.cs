using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class ForestCreatureChecks
    {
        const string Pending="Mismo.ForestCreatureChecks";
        static IEnumerator routine;
        static int frame=-1,count;
        static double deadline;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ForestCreaturesTest.unity");
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Resume()
        {if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool ok,string message)
        {SessionState.SetBool(Pending,false);EditorApplication.update-=Step;Directory.CreateDirectory("Docs/Validation");File.WriteAllText("Docs/Validation/ForestCreaturePlay.txt",(ok?"PASS ":"FAIL ")+message);Debug.Log("FOREST_PLAY_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);}
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("FOREST_CHECK "+message);}
        static IEnumerator Run()
        {
            var enemies=Object.FindObjectsByType<GoblinController>(FindObjectsSortMode.None);Check(enemies.Length==8,"Eight variants in test scene");
            var catalog=Resources.Load<World.WorldContentCatalog>("WorldContentCatalog");
            var forestIds=new System.Collections.Generic.HashSet<string>();bool outsideForest=false,bossInOrdinary=false;
            for(int i=0;i<1000;i++)
            {
                var forest=catalog.Encounter(World.WorldSiteKind.Clearing,World.WorldBiome.Forest,20,10,i);if(forest!=null)forestIds.Add(forest.id);
                outsideForest|=catalog.Encounter(World.WorldSiteKind.Clearing,World.WorldBiome.Meadow,20,10,i)?.id.StartsWith("forest.")==true;
                bossInOrdinary|=forest?.id=="forest.Forest_Golem_Stylized";
            }
            Check(forestIds.Count(id=>id.StartsWith("forest."))==7,"All seven mob variants selectable in forest encounters");
            Check(!outsideForest&&!bossInOrdinary,"Forest mobs stay in forest and golem stays out of ordinary encounters");
            var golemEntry=catalog.encounters.Single(e=>e.id=="forest.Forest_Golem_Stylized");
            Check(golemEntry.site==World.WorldSiteKind.BossArena&&golemEntry.maximumCount==1,"Golem encounter reserves one boss slot");
            foreach(var enemy in enemies)enemy.enabled=false;
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;player.GetComponent<EquipmentLoadout>().Runner.Cancel();
            var life=player.GetComponent<Health>();life.ConfigureMaximum(10000);life.Revive();var motor=player.GetComponent<PlayerMotor>();
            foreach(var enemy in enemies)
            {
                Check(enemy.GetComponent<NavMeshAgent>().isOnNavMesh,enemy.name+" navigable");
                Check(enemy.GetComponentInChildren<Animator>().avatar.isValid,enemy.name+" valid Generic avatar");
            }
            foreach(string species in new[]{"Boar_Standard","Spider_Standard","Forest_Golem_Stylized"})
            {
                var enemy=enemies.Single(e=>e.name==species);var original=enemy.Settings;var home=enemy.transform.position;
                foreach(var attack in original.attacks.Where(a=>a.enabled))
                {
                    var settings=Object.Instantiate(original);settings.attacks=new[]{attack};settings.decisionPause=100;
                    enemy.Configure(settings,enemy.GetComponent<DamageDealer>());enemy.GetComponent<NavMeshAgent>().Warp(home);enemy.transform.rotation=Quaternion.identity;
                    float distance=attack.minimumRange>0?Mathf.Min(attack.range-.5f,attack.minimumRange+1):Mathf.Min(3,attack.range-.4f);
                    motor.ResetPosition(home+new Vector3(0,.05f,distance));player.GetComponent<Invulnerability>()?.Cancel();life.Revive();
                    // Re-enable resets this life and cooldowns; the enemy's normal Update drives the test.
                    enemy.enabled=true;enemy.SetTarget(player.transform);float end=Time.time+3;
                    while(enemy.State!=GoblinState.Telegraph&&Time.time<end)yield return null;
                    Check(enemy.State==GoblinState.Telegraph,attack.label+" enters anticipation");
                    float healthBefore=life.Current;int releases=0;bool observedHeld=false,windupSafe=true;float timeout=Time.time+attack.windup+attack.active+attack.recovery+2;
                    ProjectileInstance projectile=null;
                    while(enemy.State!=GoblinState.Recovery&&Time.time<timeout)
                    {
                        if(enemy.State==GoblinState.Telegraph)windupSafe&=life.Current==healthBefore;
                        observedHeld|=enemy.GetComponentsInChildren<Transform>().Any(t=>t.name=="Held rock");
                        var active=Object.FindFirstObjectByType<ProjectileInstance>();if(active!=null&&active!=projectile){projectile=active;releases++;}
                        yield return null;
                    }
                    Check(enemy.State==GoblinState.Recovery,attack.label+" enters recovery");
                    Check(windupSafe,attack.label+" has no windup damage");
                    if(attack.kind==CreatureAttackKind.Projectile)
                    {
                        Check(observedHeld,"Golem visibly holds rock before release");
                        Check(releases<=1,"Rock released at most once");
                        float wait=Time.time+3;while(projectile!=null&&Time.time<wait)yield return null;
                        Check(life.Current<healthBefore,"Thrown rock applies damage");
                        Check(projectile==null,"Thrown rock cleans up after collision");
                    }
                    else Check(life.Current<healthBefore,attack.label+" applies contact damage");
                    float after=life.Current;float recover=Time.time+.3f;while(Time.time<recover)yield return null;
                    Check(life.Current==after,attack.label+" recovery cannot deal repeated damage");
                    enemy.enabled=false;enemy.Configure(original,enemy.GetComponent<DamageDealer>());Object.Destroy(settings);yield return null;
                }
                enemy.enabled=true;yield return null;
                enemy.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,player.gameObject,enemy.transform.position,Vector3.forward));yield return null;
                Check(enemy.State==GoblinState.Dead&&!enemy.GetComponent<NavMeshAgent>().enabled,species+" death cancels navigation");
                Check(!enemy.GetComponentsInChildren<Collider>().Any(c=>c.enabled),species+" death disables contacts");
                enemy.enabled=false;
            }
            var interruption=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ForestCreatureIntegration.Prefabs+"/Forest_Golem_Stylized.prefab"));
            interruption.transform.position=Vector3.zero;yield return null;
            var caster=interruption.GetComponent<GoblinController>();caster.enabled=false;
            var isolated=Object.Instantiate(caster.Settings);isolated.attacks=isolated.attacks.Where(a=>a.kind==CreatureAttackKind.Projectile).ToArray();
            caster.Configure(isolated,interruption.GetComponent<DamageDealer>());motor.ResetPosition(new Vector3(0,.05f,10));caster.enabled=true;caster.SetTarget(player.transform);
            float deadline=Time.time+5;while(!interruption.GetComponentsInChildren<Transform>().Any(t=>t.name=="Held rock")&&Time.time<deadline)yield return null;
            Check(interruption.GetComponentsInChildren<Transform>().Any(t=>t.name=="Held rock"),"Interrupt test reaches rock carry");
            Quaternion locked=caster.transform.rotation;motor.ResetPosition(new Vector3(6,.05f,10));yield return null;
            Check(Quaternion.Angle(locked,caster.transform.rotation)<.1f,"Attack facing remains locked while player moves");
            interruption.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,player.gameObject,Vector3.zero,Vector3.forward));yield return null;yield return null;
            Check(!interruption.GetComponentsInChildren<Transform>().Any(t=>t.name=="Held rock"),"Death removes carried rock");
            float until=Time.time+3;while(Time.time<until)yield return null;
            Check(Object.FindObjectsByType<ProjectileInstance>(FindObjectsSortMode.None).Length==0,"Interrupted throw never releases a late projectile");
            Object.Destroy(interruption);Object.Destroy(isolated);
            var group=new GameObject("Shared world root");var owner=new GameObject("Projectile owner");owner.transform.SetParent(group.transform);owner.AddComponent<Health>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(group.transform);wall.transform.position=new Vector3(0,3,2);wall.transform.localScale=new Vector3(4,4,.5f);Physics.SyncTransforms();
            var probe=ProjectileInstance.Spawn(owner,new Vector3(0,3,0),Vector3.forward,10,30,20,.2f,null);probe.Step(.1f);
            Check(!probe.enabled,"Enemy projectile collides with terrain sharing its world root");Object.Destroy(group);
        }
    }
}
