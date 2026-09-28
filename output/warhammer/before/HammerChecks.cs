using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class HammerChecks
    {
        const string Pending="Mismo.HammerChecks";
        static IEnumerator routine;static int frame=-1;static double deadline;
        static readonly List<string> log=new List<string>();
        static readonly Dictionary<string,int> audioCues=new Dictionary<string,int>();
        static void RecordAudio(AudioClip clip,float volume){if(clip!=null&&volume>0)audioCues[clip.name]=AudioCount(clip.name)+1;}
        static int AudioCount(string name)=>audioCues.TryGetValue(name,out var count)?count:0;
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);log.Add("PASS "+message);Debug.Log("HAMMER_CHECK "+message);}
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Contains("hammer-validation"))throw new InvalidOperationException("Use the isolated hammer-validation project.");
            HammerAssets.Create();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(50,1,50);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            Object.DestroyImmediate(player.GetComponent<RegionRespawn>());player.GetComponent<PlayerController>().enabled=false;
            var camera=new GameObject("Hammer test camera").AddComponent<UnityEngine.Camera>();camera.gameObject.AddComponent<AudioListener>();camera.tag="MainCamera";camera.transform.position=new Vector3(3,2.2f,4);camera.transform.LookAt(Vector3.up);
            new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume()
        {
            if(!SessionState.GetBool(Pending,false))return;
            deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||Time.frameCount==frame)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,"All checks passed");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string text)
        {
            AudioEvents.OnPlayAbilitySFX-=RecordAudio;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Directory.CreateDirectory("HammerChecks");
            File.WriteAllText("HammerChecks/playmode.txt",(pass?"PASS":"FAIL")+" "+log.Count+" checks\n"+string.Join("\n",log)+"\n"+text);EditorApplication.Exit(pass?0:1);
        }
        sealed class Memory:IProfileRepository
        {
            string payload;
            public ProfileReadResult Read(Func<string,bool> validate,out string p){p=payload;return p==null?ProfileReadResult.Missing:ProfileReadResult.Loaded;}
            public void Write(string p){payload=p;}
        }
        static Health Target(Vector3 position,string name)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name=name;go.transform.position=position;
            var health=go.AddComponent<Health>();go.AddComponent<DamageReceiver>();
            var child=new GameObject("Second collider");child.transform.SetParent(go.transform,false);child.AddComponent<SphereCollider>().radius=.4f;
            Physics.SyncTransforms();return health;
        }
        static IEnumerator Run()
        {
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var loadout=player.GetComponent<EquipmentLoadout>();loadout.Initialize();
            var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Resources.Load<ItemCatalog>("ItemCatalog"),new Memory());
            var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(HammerAssets.Data+"/Hammer.asset");
            var recipe=Resources.Load<CraftingRecipe>("Recipes/StoneHammer");var settings=Resources.Load<GatheringSettings>("GatheringSettings");
            Check(inventory.IsReady,"Inventory initialized with isolated in-memory save");
            Check(Resources.Load<ItemCatalog>("ItemCatalog").Find("hammer.stone")==weapon,"Hammer registered in persistent item catalog");
            Check(settings.recipes.Contains(recipe)&&recipe.Category==RecipeCategory.Equipment,"Hammer recipe appears in existing workbench Equipment category");
            Check(recipe.ingredients.Length==1&&recipe.ingredients[0].material.id=="MAT-03"&&recipe.ingredients[0].quantity==1,"Exactly Stone x1, no other ingredients");
            var station=new GameObject("Workbench").AddComponent<CraftingStation>();station.recipes=settings.recipes;
            Check(!inventory.CanCraft(recipe,null),"Cannot craft without stone");
            Check(inventory.TryGrantMaterial("MAT-03",1),"Grant one stone");int before=inventory.Count;
            Check(inventory.TryCraft(recipe,null,station),"Fresh character crafts hammer at ordinary workbench without level requirement");
            Check(inventory.Count==before+1&&inventory.MaterialCount("MAT-03")==0,"Craft adds inventory weapon and consumes exactly one stone");
            Check(inventory.TryEquipDefinition(0,weapon),"Crafted hammer equips through existing inventory");
            Check(loadout.ActiveDefinition!=null&&loadout.ActiveDefinition.Id=="hammer.stone","Hammer is active weapon");
            yield return null;yield return null;
            Check(weapon.isTwoHanded&&weapon.visualPrefab!=null&&weapon.poseProfile!=null,"Two-handed visual and pose references present");
            foreach(var transform in weapon.visualPrefab.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)==0,"No missing script on "+transform.name);
            Check(weapon.visualPrefab.GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh!=null),"No missing mesh");
            Check(weapon.visualPrefab.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null)),"Materials and shaders resolve");
            var animator=player.GetComponentInChildren<Animator>();Check(weapon.poseProfile.equipped.Resolve(player.transform,animator)!=null,"Pose resolves actual player hand");
            for(int i=0;i<4;i++)Check(loadout.GetAbility((AbilitySlot)i)==weapon.abilities[i],"Ability slot "+i+" is immediately available");
            Check(weapon.inventoryIcon!=null && AssetDatabase.GetAssetPath(weapon.inventoryIcon).EndsWith("weapon-Hammer.png"),"Dedicated hammer inventory sprite assigned");
            foreach (var ability in weapon.abilities)
            {
                Check(
                    ability != null && QuietFantasyUI.AbilityIcon(ability) != null,
                    "Dedicated UI icon loads for " + (ability != null ? ability.name : "NULL")
                );
            }
            var feedback=player.GetComponent<CombatFeedback>();Check(feedback!=null,"Existing combat feedback available");
            Check(weapon.abilities.All(a=>a.executionSfx!=null||a.actions[0] is HammerImpactAction h&&h.impactSfx!=null),"All four abilities have assigned audio cues");
            Capture(player.transform,"equipped");
            var runner=loadout.Runner;audioCues.Clear();AudioEvents.OnPlayAbilitySFX+=RecordAudio;
            var target=Target(new Vector3(0,1,1.1f),"Front target");
            float multiplier=inventory.DamageMultiplier(loadout.ActiveDefinition);
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Click starts");runner.Tick(.27f);
            Check(feedback.WeaponImpactCount==0,"No hammer hit feedback during basic windup");Check(Mathf.Approximately(target.Current,target.Maximum),"Click does not hit during windup");runner.Tick(.02f);
            Check(Mathf.Approximately(target.Maximum-target.Current,20*multiplier),"Click hits for 20 exactly once despite two colliders");runner.Tick(.11f);
            Check(AudioCount("Hammer-Swing")==1&&AudioCount("Hammer-Impact")==1,"Basic swing and actual impact dispatch separate existing audio events");Check(feedback.WeaponImpactCount==1,"One successful basic hit emits one cue despite duplicate colliders");Check(Mathf.Approximately(target.Maximum-target.Current,20*multiplier),"Click cannot damage every frame");runner.Tick(.4f);
            Check(!runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero)&&runner.Remaining(weapon.abilities[0])>0,"Click cooldown prevents immediate reuse");
            Object.Destroy(target.gameObject);yield return null;
            target=Target(new Vector3(0,1,1.1f),"Heavy target");
            Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Q starts");runner.Tick(.51f);Check(target.Current==target.Maximum,"Q has longer windup");runner.Tick(.02f);
            Check(Mathf.Approximately(target.Maximum-target.Current,35*multiplier),"Q hits for 35");runner.Tick(.2f);Check(runner.IsBusy,"Q holds recovery");runner.Tick(.5f);
            Object.Destroy(target.gameObject);yield return null;
            var near=Target(new Vector3(1.5f,1,1),"Slam side target");var far=Target(new Vector3(5,1,1),"Outside radius");
            Check(runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero),"E starts");runner.Tick(.64f);Check(feedback.GroundImpactCount==0,"No ground burst before E contact");Check(near.Current==near.Maximum,"E does not hit before ground impact");runner.Tick(.02f);
            Check(Mathf.Approximately(near.Maximum-near.Current,30*multiplier),"E deals 30 area damage to side target");Check(far.Current==far.Maximum,"E respects 2.5 metre radius");
            Check(AudioCount("Hammer-Ground")==1,"Ground audio dispatches exactly at the E damage tick");Check(feedback.GroundImpactCount==1,"E damage and ground burst occur in the same runner tick");
            Check(Mathf.Abs(feedback.LastGroundImpact.y)<.01f&&Vector3.Distance(feedback.LastGroundImpact,new Vector3(0,0,1))<.05f,"E burst projected onto floor at forward contact, not player origin");runner.Tick(.6f);
            Object.Destroy(near.gameObject);Object.Destroy(far.gameObject);yield return null;
            var front=Target(new Vector3(0,1,1.5f),"Spin front");var back=Target(new Vector3(0,1,-1.5f),"Spin back");
            Check(runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero),"R starts");runner.Tick(.49f);Check(front.Current==front.Maximum,"R waits for first damage window");runner.Tick(.02f);
            Check(Mathf.Approximately(front.Maximum-front.Current,20*multiplier)&&Mathf.Approximately(back.Maximum-back.Current,20*multiplier),"First turn hits front and back exactly once");
            runner.Tick(.58f);Check(Mathf.Approximately(front.Maximum-front.Current,20*multiplier),"No extra per-frame spin hits");runner.Tick(.02f);
            Check(Mathf.Approximately(front.Maximum-front.Current,40*multiplier)&&Mathf.Approximately(back.Maximum-back.Current,40*multiplier),"Second turn adds exactly one hit");runner.Tick(.6f);
            Check(AudioCount("Hammer-Spin")==1&&AudioCount("Hammer-Impact")==3,"R sends one sweep cue and exactly two hit cues across multiple targets");Check(!runner.IsBusy&&runner.Remaining(weapon.abilities[3])>0,"R ends but retains 4 second cooldown");
            var spin=weapon.family.animations.actions[3].clip;string root="Armature_Humanoid/Root";
            var turns="xyzw".Select(axis=>AnimationUtility.GetEditorCurve(spin,EditorCurveBinding.FloatCurve(root,typeof(Transform),"m_LocalRotation."+axis))).ToArray();
            Quaternion Rotation(float time)=>new Quaternion(turns[0].Evaluate(time),turns[1].Evaluate(time),turns[2].Evaluate(time),turns[3].Evaluate(time));
            float degrees=0;for(int i=1;i<=72;i++)degrees+=Quaternion.Angle(Rotation(.2f+(i-1)/60f),Rotation(.2f+i/60f));
            Check(Mathf.Abs(degrees-720)<.1f,"Spin animation explicitly contains two 360 degree turns: "+degrees);
            Check(Quaternion.Angle(Rotation(0),Rotation(spin.length))<.01f,"Spin finishes in starting direction");
            Object.Destroy(front.gameObject);Object.Destroy(back.gameObject);
            // Large frames cross both windows once; no frame-rate dependent damage.
            target=Target(new Vector3(0,1,1),"Low framerate target");
            var cast=new AbilityExecution(runner,weapon,weapon.abilities[3],Vector3.forward,Vector3.zero);var action=weapon.abilities[3].actions[0];
            action.Begin(cast);action.Tick(cast,1.2f);action.Tick(cast,.5f);action.End(cast);
            Check(Mathf.Approximately(target.Maximum-target.Current,40*multiplier),"Large timestep still produces exactly two hits");
            Object.Destroy(target.gameObject);
            yield return null; // Destroyed targets must leave the physics world before a miss test.
            int impactsBefore=feedback.WeaponImpactCount;
            var miss=new AbilityExecution(runner,weapon,weapon.abilities[0],Vector3.back,Vector3.zero);
            weapon.abilities[0].actions[0].Begin(miss);
            Check(feedback.WeaponImpactCount==impactsBefore,"Missed basic attack emits no hit cue");
            var guarded=Target(new Vector3(0,1,1.1f),"Guarded target");guarded.transform.rotation=Quaternion.Euler(0,180,0);guarded.GetComponent<DefenseWindow>().OpenGuard(2);Physics.SyncTransforms();
            var denied=new AbilityExecution(runner,weapon,weapon.abilities[0],Vector3.forward,Vector3.zero);weapon.abilities[0].actions[0].Begin(denied);
            Check(guarded.Current==guarded.Maximum&&feedback.WeaponImpactCount==impactsBefore,"Blocked basic produces neither damage nor successful-hit feedback");
            Object.Destroy(guarded.gameObject);yield return null;
            float until=Time.time+4.1f;while(Time.time<until)yield return null;
            // Capture the actual playable graph, not only the authored curve values.
            foreach(var slot in new[]{AbilitySlot.Basic,AbilitySlot.Q,AbilitySlot.E,AbilitySlot.R})
            {
                var definition=weapon.GetAbility(slot);Check(runner.TryUse(slot,Vector3.forward,Vector3.zero),slot+" starts for visual playback");
                float[] times=slot==AbilitySlot.R?new[]{.2f,.5f,.8f,1.1f,1.4f}:new[]{definition.preparation*.5f,definition.preparation+.01f,definition.preparation+.10f};
                int capture=0;float elapsed=0;
                while(runner.IsBusy)
                {
                    float dt=Time.deltaTime;runner.Tick(dt);elapsed+=dt;yield return null;
                    if(capture<times.Length&&elapsed>=times[capture]){Capture(player.transform,slot+"-"+capture);capture++;}
                }
            }
            until=Time.time+6.2f;while(Time.time<until)yield return null;
            var sword=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");var bow=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset");
            Check(sword.GetAbility(AbilitySlot.Basic).usesSwordCombo,"Sword retains original combo");Check(bow.GetAbility(AbilitySlot.Basic).actions[0] is ProjectileAction,"Bow retains original projectile action");
            Check(inventory.TryEquipDefinition(0,sword),"Original Sword equips after Hammer");yield return null;
            target=Target(new Vector3(0,1,1),"Sword regression target");
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Original Sword combo starts after Hammer");
            while(runner.IsBusy){runner.Tick(Time.deltaTime);yield return null;}
            Check(target.Current<target.Maximum,"Original Sword hitbox still deals damage");Object.Destroy(target.gameObject);
            until=Time.time+6.2f;while(Time.time<until)yield return null;
            Check(inventory.TryEquipDefinition(0,bow),"Original Bow equips after Hammer");yield return null;
            target=Target(new Vector3(0,1,4),"Bow regression target");
            Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,target.transform.position),"Original Bow shot starts after Hammer");
            while(runner.IsBusy){runner.Tick(Time.deltaTime);yield return null;}
            until=Time.time+.5f;while(Time.time<until)yield return null;
            Check(target.Current<target.Maximum,"Original Bow projectile still deals damage");Object.Destroy(target.gameObject);
            Check(!player.GetComponent<SwordAnimationFeedback>().ImpactVisible,"Hammer trail is not left emitting after switching to Bow");
            Check(AssetDatabase.FindAssets("t:WeaponDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<WeaponDefinition>).All(w=>w!=null),"All weapon definitions load");
            yield return null;
        }
        static void Capture(Transform player,string name)
        {
            Directory.CreateDirectory("HammerChecks");var camera=UnityEngine.Camera.main;
            var presentation=player.GetComponent<WeaponPresentation>();
            if(presentation!=null&&presentation.ActiveVisual!=null)
            {
                Vector3 head=presentation.ActiveVisual.TransformPoint(new Vector3(0,1.06f,0));
                Vector3 hit=player.position+Vector3.up+Vector3.forward*1.05f;
                File.AppendAllText("HammerChecks/head-positions.txt",name+" head="+head.ToString("F3")+" melee center="+hit.ToString("F3")+" distance="+Vector3.Distance(head,hit)+"\n");
                if(name=="R-1")Check(player.GetComponent<SwordAnimationFeedback>().ImpactVisible,"Existing weapon trail emits during spin");
                if(name=="E-1")
                {
                    Check(head.y>=-.04f&&head.y<.28f,"Ground slam evaluated head reaches floor at actual damage phase");
                    Check(Vector3.Distance(Vector3.ProjectOnPlane(head-player.GetComponent<CombatFeedback>().LastGroundImpact,Vector3.up),Vector3.zero)<.18f,"Ground VFX coincides with evaluated hammer head horizontally");
                }
                if(name=="Basic-1")
                {
                    var strike=(RepeatedStrikeAction)player.GetComponent<EquipmentLoadout>().GetAbility(AbilitySlot.Basic).actions[0];
                    Check(Vector3.Distance(head,hit)<=strike.radius,"Basic damage volume includes hammer head at visual impact");
                }
            }
            var rt=new RenderTexture(900,900,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(900,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,900,900),0,0);texture.Apply();File.WriteAllBytes("HammerChecks/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(texture);
        }
    }
}
