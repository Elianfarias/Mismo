using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class BuffPresentationChecks
{
    const string Output=BuffPresentationSetup.Output,Key="Mismo.BuffChecks",ScenePath="Assets/Scenes/Validation/BuffValidation.unity";
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    [Serializable] sealed class SavedScenes {public SceneSetup[] scenes;}
    static IEnumerator routine;static int frame;static double deadline;
    static void Check(bool pass,string message,string file="checks.txt")
    {if(!pass)throw new Exception(message);File.AppendAllText(Output+"/"+file,"PASS "+message+"\n");}
    [MenuItem("Mismo/Combate/Buffs/Verificar assets")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");
        var profile=AssetDatabase.LoadAssetAtPath<BuffPresentation>(BuffPresentationSetup.ProfilePath);
        Check(profile!=null&&profile.styles.Length==4,"Four styles saved in the presentation profile");
        Check(profile.material!=null&&!ShaderUtil.ShaderHasError(profile.material.shader),"URP symbol shader compiles");
        Check(profile.styles.All(s=>s.symbol!=null&&s.symbol.vertexCount>0&&s.icon!=null)&&profile.footArc!=null,"All symbols, matching HUD icons and ankle arcs resolve");
        Check(profile.symbolOpacity<=.5f&&profile.footOpacity<=.1f,"Subtle opacity and faint single ankle arc");
        Check(ProjectAssets.Load<BuffPresentation>(BuffPresentation.CatalogKey)==profile,"Runtime catalog resolves the shared profile");
        Check(PlayerSettings.GetPreloadedAssets().Contains(AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath)),"Runtime catalog remains preloaded");
        foreach(int count in new[]{1,4,8})foreach(var anchor in new[]{new Vector2(16,18),new Vector2(-40,-100),new Vector2(1150,860)})
        {
            var vitals=new Rect(anchor.x,anchor.y,380,49);var rect=BuffHud.Layout(vitals,new Vector2(1600,900),count);
            Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=1600&&rect.yMax<=900,"Vitals buff HUD fits viewport: count="+count+", anchor="+anchor);
            if(anchor==new Vector2(16,18))Check(rect.xMin>vitals.xMax&&rect.yMin==vitals.yMin,"Buffs align beside health, independent of skill layout");
        }
        try{ProjectOrganizationChecks.Run();}
        catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());File.AppendAllText(Output+"/checks.txt","Project-wide organization issues recorded separately.\n");}
    }
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        if(SessionState.GetBool(Key,false))deadline=EditorApplication.timeSinceStartup+180;
    }
    [MenuItem("Mismo/Combate/Buffs/Verificar estados en Play Mode")]
    public static void Play()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Se conserva la escena sin guardar.");
        if(File.Exists(ScenePath))throw new IOException("Temporary scene already exists");
        Directory.CreateDirectory(Output);SessionState.SetString(Key+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        ProjectAssetOrganizer.EnsureFolder("Assets/Scenes/Validation");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Success",false);deadline=EditorApplication.timeSinceStartup+180;
        File.WriteAllText(Output+"/play-checks.txt","");EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){Application.runInBackground=true;routine=null;frame=-1;deadline=EditorApplication.timeSinceStartup+180;}
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        SessionState.SetBool(Key,false);var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+".Scenes",""));
        if(saved.scenes.Any(s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Key+".Scenes");
        if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetBool(Key+".Success",false)?0:1);
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Key,false)){ReadRequest();return;}
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!EditorApplication.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
        try{routine??=Routine();if(!routine.MoveNext())Finish(true,"All gameplay and lifecycle checks passed");}
        catch(Exception e){Finish(false,e.ToString());}
    }
    // Allows validation in an already-open editor; no second instance or termination is needed.
    static void ReadRequest()
    {
        const string path="Temp/BuffChecks.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(path))return;
        string command=File.ReadAllText(path).Trim();
        if(command=="play"&&EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode&&command!="state")return;
        File.Delete(path);Directory.CreateDirectory(Output);
        try
        {
            if(command=="state")File.WriteAllText(Output+"/editor-state.txt","playing="+EditorApplication.isPlaying+"\n"+string.Join("\n",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).path+" dirty="+SceneManager.GetSceneAt(i).isDirty)));
            else if(command=="play")Play();else if(command=="build")Build();else Run();
        }
        catch(Exception e){File.WriteAllText(Output+"/request-failed.txt",e.ToString());Debug.LogException(e);}
    }
    static void Finish(bool pass,string message)
    {File.AppendAllText(Output+"/play-checks.txt",(pass?"PASS ":"FAIL ")+message+"\n");SessionState.SetBool(Key+".Success",pass);if(GameplayPause.IsPaused)GameplayPause.Resume();Time.timeScale=1;EditorApplication.ExitPlaymode();}
    static void Assert(bool pass,string message)=>Check(pass,message,"play-checks.txt");
    static AbilityDefinition Passive(WeaponPassive passive)
    {var a=ScriptableObject.CreateInstance<AbilityDefinition>();a.passive=passive;a.displayName=passive.ToString();return a;}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static IEnumerator Routine()
    {
        var actor=new GameObject("Buff checks actor");actor.AddComponent<CapsuleCollider>().center=Vector3.up*.9f;actor.GetComponent<CapsuleCollider>().height=1.8f;actor.GetComponent<CapsuleCollider>().radius=.28f;
        var health=actor.AddComponent<Health>();var loadout=actor.AddComponent<EquipmentLoadout>();loadout.Initialize();
        loadout.Runner.enabled=false;actor.GetComponent<WeaponPresentation>().enabled=false;
        var bow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset"));bow.overrideFamilyAbilities=true;
        var third=AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Skills/ThirdArrow.asset");bow.abilities=new AbilityDefinition[]{null,third,null,null};
        var alternate=ScriptableObject.CreateInstance<WeaponDefinition>();alternate.Configure("buff.checks","Buff checks",.5f);
        alternate.abilities=new[]{null,Passive(WeaponPassive.Rhythm),Passive(WeaponPassive.Buckler),Passive(WeaponPassive.Coverage)};
        Assert(loadout.TryEquip(0,bow)&&loadout.TryEquip(1,alternate),"Isolated weapon loadout created without user saves");
        var feedback=actor.GetComponent<ActorBuffFeedback>();var visual=actor.GetComponent<ActorBuffVisual>();var skills=actor.GetComponent<WeaponSkillEffects>();
        var target=new GameObject("Target");target.transform.position=Vector3.forward;var targetHealth=target.AddComponent<Health>();targetHealth.ConfigureMaximum(1000);targetHealth.Revive();var receiver=target.AddComponent<DamageReceiver>();
        long Hit()
        {
            long id=AttackIdentity.Next();receiver.Resolve(new DamageInfo(10*skills.BasicMultiplier(id,target.transform.position,receiver),actor,target.transform.position,Vector3.forward,id));
            skills.BasicHit(id,receiver);feedback.Refresh();return id;
        }
        feedback.Refresh();Assert(!feedback.Views.Any(v=>v.ready),"Third impact starts at 0/2 with no buff");
        long first=Hit();Assert(feedback.Views.Single().progress==1&&!feedback.Views.Single().ready,"First confirmed hit advances one mark");
        skills.BasicHit(first,receiver);feedback.Refresh();Assert(feedback.Views.Single().progress==1,"Duplicate contact cannot advance readiness");
        Hit();Assert(feedback.Views.Single().ready&&Mathf.Abs(skills.BasicMultiplier(0,target.transform.position,receiver)-1.8f)<.001f,"Second hit readies the real +80% third impact");
        for(int i=0;i<3;i++){skills.BasicMultiplier(AttackIdentity.Next(),Vector3.one*50);yield return null;}
        feedback.Refresh();Assert(feedback.Views.Single().ready,"Misses within the reset window do not consume the next-hit state");
        receiver.GetComponent<DefenseWindow>().OpenDodge(1);long missed=AttackIdentity.Next();receiver.Resolve(new DamageInfo(18,actor,target.transform.position,Vector3.forward,missed));skills.BasicHit(missed,receiver);feedback.Refresh();
        Assert(feedback.Views.Single().ready,"A dodged contact cannot consume or advance the buff");receiver.GetComponent<DefenseWindow>().CloseDodge();
        float before=targetHealth.Current;Hit();Assert(Mathf.Abs(before-targetHealth.Current-18)<.01f&&!feedback.Views.Single().ready,"Third successful hit deals 18 instead of 10 and consumes readiness");
        Assert(third.thirdArrowResetSeconds==5,"ThirdArrow asset defines a five-second reset window");
        Hit();float firstExpiry=(float)typeof(WeaponSkillEffects).GetField("thirdArrowUntil",Private).GetValue(skills);
        skills.BasicMultiplier(AttackIdentity.Next(),Vector3.one*50);
        Assert((float)typeof(WeaponSkillEffects).GetField("thirdArrowUntil",Private).GetValue(skills)==firstExpiry,"A miss does not refresh the expiry clock");
        while(Time.time<=firstExpiry+.02f)yield return null;feedback.Refresh();
        Assert(feedback.Views.Single().progress==0&&!feedback.Views.Single().ready,"Five seconds without a hit empties partial progress");
        Hit();Hit();Set(skills,"thirdArrowUntil",Time.time+.06f);float resetAt=Time.time+.1f;while(Time.time<resetAt)yield return null;
        Assert(skills.BasicMultiplier(0,target.transform.position,receiver)==1,"Damage expires a ready third hit even before a HUD refresh");
        feedback.Refresh();Assert(feedback.Views.Single().progress==0,"Ready indicator also empties on expiry");
        Hit();Assert(feedback.Views.Single().progress==1,"Next hit after timeout starts a new chain");skills.ResetEffects();feedback.Refresh();
        skills.Empower();feedback.Refresh();Assert(feedback.Views.Any(v=>v.label=="PASO LATERAL"&&v.worldVisible)&&Mathf.Abs(skills.BasicMultiplier(0,target.transform.position,receiver)-1.5f)<.001f,"Empowered basic publishes a timed next-hit buff");
        Hit();Assert(!feedback.Views.Any(v=>v.label=="PASO LATERAL"),"A confirmed hit consumes empowerment immediately");
        skills.Empower();Set(skills,"empoweredUntil",Time.time-.1f);feedback.Refresh();Assert(!feedback.Views.Any(v=>v.label=="PASO LATERAL"),"Expired empowerment disappears without requiring another hit");
        skills.TwoTimes();feedback.Refresh();Assert(!feedback.Views.Single(v=>v.label=="DOS TIEMPOS").ready,"Two Times does not advertise extra damage on its slow-only first hit");
        Hit();Assert(feedback.Views.Single(v=>v.label=="DOS TIEMPOS").ready,"Two Times second hit is presented as ready");Hit();Assert(!feedback.Views.Any(v=>v.label=="DOS TIEMPOS"),"Two Times disappears after both charges");
        Assert(loadout.TrySwap(),"Actual weapon swap succeeds");feedback.Refresh();Assert(feedback.Views.Count==0,"Swap clears prior weapon readiness");
        for(int i=0;i<5;i++)Hit();Assert(Mathf.Abs(skills.SpeedBonus-.2f)<.001f&&feedback.Views.Single(v=>v.kind==BuffKind.Speed).progress==5,"Rhythm publishes five stacks and the real 20% attack speed bonus");
        skills.GainBarrier(25,4);feedback.Refresh();Assert(feedback.Views.Any(v=>v.kind==BuffKind.Shield),"Broquel barrier publishes light cyan shield");
        Assert(skills.Absorb(30,Vector3.forward)==5,"Barrier still absorbs exactly 25 damage");feedback.Refresh();Assert(!feedback.Views.Any(v=>v.kind==BuffKind.Shield),"Depleted barrier vanishes immediately");
        skills.GainBarrier(25,4);Set(skills,"barrierUntil",Time.time-.1f);feedback.Refresh();Assert(!feedback.Views.Any(v=>v.kind==BuffKind.Shield),"Expired barrier disappears even when no damage is incoming");
        var combo=ScriptableObject.CreateInstance<AbilityDefinition>();combo.usesSwordCombo=true;
        typeof(AbilityRunner).GetProperty("Current").SetValue(loadout.Runner,new AbilityExecution(loadout.Runner,alternate,combo,Vector3.forward,Vector3.zero));
        feedback.Refresh();Assert(feedback.Views.Any(v=>v.kind==BuffKind.Defense&&v.detail=="SOLO FRONTAL"),"Coverage is blue and explicitly frontal");
        Assert(Mathf.Abs(skills.Absorb(10,Vector3.back)-8)<.001f&&skills.Absorb(10,Vector3.forward)==10,"Coverage still protects only the front");
        loadout.Runner.Cancel();Set(skills,"rhythmUntil",Time.time-.1f);feedback.Refresh();Assert(feedback.Views.Count==0,"Recovery and expiry remove defense and speed indicators");
        var rhythmAbility=alternate.abilities[1];var finisher=Passive(WeaponPassive.Finisher);alternate.abilities[1]=finisher;skills.ResetEffects();
        for(int i=0;i<3;i++)Hit();
        Assert(feedback.Views.Single().ready&&!feedback.Views.Single().worldVisible&&feedback.Views.Single().detail=="MISMO ENEMIGO","Finisher HUD preserves target-specific scope without advertising global damage");
        Assert(Mathf.Abs(skills.BasicMultiplier(0,target.transform.position,receiver)-1.5f)<.001f&&skills.BasicMultiplier(0,target.transform.position,health)==1,"Finisher bonus applies only to the tracked receiver");
        Hit();Assert(feedback.Views.Count==0,"Fourth same-target hit consumes Finisher progress");alternate.abilities[1]=rhythmAbility;Object.Destroy(finisher);skills.ResetEffects();
        var sourceA=new GameObject("Future party source A");var sourceB=new GameObject("Future party source B");
        feedback.SetBuff(sourceA,"damage",BuffKind.Damage,5,5);feedback.SetBuff(sourceA,"damage",BuffKind.Damage,6,6);feedback.SetBuff(sourceB,"damage",BuffKind.Damage,6,6);feedback.Refresh();visual.Tick(.3f);
        int Glyphs(BuffKind kind)=>actor.scene.GetRootGameObjects().Where(g=>g.activeSelf&&g.name.StartsWith("Buff VFX")).SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Count(f=>f.sharedMesh==BuffPresentation.Current.For(kind).symbol&&f.GetComponent<Renderer>().enabled);
        Assert(feedback.Views.Count==2&&visual.ActiveKinds==1&&visual.SymbolCount==4&&Glyphs(BuffKind.Damage)==4,"One kind uses four symbols even with duplicate sources");
        feedback.SetBuff(sourceA,"defense",BuffKind.Defense,30,30);feedback.Refresh();visual.Tick(.3f);
        Assert(visual.SymbolCount==4&&Glyphs(BuffKind.Damage)==2&&Glyphs(BuffKind.Defense)==2,"Two kinds share four slots as 2+2");
        feedback.SetBuff(sourceA,"speed",BuffKind.Speed,30,30);feedback.Refresh();visual.Tick(.3f);
        Assert(visual.SymbolCount==3&&Glyphs(BuffKind.Damage)==1&&Glyphs(BuffKind.Defense)==1&&Glyphs(BuffKind.Speed)==1,"Three kinds use exactly three symbols");
        feedback.RemoveBuff(sourceA,"defense");feedback.RemoveBuff(sourceA,"speed");
        foreach(BuffKind kind in Enum.GetValues(typeof(BuffKind)))feedback.SetBuff(sourceA,kind.ToString(),kind,30,30);
        feedback.Refresh();visual.Tick(.3f);Assert(visual.ActiveKinds==4&&visual.SymbolCount==4&&visual.RendererCount==5&&Enum.GetValues(typeof(BuffKind)).Cast<BuffKind>().All(k=>Glyphs(k)==1),"Four kinds use one symbol each and a single faint arc");
        feedback.RemoveBuff(sourceA,BuffKind.Shield.ToString());feedback.Refresh();visual.Tick(.001f);
        Assert(visual.SymbolCount<=3,"Removing a fourth kind respects the three-symbol budget immediately, including fades");
        feedback.SetBuff(sourceA,BuffKind.Shield.ToString(),BuffKind.Shield,30,30);feedback.Refresh();visual.Tick(.3f);
        float time=visual.AnimationTime,remaining=feedback.Views[0].remaining;GameplayPause.Pause();double until=EditorApplication.timeSinceStartup+.35;
        while(EditorApplication.timeSinceStartup<until)yield return null;
        Assert(Mathf.Abs(time-visual.AnimationTime)<.001f&&Mathf.Abs(remaining-feedback.Views[0].remaining)<.001f,"Pause freezes both animation and buff clocks");GameplayPause.Resume();
        foreach(var root in actor.scene.GetRootGameObjects().Where(g=>g.name.StartsWith("Buff VFX")))Assert(root.GetComponentsInChildren<Collider>().Length==0&&root.GetComponentsInChildren<Light>().Length==0,"Cosmetic roots have no colliders or dynamic lights");
        feedback.RemoveBuff(sourceA,"damage");Object.Destroy(sourceB);yield return null;yield return null;feedback.Refresh();Assert(feedback.Views.Count==4,"Removing one source and destroying another preserves independent effects");
        // Use the real mage visual, without modifying its prefab, palette, rig or materials.
        var mage=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Skins/MageSkin.prefab"),actor.transform);
        var renderers=mage.GetComponentsInChildren<Renderer>();var materials=renderers.Select(r=>r.sharedMaterials).ToArray();
        var animator=mage.GetComponentInChildren<Animator>();if(animator!=null){var idle=animator.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("idle"));if(idle!=null)idle.SampleAnimation(animator.gameObject,.3f);animator.enabled=false;}
        Bounds mageBounds=renderers[0].bounds;foreach(var renderer in renderers)mageBounds.Encapsulate(renderer.bounds);
        mage.transform.localScale*=1.8f/mageBounds.size.y;
        mageBounds=renderers[0].bounds;foreach(var renderer in renderers)mageBounds.Encapsulate(renderer.bounds);
        mage.transform.position-=Vector3.up*mageBounds.min.y;
        var camera=new GameObject("Buff camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();camera.fieldOfView=36;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.14f);
        camera.transform.position=new Vector3(1.4f,1.7f,-2.9f);camera.transform.LookAt(new Vector3(0,.95f,0));
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.transform.rotation=Quaternion.Euler(40,20,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.63f,.65f);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.06f,0);floor.transform.localScale=new Vector3(20,.1f,20);
        var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMaterial.SetColor("_BaseColor",new Color(.20f,.25f,.18f));floor.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        foreach(BuffKind kind in Enum.GetValues(typeof(BuffKind)))feedback.RemoveBuff(sourceA,kind.ToString());feedback.Refresh();visual.Tick(.3f);
        foreach(BuffKind kind in Enum.GetValues(typeof(BuffKind)))
        {
            feedback.SetBuff(sourceA,kind.ToString(),kind,30,30);feedback.Refresh();visual.Tick(.4f);yield return null;
            Capture(camera,kind+"-rear.png");feedback.RemoveBuff(sourceA,kind.ToString());feedback.Refresh();visual.Tick(.4f);
        }
        foreach(BuffKind kind in Enum.GetValues(typeof(BuffKind)))feedback.SetBuff(sourceA,kind.ToString(),kind,30,30);feedback.Refresh();visual.Tick(.4f);yield return null;Capture(camera,"combined-rear.png");
        // Ask the editor's actual Game View to render IMGUI as well; Camera.Render alone cannot include HUD.
        Assert(loadout.TrySwap(),"Swap while party feedback is active succeeds");feedback.Refresh();
        Assert(feedback.Views.Count(v=>v.ready)==4,"Weapon swap preserves all four externally supplied buffs");
        Hit();Hit();Assert(feedback.Views.Any(v=>v.label=="TERCER IMPACTO"&&v.ready),"Real third-hit readiness coexists with timed party feedback");
        var gameViewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if(gameViewType!=null)
        {
            var gameView=EditorWindow.GetWindow(gameViewType);var previousPosition=gameView.position;gameView.position=new Rect(0,0,1280,800);gameView.Repaint();
            actor.AddComponent<PlayerHUD>();
            for(int i=0;i<8;i++)yield return null;
            bool captured=false;
            for(var type=gameViewType;type!=null;type=type.BaseType)
            {
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                    if(field.Name=="m_TargetTexture"&&field.GetValue(gameView) is RenderTexture texture&&texture.width>100)
                    {CaptureTexture(texture,"hud-game-view.png");captured=true;}
            }
            gameView.position=previousPosition;
            Assert(captured,"Actual Game View with PlayerHUD and buff indicators captured");
        }
        skills.ResetEffects();feedback.Refresh();
        Assert(renderers.Select((r,i)=>r.sharedMaterials.SequenceEqual(materials[i])).All(v=>v),"Actual mage materials stay unchanged");
        actor.SetActive(false);Assert(visual.RendererCount==0&&feedback.Views.Count==0,"Disabling the actor clears effects");actor.SetActive(true);
        feedback.SetBuff(sourceA,"expiry",BuffKind.Speed,.03f,.03f);float expiry=Time.time+.08f;while(Time.time<expiry)yield return null;feedback.Refresh();Assert(!feedback.Views.Any(v=>v.ready),"External timed effect expires automatically; passive progress may remain");
        feedback.SetBuff(sourceA,"death",BuffKind.Defense,-1);feedback.Refresh();visual.Tick(.3f);health.ApplyDamage(new DamageInfo(10000,target,actor.transform.position,Vector3.forward));
        Assert(feedback.Views.Count==0&&visual.RendererCount==0,"Death immediately clears permanent and timed feedback");
        Object.Destroy(actor);Object.Destroy(target);Object.Destroy(sourceA);Object.Destroy(bow);foreach(var a in alternate.abilities)if(a!=null)Object.Destroy(a);Object.Destroy(alternate);Object.Destroy(combo);Object.Destroy(groundMaterial);
        yield return null;yield return null;
        Assert(!SceneManager.GetActiveScene().GetRootGameObjects().Any(g=>g.name.StartsWith("Buff VFX")),"No cosmetic roots leak after owner destruction");
        var poisonChecks=PoisonReactions();while(poisonChecks.MoveNext())yield return poisonChecks.Current;
    }
    static IEnumerator PoisonReactions()
    {
        var source=new GameObject("Poison regression source");
        foreach(string path in new[]{"Assets/Art/Prefabs/Enemies/ForestCreatures/Imp.prefab","Assets/Art/Prefabs/Enemies/Goblin.prefab","Assets/Art/Prefabs/Enemies/FirstBoss.prefab"})
        {
            var holder=new GameObject("Isolated poison target");holder.SetActive(false);
            var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),holder.transform);
            foreach(var brain in enemy.GetComponentsInChildren<EnemyController>())brain.enabled=false;
            foreach(var brain in enemy.GetComponentsInChildren<BossController>())brain.enabled=false;
            foreach(var agent in enemy.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())agent.enabled=false;
            holder.SetActive(true);yield return null;
            var health=enemy.GetComponent<Health>();var receiver=enemy.GetComponent<DamageReceiver>();
            health.ConfigureMaximum(1000);health.Revive();
            var listeners=enemy.GetComponents<MonoBehaviour>().Where(c=>c is EnemyEquipment||c is GoblinAnimationDriver||c is BossAnimationDriver).ToArray();
            Assert(listeners.Length>0,"Real enemy exposes its hit animation listeners: "+enemy.name);
            receiver.Resolve(new DamageInfo(1,source,enemy.transform.position,Vector3.forward,AttackIdentity.Next(),postureDamage:0,area:true));
            Assert(listeners.All(c=>(float)c.GetType().GetField("hitAt",Private).GetValue(c)==Time.time),"Ordinary hit still starts animation: "+enemy.name);
            var stamps=listeners.Select(c=>(float)c.GetType().GetField("hitAt",Private).GetValue(c)).ToArray();
            float before=health.Current;int ticks=0;health.Damaged+=d=>{if(d.IsStatusTick)ticks++;};
            CombatAilment.Poison(enemy,source,"Bow",2,2.1f);float until=Time.time+1.15f;while(Time.time<until)yield return null;
            Assert(ticks==1&&Mathf.Abs(before-health.Current-2)<.01f,"Real poison update still deals its tick damage: "+enemy.name);
            Assert(listeners.Select((c,i)=>(float)c.GetType().GetField("hitAt",Private).GetValue(c)==stamps[i]).All(v=>v),"Poison cannot restart any hit animation listener: "+enemy.name);
            Assert(enemy.GetComponent<EnemyEquipment>()?.PlayingHit!=true,"Poison does not select the equipped hit clip: "+enemy.name);
            receiver.Resolve(new DamageInfo(10000,source,enemy.transform.position,Vector3.zero,AttackIdentity.Next(),postureDamage:0,area:true,parryable:false,statusEffect:StatusEffectType.Poison));
            Assert(health.IsDead,"Lethal poison still uses normal death: "+enemy.name);
            Object.Destroy(holder);yield return null;
        }
        Object.Destroy(source);
    }
    static void Capture(Camera camera,string name)
    {
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1000,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var texture=new Texture2D(1000,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1000,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);}
    }
    static void CaptureTexture(RenderTexture source,string name)
    {
        var previous=RenderTexture.active;var converted=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
        try{Graphics.Blit(source,converted,SystemInfo.graphicsUVStartsAtTop?new Vector2(1,-1):Vector2.one,SystemInfo.graphicsUVStartsAtTop?new Vector2(0,1):Vector2.zero);RenderTexture.active=converted;texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(converted);Object.Destroy(texture);}
    }
    [MenuItem("Mismo/Combate/Buffs/Verificar contenido compilado")]
    public static void Build()
    {
        Run();
        Directory.CreateDirectory(Output);Directory.CreateDirectory(".validation/BuffScripts");Directory.CreateDirectory(".validation/BuffContent");
        var scripts=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/BuffScripts");
        if(scripts.assemblies==null||!scripts.assemblies.Any(p=>p.EndsWith("Mismo.Gameplay.Player.dll")))throw new Exception("Player scripts failed");
        var manifest=BuildPipeline.BuildAssetBundles(".validation/BuffContent",new[]{new AssetBundleBuild{assetBundleName="buffs",assetNames=new[]{BuffPresentationSetup.ProfilePath}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new Exception("Buff content build failed");
        var bundle=AssetBundle.LoadFromFile(".validation/BuffContent/buffs");
        try
        {
            var profile=bundle.LoadAsset<BuffPresentation>(BuffPresentationSetup.ProfilePath);
            if(profile==null||profile.styles.Length!=4||profile.styles.Any(s=>s.symbol==null||s.icon==null)||profile.material?.shader==null||profile.footArc==null)throw new Exception("Missing built dependencies");
            File.WriteAllText(Output+"/content-build.txt","PASS Windows runtime compilation and bundle reload: four meshes, four icons, arc and shader material.\n");
        }
        finally{if(bundle!=null)bundle.Unload(true);}
        var errors=new List<string>();Application.LogCallback capture=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=capture;
        try
        {
            var game=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=".validation/BuffGame/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Output+"/game-build.txt",game.summary.result+"\nErrors: "+game.summary.totalErrors+"\n"+string.Join("\n",errors.Distinct()));
            if(game.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Player build failed");
        }
        finally{Application.logMessageReceived-=capture;}
    }
}
