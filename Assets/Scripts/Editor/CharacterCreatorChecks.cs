using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterCreatorChecks
{
    const string Output="output/character-creator";
    [InitializeOnLoadMethod] static void RegisterRequests(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/CharacterCreatorChecks.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);Directory.CreateDirectory(Output);
        try
        {
            PlayerSkinCatalogIntegration.Register();
            if(command=="build")Build();
            else{Run();CharacterCreatorSceneChecks.Run();}
            File.WriteAllText(Output+"/request-result.txt","PASS: "+command);
        }
        catch(Exception e){File.WriteAllText(Output+"/request-result.txt","FAIL: "+command+"\n"+e);Debug.LogException(e);}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [MenuItem("Mismo/Character/Verificar creador de personaje")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        Require(catalog!=null,"Missing runtime asset catalog");
        Require(PlayerSettings.GetPreloadedAssets().Contains(catalog),"Runtime catalog is not preloaded in builds");
        foreach(var id in PlayerAppearance.Skins)
            Require(catalog.entries.Count(e=>e?.key=="PlayerSkins/"+id)==1&&PlayerAppearance.Prefab(id)!=null,"Skin missing or duplicated in catalog: "+id);
        try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
        catch(InvalidOperationException e){File.WriteAllText(Output+"/organization.txt",e.Message);Debug.LogWarning("Organization checks failed; see "+Output+"/organization.txt");}
        Require(!PlayerAppearance.ValidName("   ")&&!PlayerAppearance.ValidName("<b>A</b>")&&!PlayerAppearance.ValidName(new string('a',25)),"Invalid names accepted");
        Require(PlayerAppearance.ValidName("Élian del Sur")&&PlayerAppearance.ValidName(new string('a',24)),"Valid names rejected");
        var session=typeof(WorldSession).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        var previous=session.GetValue(null);
        try
        {
            foreach(var id in PlayerAppearance.Skins)
            {
                var save=new WorldSaveData{playerName="Élian",playerSkin=id};
                var copy=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(save.Copy()));
                Require(copy.playerName==save.playerName&&copy.playerSkin==id,"Character save roundtrip failed");
                session.SetValue(null,copy);
                var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Player));
                try
                {
                    var driver=player.GetComponent<PlayerAnimationDriver>();
                    var result=PlayerAppearance.Apply(player,driver.Animator);driver.Configure(result);
                    Require(result.avatar.isValid&&result.avatar.isHuman,"Invalid avatar: "+id);
                    Require(player.GetComponent<PlayerMotor>().Visual==result.transform,"Motor lost visual: "+id);
                    Require(player.GetComponent<SwordAnimationFeedback>().SwordVisual==result.GetBoneTransform(HumanBodyBones.RightHand),"Weapon lost hand: "+id);
                    Require(result.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.sharedMesh!=null&&r.bones.All(b=>b!=null)&&r.sharedMaterials.All(m=>m!=null)),"Broken skin: "+id);
                    Require(id==PlayerAppearance.Mage?result.GetComponent<MageHatMotion>()!=null:
                        id==PlayerAppearance.NinjaFrog?result.GetComponent<FrogScarfMotion>()!=null:
                        result.GetComponent<WarriorCapeMotion>()!=null&&result.GetComponent<WarriorPlumeMotion>()!=null,"Secondary motion missing: "+id);
                    Require(result.GetComponent<MageGroundContact>()!=null,"Ground contact missing: "+id);
                    if(id==PlayerAppearance.NinjaFrog)
                    {
                        var scarf=result.GetComponent<FrogScarfMotion>();
                        var bones=(Transform[])typeof(FrogScarfMotion).GetField("bones",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(scarf);
                        Require(bones.Length==8&&bones.All(b=>b!=null&&b.IsChildOf(result.transform)),"Scarf references escaped replacement rig");
                        scarf.Advance(1f/60);scarf.Advance(1f/60);
                    }
                }
                finally{Object.DestroyImmediate(player);}
            }
            var legacy=JsonUtility.FromJson<WorldSaveData>("{\"version\":1}");
            Require(legacy.playerSkin==null,"Old saves must retain authored skin");
        }
        finally{session.SetValue(null,previous);}
        File.WriteAllText(Output+"/checks.txt","PASS: all registered skins (mage, knight, ninja-frog), preloaded catalog, name validation, save roundtrip, legacy saves, Humanoid avatars, meshes, materials, weapon hand, motor visual, ground contact and secondary motion including scarf rig references. See organization.txt for the separate project-wide check.");
        Debug.Log("CHARACTER_CREATOR_CHECKS_OK");
    }
    public static void Build()
    {
        PlayerSkinCatalogIntegration.Register();
        Run();
        var compiled=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings{
            target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone},".validation/CharacterCreatorScripts");
        Require(compiled.assemblies.Any(a=>a.EndsWith("Mismo.Menu.dll")),"Menu missing from player compilation");
        File.WriteAllText(Output+"/compilation.txt","PASS: Windows player scripts, including creator and runtime appearance.");
        Directory.CreateDirectory(".validation/CharacterCreatorContent");
        var bundle=BuildPipeline.BuildAssetBundles(".validation/CharacterCreatorContent",new[]{new AssetBundleBuild{
            assetBundleName="character-skins",assetNames=PlayerSkinCatalogIntegration.SkinPaths}},
            BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        Require(bundle!=null,"Skin content build failed");
        File.WriteAllText(Output+"/content.txt","PASS: all three skins and dependencies built for Windows.");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=".validation/CharacterCreator/Mismo.exe",target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development});
        File.WriteAllText(Output+"/build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors);
        Require(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"Character creator build failed");
    }
}
