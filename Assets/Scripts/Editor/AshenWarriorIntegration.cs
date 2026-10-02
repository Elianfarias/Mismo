using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authoring-time skin selection. Keeps the Player Animator and gameplay root stable.</summary>
public static class AshenWarriorIntegration
{
    public const string Model = "Assets/Art/FBX/Characters/AshenWarrior.fbx";
    public const string Player = "Assets/Art/Prefabs/Player/Player.prefab";
    public const string Warrior = "Assets/Art/Prefabs/Player/Skins/WarriorSkin.prefab";
    public const string Mage = "Assets/Art/Prefabs/Player/Skins/MageSkin.prefab";
    public const string Out = "output/ashen-warrior";
    const string Materials = "Assets/Art/Materials/Characters/AshenWarrior";
    static readonly Dictionary<string,string> Human = new Dictionary<string,string>
    {
        {"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"Neck","Neck"},{"Head","Head"},
        {"LeftShoulder","Clavicle.L"},{"LeftUpperArm","UpperArm.L"},{"LeftLowerArm","LowerArm.L"},{"LeftHand","Hand.L"},
        {"RightShoulder","Clavicle.R"},{"RightUpperArm","UpperArm.R"},{"RightLowerArm","LowerArm.R"},{"RightHand","Hand.R"},
        {"LeftUpperLeg","UpperLeg.L"},{"LeftLowerLeg","LowerLeg.L"},{"LeftFoot","Foot.L"},{"LeftToes","Toes.L"},
        {"RightUpperLeg","UpperLeg.R"},{"RightLowerLeg","LowerLeg.R"},{"RightFoot","Foot.R"},{"RightToes","Toes.R"}
    };
    static readonly Dictionary<string,string> Palette = new Dictionary<string,string>
    {
        {"Steel","606879"},{"SteelDark","3f4655"},{"Edge","959aa5"},{"Chain","282d35"},{"ChainLight","3c414b"},
        {"Cloth","743039"},{"ClothLight","8d4246"},{"ClothDark","49212c"},{"Leather","473327"},
        {"Crimson","a72b35"},{"CrimsonLight","cb4546"},{"CrimsonDark","661e2a"},
        {"LeatherEdge","765742"},{"Brass","b2905e"},{"Rust","72503b"},{"Sole","23252a"},{"Face","080c18"},{"Eyes","eadbff"}
    };

    [MenuItem("Mismo/Character/Skins/Importar armadura de guerrero")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Salir de Play Mode antes de cambiar la skin.");
        Directory.CreateDirectory(Out);
        if(File.Exists(Out+"/unity-failure.txt"))File.Delete(Out+"/unity-failure.txt");
        if(!File.Exists(Out+"/Player.before.prefab.txt"))File.Copy(Player,Out+"/Player.before.prefab.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(Warrior));
        Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType=ModelImporterAnimationType.Human;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=false; importer.optimizeGameObjects=false; importer.isReadable=true;
        importer.meshCompression=ModelImporterMeshCompression.Off; importer.importBlendShapes=false;
        importer.importCameras=false; importer.importLights=false; importer.importNormals=ModelImporterNormals.Import;
        var desc=importer.humanDescription;
        desc.human=Human.Select(p=>new HumanBone{humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
        desc.upperArmTwist=.5f;desc.lowerArmTwist=.5f;desc.upperLegTwist=.5f;desc.lowerLegTwist=.5f;
        desc.armStretch=.05f;desc.legStretch=.05f;desc.feetSpacing=0;importer.humanDescription=desc;
        importer.SaveAndReimport();
        // Material remaps are explicit; the FBX never depends on a texture search path.
        foreach(var embedded in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
        {
            var parts=embedded.name.Split('_');
            if(parts.Length!=3 || !Palette.TryGetValue(parts[1],out var hex))continue;
            ColorUtility.TryParseHtmlString("#"+hex,out var color);
            int variant=int.Parse(parts[2]);
            if(parts[1]!="Face" && parts[1]!="Eyes")color*=variant==0?.94f:variant==2?1.065f:1;
            color.a=1;
            string path=Materials+"/"+embedded.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetColor("_Color",color);
            bool steel=parts[1].StartsWith("Steel")||parts[1]=="Edge";
            bool metal=steel||parts[1].StartsWith("Chain")||parts[1]=="Brass";
            mat.SetFloat("_Metallic",metal?.5f:0);mat.SetFloat("_Smoothness",steel?.32f:parts[1]=="Brass"?.35f:.12f);
            if(parts[1]=="Eyes"){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.6f);}
            EditorUtility.SetDirty(mat);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded),mat);
        }
        importer.SaveAndReimport();
        var player=PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var animator=player.GetComponent<PlayerAnimationDriver>().Animator;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Mage)==null)
            {
                Require(animator.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.name=="Mage_Eyes_Flush"),"Guardar la skin del mago antes de reemplazarla.");
                var backup=Object.Instantiate(animator.gameObject);
                try{backup.name="MageSkin";backup.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(backup,Mage);}
                finally{Object.DestroyImmediate(backup);}
            }
            var skin=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                skin.name="WarriorSkin";
                var skinAnimator=skin.GetComponent<Animator>();
                Require(skinAnimator!=null && skinAnimator.avatar!=null && skinAnimator.avatar.isHuman && skinAnimator.avatar.isValid,"Avatar del guerrero inválido.");
                skinAnimator.runtimeAnimatorController=animator.runtimeAnimatorController;
                skinAnimator.applyRootMotion=animator.applyRootMotion;
                ConfigureVisual(skinAnimator,player.layer,true);
                PrefabUtility.SaveAsPrefabAsset(skin,Warrior);
            }
            finally{Object.DestroyImmediate(skin);}
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        UseWarrior();AssetDatabase.SaveAssets();
        File.Copy(Model,Out+"/AshenWarrior_TPose.fbx",true);
        Debug.Log("ASHEN_WARRIOR_INTEGRATED");
    }

    [MenuItem("Mismo/Character/Skins/Usar guerrero")]
    public static void UseWarrior()=>UseSkin(Warrior,true);
    [MenuItem("Mismo/Character/Skins/Usar mago")]
    public static void UseMage()=>UseSkin(Mage,false);

    static void UseSkin(string path,bool warrior)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Salir de Play Mode antes de cambiar la skin.");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(source!=null,"Falta la skin: "+path+". Ejecutar Importar armadura de guerrero.");
        var root=PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var animator=root.GetComponent<PlayerAnimationDriver>().Animator;
            var visual=animator.transform;
            var oldChildren=visual.Cast<Transform>().ToArray();
            var oldSet=new HashSet<Transform>(oldChildren.SelectMany(t=>t.GetComponentsInChildren<Transform>(true)));
            if(visual.GetComponent<MageHatMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<MageHatMotion>());
            if(visual.GetComponent<WarriorCapeMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<WarriorCapeMotion>());
            if(visual.GetComponent<WarriorPlumeMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<WarriorPlumeMotion>());
            var replacement=Object.Instantiate(source);
            try
            {
                // Transfer children in authored T pose. The gameplay-facing root and
                // its Animator retain their serialized identity and controller.
                foreach(var child in replacement.transform.Cast<Transform>().ToArray())child.SetParent(visual,false);
                var newBones=visual.GetComponentsInChildren<Transform>(true).Where(t=>!oldSet.Contains(t)).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(component==null || oldSet.Contains(component.transform))continue;
                    var serialized=new SerializedObject(component);var prop=serialized.GetIterator();bool changed=false;
                    while(prop.Next(true))
                    {
                        if(prop.propertyType!=SerializedPropertyType.ObjectReference)continue;
                        var old=prop.objectReferenceValue as Transform;
                        if(old==null || !oldSet.Contains(old))continue;
                        Require(newBones.TryGetValue(old.name,out var value),"No se puede reasignar el hueso "+old.name);
                        prop.objectReferenceValue=value;changed=true;
                    }
                    if(changed)serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                animator.avatar=source.GetComponent<Animator>().avatar;
                foreach(var child in oldChildren)Object.DestroyImmediate(child.gameObject);
                ConfigureVisual(animator,root.layer,warrior);
            }
            finally{Object.DestroyImmediate(replacement);}
            PrefabUtility.SaveAsPrefabAsset(root,Player);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        Debug.Log("Player: skin "+(warrior?"guerrero":"mago")+" activa.");
    }

    static void ConfigureVisual(Animator animator,int layer,bool warrior)
    {
        // Avatar assignment can leave a cached mapping to the removed hierarchy,
        // especially when reapplying the same avatar. Rebuild it before capturing bones.
        animator.Rebind();
        var visual=animator.transform;
        var map=visual.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        if(visual.GetComponent<MageGroundContact>()==null)visual.gameObject.AddComponent<MageGroundContact>();
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        if(warrior)
        {
            var bones=new[]{"L","C","R"}.SelectMany(c=>Enumerable.Range(1,4).Select(i=>map["Cape."+c+"_"+i])).ToArray();
            var tips=Enumerable.Range(0,3).Select(c=>bones[c*4+3].position+bones[c*4+3].up*(c==1?.25318f:.25495f)*visual.lossyScale.y).ToArray();
            (visual.GetComponent<WarriorCapeMotion>()??visual.gameObject.AddComponent<WarriorCapeMotion>()).Configure(animator,bones,tips);
            var plume=Enumerable.Range(1,5).Select(i=>map["Plume_"+i]).ToArray();
            (visual.GetComponent<WarriorPlumeMotion>()??visual.gameObject.AddComponent<WarriorPlumeMotion>()).Configure(animator.GetBoneTransform(HumanBodyBones.Head),plume,plume[4].position+plume[4].up*.220907f*visual.lossyScale.y);
        }
        else
        {
            var bones=Enumerable.Range(1,4).Select(i=>map["Hat_"+i]).ToArray();
            (visual.GetComponent<MageHatMotion>()??visual.gameObject.AddComponent<MageHatMotion>()).Configure(bones,bones[3].position+bones[3].up*.139f*visual.lossyScale.y);
        }
        foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
        foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.updateWhenOffscreen=true;
            var center=r.transform.InverseTransformPoint(visual.TransformPoint(new Vector3(0,1.2f,0)));
            var size=r.transform.InverseTransformVector(visual.TransformVector(new Vector3(2.8f,3.2f,2.8f)));
            r.localBounds=new Bounds(center,new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z)));
        }
    }

    public static void RunBatch()
    {
        try{Apply();AshenWarriorChecks.Run();AshenWarriorChecks.Capture();EditorApplication.Exit(0);}
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/unity-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void ReviewAndBuild()
    {
        try{Apply();AshenWarriorChecks.Run();AshenWarriorChecks.Capture();AshenWarriorChecks.Build();}
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/unity-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
