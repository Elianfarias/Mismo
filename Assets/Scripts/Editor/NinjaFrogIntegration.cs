using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imports the unarmed frog skin without changing gameplay or existing skins.</summary>
public static class NinjaFrogIntegration
{
    public const string Model="Assets/Art/FBX/Characters/NinjaFrog.fbx";
    public const string Skin="Assets/Art/Prefabs/Player/Skins/NinjaFrogSkin.prefab";
    public const string Out="output/ninja-frog";
    const string Materials="Assets/Art/Materials/Characters/NinjaFrog";
    static readonly Dictionary<string,string> Human=new Dictionary<string,string>
    {
        {"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"Neck","Neck"},{"Head","Head"},
        {"LeftShoulder","Clavicle.L"},{"LeftUpperArm","UpperArm.L"},{"LeftLowerArm","LowerArm.L"},{"LeftHand","Hand.L"},
        {"RightShoulder","Clavicle.R"},{"RightUpperArm","UpperArm.R"},{"RightLowerArm","LowerArm.R"},{"RightHand","Hand.R"},
        {"LeftUpperLeg","UpperLeg.L"},{"LeftLowerLeg","LowerLeg.L"},{"LeftFoot","Foot.L"},{"LeftToes","Toes.L"},
        {"RightUpperLeg","UpperLeg.R"},{"RightLowerLeg","LowerLeg.R"},{"RightFoot","Foot.R"},{"RightToes","Toes.R"}
    };
    static readonly Dictionary<string,string> Palette=new Dictionary<string,string>
    {
        {"Skin","839449"},{"SkinLight","9cab60"},{"SkinDark","536a36"},{"Belly","a7b6a2"},{"BellyShade","839784"},
        {"Iris","c89b4a"},{"IrisLight","dfb764"},{"Pupil","14201b"},{"Glint","eee9d9"},
        {"Jacket","aaa999"},{"JacketLight","c2bfad"},{"JacketDark","798071"},
        {"Pants","434c38"},{"PantsLight","596146"},{"PantsDark","333d2f"},
        {"Orange","b96838"},{"OrangeLight","d3844e"},{"OrangeDark","8b492d"},
        {"Sash","8d565b"},{"SashLight","a96a6a"},{"SashDark","683f47"},{"Cord","6c5340"},{"CordLight","9b8063"}
    };

    [MenuItem("Mismo/Character/Skins/Importar rana ninja")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Salir de Play Mode antes de cambiar la skin.");
        Directory.CreateDirectory(Out);Directory.CreateDirectory(Materials);Directory.CreateDirectory(Path.GetDirectoryName(Skin));
        if(File.Exists(Out+"/unity-failure.txt"))File.Delete(Out+"/unity-failure.txt");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Mage)!=null && AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Warrior)!=null,"Conservar primero las skins del mago y guerrero.");
        var importer=AssetImporter.GetAtPath(Model) as ModelImporter;
        Require(importer!=null,"No se encuentra el FBX de la rana.");
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=false;importer.optimizeGameObjects=false;importer.isReadable=true;
        importer.meshCompression=ModelImporterMeshCompression.Off;importer.importBlendShapes=false;
        importer.importCameras=false;importer.importLights=false;importer.importNormals=ModelImporterNormals.Import;
        var desc=importer.humanDescription;
        desc.human=Human.Select(p=>new HumanBone{humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
        desc.upperArmTwist=.5f;desc.lowerArmTwist=.5f;desc.upperLegTwist=.5f;desc.lowerLegTwist=.5f;
        desc.armStretch=.05f;desc.legStretch=.05f;desc.feetSpacing=0;importer.humanDescription=desc;importer.SaveAndReimport();
        foreach(var embedded in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
        {
            var split=embedded.name.Split('_');Require(split.Length==3 && Palette.ContainsKey(split[1]),"Material sin paleta: "+embedded.name);
            ColorUtility.TryParseHtmlString("#"+Palette[split[1]],out var color);int variant=int.Parse(split[2]);
            if(!new[]{"Iris","IrisLight","Pupil","Glint"}.Contains(split[1]))color*=variant==0?.95f:variant==2?1.055f:1;
            color.a=1;string path=Materials+"/"+embedded.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetColor("_Color",color);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",split[1].StartsWith("Skin")?.16f:.08f);
            EditorUtility.SetDirty(mat);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded),mat);
        }
        importer.SaveAndReimport();
        var player=PrefabUtility.LoadPrefabContents(AshenWarriorIntegration.Player);
        try
        {
            var current=player.GetComponent<PlayerAnimationDriver>().Animator;
            var skin=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                skin.name="NinjaFrogSkin";var animator=skin.GetComponent<Animator>();
                Require(animator!=null && animator.avatar!=null && animator.avatar.isHuman && animator.avatar.isValid,"Avatar de la rana inválido.");
                animator.runtimeAnimatorController=current.runtimeAnimatorController;animator.applyRootMotion=current.applyRootMotion;
                AshenWarriorIntegration.ConfigureVisual(animator,player.layer,PlayerSkinKind.NinjaFrog);
                PrefabUtility.SaveAsPrefabAsset(skin,Skin);
            }
            finally{Object.DestroyImmediate(skin);}
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        UseFrog();AssetDatabase.SaveAssets();PlayerSkinCatalogIntegration.Register();File.Copy(Model,Out+"/NinjaFrog_TPose.fbx",true);
        Debug.Log("NINJA_FROG_INTEGRATED");
    }
    [MenuItem("Mismo/Character/Skins/Usar rana ninja")]
    public static void UseFrog()=>AshenWarriorIntegration.UseSkin(Skin,PlayerSkinKind.NinjaFrog);

    public static void RunBatch()
    {
        try{Apply();NinjaFrogChecks.Run();NinjaFrogChecks.Capture();EditorApplication.Exit(0);}
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/unity-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void ReviewAndBuild()
    {
        try{Apply();NinjaFrogChecks.Run();NinjaFrogChecks.Capture();NinjaFrogChecks.Build();}
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/unity-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
