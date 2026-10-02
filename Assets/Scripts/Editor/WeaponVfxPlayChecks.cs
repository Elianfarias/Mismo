using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Real particles and runner in an empty temporary scene. Never opens a player profile.
public static class WeaponVfxPlayChecks
{
    const string Active="Mismo.WeaponVfxPlayChecks";
    const string ScenePath="Assets/Scenes/WeaponVfxValidationTemp.unity";
    const string Report=WeaponVfxArtSetup.Output+"/play-checks.txt";
    [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
    static EquipmentLoadout loadout;
    static GameObject actor,model;
    static WeaponPresentation presentation;
    static int stage;
    static double next;
    static float pausedTime;
    static ParticleSystem pausedParticle;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged-=PlayState;EditorApplication.playModeStateChanged+=PlayState;
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists("Temp/WeaponVfxPlay.request"))
        {File.Delete("Temp/WeaponVfxPlay.request");Run();return;}
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||actor==null||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.18;
        try{Tick();}catch(Exception e){Finish("FAIL\n"+e);}
    }
    [MenuItem("Mismo/Armas/VFX/Verificar efectos reales en Play Mode")]
    public static void Run()
    {
        Directory.CreateDirectory(WeaponVfxArtSetup.Output);
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if(scene.isDirty||string.IsNullOrEmpty(scene.path)){File.WriteAllText(Report,"BLOCKED: hay una escena abierta sin guardar; se conservó intacta.");return;}
        }
        if(File.Exists(ScenePath)){File.WriteAllText(Report,"BLOCKED: existe una escena de validación anterior; no se sobrescribió.");return;}
        SessionState.SetString(Active+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(test,ScenePath);
        SessionState.SetBool(Active,true);File.WriteAllText(Report,"Real Play Mode — serialized weapon VFX\n");EditorApplication.isPlaying=true;
    }
    static void PlayState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Active,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                actor=new GameObject("VFX validation actor");actor.AddComponent<Health>();actor.AddComponent<PlayerMotor>();
                loadout=actor.AddComponent<EquipmentLoadout>();
                var weapons=ScriptableObject.CreateInstance<WeaponSetDefinition>();
                weapons.primary=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Bow/Bow.asset");
                weapons.secondary=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                typeof(EquipmentLoadout).GetField("startingWeapons",Private).SetValue(loadout,weapons);loadout.Initialize();
                presentation=actor.GetComponent<WeaponPresentation>();presentation.enabled=false;Model();
                stage=0;next=EditorApplication.timeSinceStartup+.3;
            }
            catch(Exception e){Finish("FAIL setup\n"+e);}
        }
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active,false);
            var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active+".Scenes",""));
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);AssetDatabase.DeleteAsset(ScenePath);SessionState.EraseString(Active+".Scenes");
        }
    }
    static void Model()
    {
        if(model!=null){model.SetActive(false);Object.Destroy(model);}
        model=Object.Instantiate(loadout.ActiveDefinition.visualPrefab);
        typeof(WeaponPresentation).GetField("activeVisual",Private).SetValue(presentation,model);
    }
    static ParticleSystem[] Particles()=>model.GetComponentsInChildren<ParticleSystem>().Concat(actor.GetComponentsInChildren<ParticleSystem>()).Distinct().ToArray();
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    static void Tick()
    {
        var runner=loadout.Runner;
        switch(stage++)
        {
            case 0: Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,held:true),"Real bow charge starts");return;
            case 1:
                runner.Tick(.5f);Check(Particles().Length==3&&Particles().Sum(p=>p.particleCount)>0,"Authored BowCharge emits above the character");
                Check(Particles().All(p=>p.transform.position.y>actor.GetComponent<CharacterController>().bounds.max.y),"Bow charge stays above the character collider");
                pausedParticle=Particles()[0];GameplayPause.Pause();return;
            case 2: pausedTime=pausedParticle.time;Check(pausedParticle.isPaused,"Gameplay pause freezes authored charge effect");return;
            case 3: Check(Mathf.Abs(pausedParticle.time-pausedTime)<.001f,"Particle clock stays frozen across real frames");GameplayPause.Resume();runner.SetHeld(false);runner.Tick(.01f);return;
            case 4:
                Check(Particles().Length==3&&Particles().Any(p=>p.transform.parent.name.Contains("BowRelease")),"Releasing replaces charge with the authored release burst");runner.Tick(1);next=EditorApplication.timeSinceStartup+.6;return;
            case 5:
                Check(Particles().Length==0,"Release burst leaves no live particles after its lifetime");Check(loadout.TrySwap(),"Swap to real sword definition");Model();return;
            case 6: Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Real Estocada starts");runner.Tick(.06f);return;
            case 7:
                Check(Particles().Length==3&&Particles().Sum(p=>p.particleCount)>0,"Authored amber blade release emits at the sword socket");runner.Cancel();Check(Particles().Length==0,"Cancel removes the real blade effect immediately");
                Check(runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero),"Real Parry starts");runner.Tick(.02f);return;
            case 8:
                Check(Particles().Length==3&&Particles().Sum(p=>p.particleCount)>0,"Authored ParryHalo is active during defense");
                actor.GetComponent<Health>().ApplyDamage(new DamageInfo(10000,actor,Vector3.zero,Vector3.forward));Check(Particles().Length==0,"Death clears real authored effects");Finish("PASS ALL real VFX playback checks");return;
        }
    }
    static void Finish(string message)
    {
        File.AppendAllText(Report,message+"\n");GameplayPause.Resume();EditorApplication.isPlaying=false;
    }
}
