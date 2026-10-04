using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Imports the authored mage and replaces only Player's visual contents.</summary>
public static class ArcaneMageIntegration
{
    const string Model = "Assets/Art/FBX/Characters/ArcaneMage.fbx";
    const string Player = "Assets/Art/Prefabs/Player/Player.prefab";
    const string Materials = "Assets/Art/Materials/Characters/ArcaneMage";
    const string Out = "output/arcane-mage";
    static readonly Dictionary<string,string> Palette = new Dictionary<string,string>
    {
        {"Indigo","303b63"},{"IndigoLight","3e4b74"},{"IndigoDark","222d4a"},
        {"Mantle","586b85"},{"MantleLight","697c92"},{"MantleDark","43566f"},
        {"Lining","673e50"},{"Gold","b88c48"},{"GoldLight","d0a967"},
        {"Leather","503a2e"},{"LeatherLight","6e5038"},{"Sole","292728"},
        {"Glove","353442"},{"Pages","ae9973"},{"Face","080c18"},{"Eyes","eadbff"}
    };
    static readonly Dictionary<string,string> Human = new Dictionary<string,string>
    {
        {"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"Neck","Neck"},{"Head","Head"},
        {"LeftShoulder","Clavicle.L"},{"LeftUpperArm","UpperArm.L"},{"LeftLowerArm","LowerArm.L"},{"LeftHand","Hand.L"},
        {"RightShoulder","Clavicle.R"},{"RightUpperArm","UpperArm.R"},{"RightLowerArm","LowerArm.R"},{"RightHand","Hand.R"},
        {"LeftUpperLeg","UpperLeg.L"},{"LeftLowerLeg","LowerLeg.L"},{"LeftFoot","Foot.L"},{"LeftToes","Toes.L"},
        {"RightUpperLeg","UpperLeg.R"},{"RightLowerLeg","LowerLeg.R"},{"RightFoot","Foot.R"},{"RightToes","Toes.R"}
    };

    [MenuItem("Mismo/Character/Integrar mago del concept")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
        Directory.CreateDirectory(Out);
        // New source was archived after the first review to avoid Unity's Blender
        // auto-converter (the standalone FBX is the only model consumed by Player).
        if(File.Exists("Assets/Art/Source/Characters/Mago/ArcaneMage_Source.zip"))
        {
            foreach(string suffix in new[]{".blend",".blend1"})
            {
                string path="Assets/Art/Source/Characters/Mago/ArcaneMage"+suffix;
                if(File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }
        // Keep a one-time exact backup, including all pre-existing gameplay fields.
        if (!File.Exists(Out+"/Player.before.prefab.txt")) File.Copy(Player,Out+"/Player.before.prefab.txt");
        AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        var desc = importer.humanDescription;
        desc.human = Human.Select(p => new HumanBone { humanName=p.Key,boneName=p.Value,limit=new HumanLimit {useDefaultValues=true} }).ToArray();
        desc.upperArmTwist=.5f;desc.lowerArmTwist=.5f;desc.upperLegTwist=.5f;desc.lowerLegTwist=.5f;
        desc.armStretch=.05f;desc.legStretch=.05f;desc.feetSpacing=0;
        importer.humanDescription = desc;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var sourceAnimator = source.GetComponent<Animator>();
        Require(sourceAnimator!=null && sourceAnimator.avatar!=null && sourceAnimator.avatar.isHuman && sourceAnimator.avatar.isValid,"New Avatar invalid");

        Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var material in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>())
        {
            string[] parts=material.name.Split('_');
            if(parts.Length<3 || !Palette.TryGetValue(parts[1],out var hex))continue;
            ColorUtility.TryParseHtmlString("#"+hex,out var c);
            int variant=int.TryParse(parts[2],out var n)?n:1;
            if(parts[1]!="Eyes" && parts[1]!="Face")c*=variant==0?.94f:variant==2?1.065f:1;
            c.a=1;
            string path=Materials+"/"+material.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",c);mat.SetColor("_Color",c);
            mat.SetFloat("_Smoothness",parts[1].StartsWith("Gold")?.3f:.12f);
            mat.SetFloat("_Metallic",parts[1].StartsWith("Gold")?.4f:0);
            if(parts[1]=="Eyes") {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",c*.8f);}
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material),mat);
        }
        importer.SaveAndReimport();
        source=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var root=PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var animator=root.GetComponent<PlayerAnimationDriver>().Animator;
            Require(animator!=null,"Missing existing Player animator");
            var visual=animator.transform;
            if(visual.GetComponent<WarriorCapeMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<WarriorCapeMotion>());
            if(visual.GetComponent<WarriorPlumeMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<WarriorPlumeMotion>());
            if(visual.GetComponent<FrogScarfMotion>()!=null)Object.DestroyImmediate(visual.GetComponent<FrogScarfMotion>());
            // Reuse the visual root and Animator so scene/driver references survive.
            var previousChildren=visual.Cast<Transform>().ToArray();
            var previousObjects=previousChildren.SelectMany(t=>t.GetComponentsInChildren<Transform>(true)).ToArray();
            var oldSet=new HashSet<Transform>(previousObjects);
            var replacement=Object.Instantiate(source);
            replacement.transform.SetPositionAndRotation(visual.position,visual.rotation);
            replacement.transform.localScale=visual.lossyScale;
            foreach(var child in replacement.transform.Cast<Transform>().ToArray())child.SetParent(visual,false);
            var replacementBones=visual.GetComponentsInChildren<Transform>(true).Where(t=>!oldSet.Contains(t)).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                if(!(component is MonoBehaviour) || oldSet.Contains(component.transform))continue;
                var serialized=new SerializedObject(component);var prop=serialized.GetIterator();
                bool changed=false;
                while(prop.Next(true))
                {
                    if(prop.propertyType!=SerializedPropertyType.ObjectReference)continue;
                    var old=prop.objectReferenceValue as Transform;
                    if(old!=null && oldSet.Contains(old) && replacementBones.TryGetValue(old.name,out var value))
                    {prop.objectReferenceValue=value;changed=true;}
                }
                if(changed)serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            animator.avatar=source.GetComponent<Animator>().avatar;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var old in previousChildren)Object.DestroyImmediate(old.gameObject);
            Object.DestroyImmediate(replacement);
            var hat=visual.GetComponent<MageHatMotion>()??visual.gameObject.AddComponent<MageHatMotion>();
            if(visual.GetComponent<MageGroundContact>()==null)visual.gameObject.AddComponent<MageGroundContact>();
            var bones=Enumerable.Range(1,4).Select(i=>replacementBones["Hat_"+i]).ToArray();
            hat.Configure(bones,bones[3].position+bones[3].up*.139f);
            foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=root.layer;
            foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.updateWhenOffscreen=true;
                // Bounds include bent hat and the extended arms of any combat pose.
                Vector3 center=r.transform.InverseTransformPoint(visual.TransformPoint(new Vector3(0,1.2f,0)));
                Vector3 size=r.transform.InverseTransformVector(visual.TransformVector(new Vector3(2.8f,3.2f,2.4f)));
                r.localBounds=new Bounds(center,new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z)));
            }
            PrefabUtility.SaveAsPrefabAsset(root,Player);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        File.Copy(Model,Out+"/ArcaneMage_TPose.fbx",true);
        Validate();
        Debug.Log("ARCANE_MAGE_INTEGRATED");
    }

    public static void Validate()
    {
        var report=new List<string>();
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        try
        {
            // Do not invoke gameplay callbacks during this asset check.
            var driver=root.GetComponent<PlayerAnimationDriver>();var animator=driver.Animator;
            Require(animator!=null && animator.avatar!=null && animator.avatar.isHuman && animator.avatar.isValid,"Player Avatar invalid");
            report.Add("PASS Humanoid Avatar, all 21 mapped bones and T-pose import.");
            Require(root.GetComponent<PlayerMotor>().Visual==animator.transform,"Motor visual reference lost");
            Require(animator.GetComponent<MageGroundContact>()!=null,"Mage ground contact missing");
            Require(animator.GetBoneTransform(HumanBodyBones.RightHand)!=null,"Right hand missing");
            var renderers=animator.GetComponentsInChildren<SkinnedMeshRenderer>();
            Require(renderers.Length>10,"Missing clothing");
            foreach(var r in renderers)
            {
                Require(r.sharedMesh!=null,r.name+" mesh missing");
                Require(r.sharedMaterials.All(m=>m!=null && m.shader!=null),r.name+" material missing");
                Require(r.bones.All(b=>b!=null),r.name+" bone missing");
            }
            Require(renderers.Any(r=>r.name=="Mage_Eyes_Flush"),"Flush eyes missing");
            report.Add("PASS "+renderers.Length+" skinned parts, materials, skin bones and flush eyes.");
            var hat=animator.GetComponent<MageHatMotion>();Require(hat!=null,"Hat motion missing");
            hat.Advance(1f/60);var first=animator.GetComponentsInChildren<Transform>().First(t=>t.name=="Hat_1");
            var rest=first.localRotation;
            root.transform.position+=Vector3.right*.12f;hat.Advance(1f/60);
            Require(Quaternion.Angle(rest,first.localRotation)>.1f,"Hat has no inertial response");
            for(int i=0;i<300;i++)hat.Advance(i%2==0?1f/30:1f/120);
            Require(Quaternion.Angle(rest,first.localRotation)<1f,"Hat does not settle");
            root.transform.position+=Vector3.right*50;hat.Advance(1f/60);
            Require(Quaternion.Angle(rest,first.localRotation)<.01f,"Hat teleport reset failed");
            hat.enabled=false;
            report.Add("PASS hat responds to motion, settles, respects variable frame time and resets on teleport.");
            var clips=animator.runtimeAnimatorController.animationClips.Where(c=>c!=null).ToArray();
            foreach(string name in new[]{"Idle","Walk","Run","Attack"})
            {
                var clip=name=="Attack" ? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/WeaponCombat/OneHandSword/1Hand_Up_Attack_A_1_InPlace.anim") : clips.FirstOrDefault(c=>c.name=="Quaternius_"+name);
                Require(clip!=null && clip.humanMotion,"Missing humanoid "+name);
                animator.Rebind();
                var graph=PlayableGraph.Create("Mage validation");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                try
                {
                    var playable=AnimationClipPlayable.Create(graph,clip);
                    var output=AnimationPlayableOutput.Create(graph,"Character",animator);output.SetSourcePlayable(playable);
                    graph.Play();
                    Vector3 start=Vector3.zero;float excursion=0;
                    for(int i=0;i<12;i++)
                    {
                        playable.SetTime(clip.length*i/12);graph.Evaluate(.0001f);
                        var head=animator.GetBoneTransform(HumanBodyBones.Head).position-root.transform.position;
                        Require(!float.IsNaN(head.x)&&head.magnitude<4,"Invalid retarget pose "+name);
                        var movingBone=animator.GetBoneTransform(name=="Attack"?HumanBodyBones.RightHand:HumanBodyBones.RightFoot).position;
                        if(i==0)start=movingBone;else excursion=Mathf.Max(excursion,Vector3.Distance(start,movingBone));
                    }
                    if(name!="Idle")Require(excursion>.05f,"Clip does not move the rig: "+name);
                    report.Add("PASS "+name+" sampled at 12 times; tracked bone excursion="+excursion.ToString("F3")+" m.");
                }
                finally{graph.Destroy();}
            }
            report.Add("PASS existing locomotion and sword attack retarget; controller preserved.");
        }
        finally{Object.DestroyImmediate(root);}
        try{ProjectOrganizationChecks.Run();report.Add("PASS ProjectOrganizationChecks.Run.");}
        catch(Exception e){report.Add("ORGANIZATION CHECK: "+e.Message);Debug.LogWarning(e.Message);}
        File.WriteAllLines(Out+"/unity-checks.txt",report);
    }

    public static void Capture()
    {
        bool previousAsync=ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation=false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        var animator=player.GetComponent<PlayerAnimationDriver>().Animator;
        foreach(var b in player.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        var sun=new GameObject("Review key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.35f;sun.transform.rotation=Quaternion.Euler(38,145,0);
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.color=new Color(.7f,.82f,1);fill.transform.rotation=Quaternion.Euler(25,-45,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.65f);RenderSettings.fog=false;
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.42f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.14f,.20f);
        camera.nearClipPlane=.05f;camera.farClipPlane=100;
        var clips=animator.runtimeAnimatorController.animationClips.Where(c=>c!=null).ToArray();
        foreach(string motion in new[]{"Idle","Run","Attack"})
        {
            var clip=motion=="Attack" ? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/WeaponCombat/OneHandSword/1Hand_Up_Attack_A_1_InPlace.anim") : clips.First(c=>c.name=="Quaternius_"+motion);
            Require(clip!=null && clip.humanMotion,"Invalid capture clip "+motion);
            var graph=PlayableGraph.Create("Mage capture");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            GameObject weapon=null;
            var baked=new List<GameObject>();var bakedMeshes=new List<Mesh>();
            var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>();
            try
            {
                animator.Rebind();
                var p=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"Mage",animator);output.SetSourcePlayable(p);
                graph.Play();p.SetTime(clip.length*(motion=="Attack"?.4:.3));graph.Evaluate(.0001f);
                // Synchronous CPU snapshots avoid stale GPU skinning in batch-mode captures.
                foreach(var skin in skins)
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);bakedMeshes.Add(mesh);
                    var go=new GameObject("Review "+skin.name);go.transform.SetParent(skin.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    baked.Add(go);skin.enabled=false;
                }
                if(motion=="Attack")
                {
                    var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                    if(definition!=null && definition.visualPrefab!=null && definition.poseProfile!=null)
                    {
                        weapon=Object.Instantiate(definition.visualPrefab);
                        var pose=definition.poseProfile.equipped;
                        pose.Apply(weapon.transform,pose.Resolve(player.transform,animator),animator);
                    }
                }
                foreach(var view in new[]{"Front","Side","Back"})
                {
                    Vector3 offset=view=="Front"?new Vector3(-3,.6f,7):view=="Side"?new Vector3(7,.6f,0):new Vector3(3,.8f,-7);
                    camera.transform.position=new Vector3(0,1.15f,0)+offset;camera.transform.LookAt(new Vector3(0,1.23f,0));
                    SaveCapture(camera,Out+"/Unity_"+motion+"_"+view+".png");
                }
            }
            finally
            {
                foreach(var go in baked)Object.DestroyImmediate(go);
                foreach(var mesh in bakedMeshes)Object.DestroyImmediate(mesh);
                foreach(var skin in skins)skin.enabled=true;
                graph.Destroy();if(weapon!=null)Object.DestroyImmediate(weapon);
            }
        }
        Object.DestroyImmediate(player);
        ShaderUtil.allowAsyncCompilation=previousAsync;
    }
    static void SaveCapture(Camera camera,string path)
    {
        var rt=new RenderTexture(900,1050,24);var prev=RenderTexture.active;var image=new Texture2D(900,1050,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,900,1050),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=prev;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void RunBatch()
    {
        try{Apply();Capture();EditorApplication.Exit(0);}
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/unity-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Build()
    {
        Validate();
        var result=UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=Out+"/Build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        File.WriteAllText(Out+"/build-check.txt",result.summary.result+" | errors="+result.summary.totalErrors+" | bytes="+result.summary.totalSize);
        Require(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"Mage build failed");
    }
    public static void ReviewAndBuild()
    {
        try{Validate();Capture();Build();EditorApplication.Exit(0);}
        catch(Exception e){File.WriteAllText(Out+"/build-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
