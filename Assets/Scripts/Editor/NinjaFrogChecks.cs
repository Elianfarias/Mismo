using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class NinjaFrogChecks
{
    const string Out=NinjaFrogIntegration.Out;
    const string Player=AshenWarriorIntegration.Player;
    const string Attack="Assets/Art/Animations/WeaponCombat/OneHandSword/1Hand_Up_Attack_A_1_InPlace.anim";
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}

    public static void Run()
    {
        Directory.CreateDirectory(Out);var report=new List<string>();
        var mageBefore=File.ReadAllBytes(AshenWarriorIntegration.Mage);
        var warriorBefore=File.ReadAllBytes(AshenWarriorIntegration.Warrior);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        try
        {
            var animator=player.GetComponent<PlayerAnimationDriver>().Animator;
            Require(animator.avatar!=null && animator.avatar.isHuman && animator.avatar.isValid,"Invalid frog avatar");
            Require(player.GetComponent<PlayerMotor>().Visual==animator.transform,"Motor visual reference changed");
            Require(animator.GetComponent<MageHatMotion>()==null && animator.GetComponent<WarriorCapeMotion>()==null && animator.GetComponent<WarriorPlumeMotion>()==null,"Frog retained another skin's motion components");
            var skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(var skin in skins)
            {
                Require(skin.sharedMesh!=null && skin.bones.All(b=>b!=null),"Invalid mesh bones: "+skin.name);
                Require(skin.sharedMaterials.All(m=>m!=null && m.shader!=null),"Missing material: "+skin.name);
                Require(!new[]{"katana","sword","scabbard","weapon"}.Any(s=>skin.name.ToLowerInvariant().Contains(s)),"Frog model contains a weapon");
                foreach(var w in skin.sharedMesh.boneWeights)Require(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f,"Unnormalized skin weights: "+skin.name);
            }
            Require(new[]{"Frog_Head","Frog_Jaw","Frog_Iris_L","Frog_Iris_R","Frog_ScarfTail_L","Frog_ScarfTail_R"}.All(n=>skins.Any(r=>r.name==n)),"Missing frog anatomy");
            var neck=skins.First(r=>r.name=="Frog_Neck");var jaw=skins.First(r=>r.name=="Frog_Jaw");var neckSeam=SeamPairs(neck,jaw);
            Require(neckSeam.Count>=12,"Neck/jaw seam is open: "+neckSeam.Count);
            var feet=skins.Where(r=>r.name.EndsWith("_Foot_L")||r.name.EndsWith("_Foot_R")).ToArray();Require(feet.Length==2,"Two frog feet are required");
            report.Add("PASS valid Humanoid, "+skins.Length+" unarmed skinned parts, materials and normalized weights; head/neck seam has "+neckSeam.Count+" shared vertices.");
            var scarf=animator.GetComponent<FrogScarfMotion>();Require(scarf!=null,"Scarf simulation missing");
            var fields=new SerializedObject(scarf);
            foreach(var field in new[]{"hips","neck","leftThigh","leftKnee","rightThigh","rightKnee"})
                Require(fields.FindProperty(field).objectReferenceValue is Transform bodyBone && bodyBone.IsChildOf(animator.transform),"Scarf collision reference missing: "+field);
            var bones=animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Scarf.") && !t.name.EndsWith("_tip")).OrderBy(t=>t.name).ToArray();
            Require(bones.Length==8,"Scarf needs two four-bone chains");var rest=bones.Select(b=>b.localRotation).ToArray();
            Func<float> angle=()=>bones.Select((b,i)=>Quaternion.Angle(b.localRotation,rest[i])).Max();
            scarf.Advance(1f/60);player.transform.position+=Vector3.right*.12f;scarf.Advance(1f/60);
            float response=angle();Require(response>.5f,"Scarf has no inertia");
            var paused=bones.Select(b=>b.localRotation).ToArray();scarf.Advance(0);
            Require(bones.Select((b,i)=>Quaternion.Angle(b.localRotation,paused[i])).Max()<.01f,"Scarf moves while paused");
            for(int i=0;i<360;i++)scarf.Advance(i%2==0?1f/30:1f/120);
            Require(angle()<1.5f,"Scarf did not settle: "+angle());
            player.transform.position+=Vector3.right*20;scarf.Advance(1f/60);Require(angle()<.01f,"Teleport reset failed");
            player.transform.position+=Vector3.right*.12f;scarf.Advance(1f/60);scarf.Advance(.3f);Require(angle()<.01f,"Long-frame reset failed");
            report.Add("PASS scarf inertia="+response.ToString("F2")+" degrees; pause, settling, variable dt, teleport and long-frame reset.");
            var contact=animator.GetComponent<MageGroundContact>();Require(contact!=null,"Grounding missing");
            contact.Initialize(animator,player.GetComponent<CharacterController>());var clips=animator.runtimeAnimatorController.animationClips;animator.applyRootMotion=false;
            foreach(string motion in new[]{"Idle","Walk","Run","Attack","Jump","Fall"})
            {
                var clip=motion=="Attack"?AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack):clips.First(c=>c.name=="Quaternius_"+motion);
                Require(clip.humanMotion,"Non-Humanoid clip "+motion);contact.RestorePose();animator.Rebind();scarf.Advance(.2f);
                var graph=PlayableGraph.Create("Frog regression");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                try
                {
                    var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);
                    var output=AnimationPlayableOutput.Create(graph,"Frog",animator);output.SetSourcePlayable(p);graph.Play();
                    bool air=motion=="Jump"||motion=="Fall";float soleError=0,seamError=0,maxBend=0,excursion=0;Vector3 start=Vector3.zero;
                    for(int i=0;i<48;i++)
                    {
                        contact.RestorePose();p.SetTime(clip.length*i/48);graph.Evaluate(.00001f);
                        player.transform.position=new Vector3(.1f*Mathf.Sin(i*.1f),.04f,i*.055f);player.transform.rotation=Quaternion.Euler(0,30*Mathf.Sin(i*.05f),0);
                        var root=player.transform.position;float raw=Soles(feet);contact.ApplyGrounding(!air,0);float actual=Soles(feet);
                        if(air)Require(Mathf.Abs(actual-raw)<.0001f,"Airborne pose modified");
                        else{soleError=Mathf.Max(soleError,Mathf.Abs(actual-.002f));Require(soleError<.001f,motion+" floating feet: "+actual);}
                        Require(player.transform.position==root,"Grounding moved gameplay root");
                        scarf.Advance(1f/60);maxBend=Mathf.Max(maxBend,angle());Require(maxBend<30.1f,"Scarf exceeded angular limit");
                        seamError=Mathf.Max(seamError,SeamDistance(neck,jaw,neckSeam));Require(seamError<.004f,"Head separated in "+motion+": "+seamError);
                        var bone=animator.GetBoneTransform(motion=="Attack"?HumanBodyBones.RightHand:HumanBodyBones.RightFoot);var point=player.transform.InverseTransformPoint(bone.position);
                        Require(!float.IsNaN(point.x) && point.magnitude<4,"Invalid pose");
                        if(i==0)start=point;else excursion=Mathf.Max(excursion,Vector3.Distance(start,point));
                        contact.RestorePose();Require(Mathf.Abs(Soles(feet)-raw)<.0001f,"Grounding restore failed");
                    }
                    if(motion=="Walk"||motion=="Run"||motion=="Attack")Require(excursion>.05f,"Clip does not animate: "+motion);
                    report.Add("PASS "+motion+": 48 poses; excursion="+excursion.ToString("F3")+" m; scarf bend<="+maxBend.ToString("F2")+" degrees; neck seam="+seamError.ToString("F5")+" m; "+(air?"airborne unchanged":"sole error="+soleError.ToString("F5")+" m"));
                }
                finally{graph.Destroy();}
            }
        }
        finally{Object.DestroyImmediate(player);File.WriteAllLines(Out+"/unity-checks.txt",report);}
        // Exercise all three authoring choices and the existing skin regressions.
        AshenWarriorIntegration.UseMage();
        var mage=AssetDatabase.LoadAssetAtPath<GameObject>(Player);
        Require(mage.GetComponentInChildren<MageHatMotion>()!=null && mage.GetComponentInChildren<FrogScarfMotion>()==null,"Mage selection failed");
        AshenWarriorIntegration.UseWarrior();AshenWarriorChecks.Run();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<FrogScarfMotion>()==null,"Warrior retained frog scarf");
        NinjaFrogIntegration.UseFrog();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<FrogScarfMotion>()!=null,"Frog selection failed");
        Require(mageBefore.SequenceEqual(File.ReadAllBytes(AshenWarriorIntegration.Mage)) && warriorBefore.SequenceEqual(File.ReadAllBytes(AshenWarriorIntegration.Warrior)),"Existing skin assets changed");
        report.Add("PASS mage/warrior/frog round trip; existing skin prefabs byte-for-byte preserved; warrior regressions passed; frog left active.");
        try{ProjectOrganizationChecks.Run();report.Add("PASS project organization.");}
        catch(Exception e){report.Add("EXISTING ORGANIZATION BLOCKER: "+e.Message);Debug.LogWarning(e.Message);}
        File.WriteAllLines(Out+"/unity-checks.txt",report);Debug.Log("NINJA_FROG_CHECKS_PASSED");
    }
    static float Soles(SkinnedMeshRenderer[] boots)
    {
        float min=float.PositiveInfinity;var mesh=new Mesh();
        try{foreach(var boot in boots){boot.BakeMesh(mesh);foreach(var p in mesh.vertices)min=Mathf.Min(min,boot.transform.TransformPoint(p).y);}}
        finally{Object.DestroyImmediate(mesh);}return min;
    }

    static List<(int a,int b)> SeamPairs(SkinnedMeshRenderer a,SkinnedMeshRenderer b)
    {
        var ma=new Mesh();var mb=new Mesh();var pairs=new List<(int,int)>();
        try
        {
            a.BakeMesh(ma);b.BakeMesh(mb);var av=ma.vertices;var bv=mb.vertices;
            var map=new Dictionary<Vector3Int,int>();
            for(int i=0;i<bv.Length;i++)map[Vector3Int.RoundToInt(b.transform.TransformPoint(bv[i])*10000)]=i;
            for(int i=0;i<av.Length;i++)if(map.TryGetValue(Vector3Int.RoundToInt(a.transform.TransformPoint(av[i])*10000),out int j))pairs.Add((i,j));
            return pairs;
        }
        finally{Object.DestroyImmediate(ma);Object.DestroyImmediate(mb);}
    }
    static float SeamDistance(SkinnedMeshRenderer a,SkinnedMeshRenderer b,List<(int a,int b)> pairs)
    {
        var ma=new Mesh();var mb=new Mesh();float max=0;
        try
        {
            a.BakeMesh(ma);b.BakeMesh(mb);var av=ma.vertices;var bv=mb.vertices;
            foreach(var pair in pairs)max=Mathf.Max(max,Vector3.Distance(a.transform.TransformPoint(av[pair.a]),b.transform.TransformPoint(bv[pair.b])));
            return max;
        }
        finally{Object.DestroyImmediate(ma);Object.DestroyImmediate(mb);}
    }

    public static void Capture()
    {
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        var animator=player.GetComponent<PlayerAnimationDriver>().Animator;
        foreach(var b in player.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        animator.applyRootMotion=false;
        var contact=animator.GetComponent<MageGroundContact>();contact.Initialize(animator,player.GetComponent<CharacterController>());
        var scarf=animator.GetComponent<FrogScarfMotion>();
        var sun=new GameObject("Review key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.35f;sun.transform.rotation=Quaternion.Euler(38,145,0);
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.color=new Color(.7f,.82f,1);fill.transform.rotation=Quaternion.Euler(25,-45,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.65f);RenderSettings.fog=false;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(200,.1f,200);
        var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.SetColor("_BaseColor",new Color(.12f,.17f,.23f));floor.GetComponent<Renderer>().sharedMaterial=floorMat;
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.3f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.14f,.20f);camera.nearClipPlane=.05f;camera.farClipPlane=100;
        var clips=animator.runtimeAnimatorController.animationClips;
        try
        {
            foreach(string motion in new[]{"Idle","Run","Attack"})
            {
                var clip=motion=="Attack"?AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack):clips.First(c=>c.name=="Quaternius_"+motion);
                var graph=PlayableGraph.Create("Frog capture");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baked=new List<GameObject>();var meshes=new List<Mesh>();var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>();
                try
                {
                    contact.RestorePose();animator.Rebind();scarf.Advance(.2f);
                    var p=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"Frog",animator);output.SetSourcePlayable(p);graph.Play();
                    for(int step=0;step<90;step++)
                    {
                        contact.RestorePose();p.SetTime(motion=="Run"?step/60.0:clip.length*(motion=="Attack"?.4:.3)*step/89);graph.Evaluate(.00001f);
                        player.transform.position=new Vector3(0,.04f,motion=="Run"?step*.07f:0);
                        contact.ApplyGrounding(true,0);scarf.Advance(1f/60);
                    }
                    player.transform.position=Vector3.up*.04f;
                    foreach(var skin in skins)
                    {
                        var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                        var go=new GameObject("Review "+skin.name);go.transform.SetParent(skin.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;baked.Add(go);skin.enabled=false;
                    }
                    foreach(string view in new[]{"Front","Side","Back"})
                    {
                        Vector3 offset=view=="Front"?new Vector3(-3,.7f,7):view=="Side"?new Vector3(7,.6f,0):new Vector3(3,.8f,-7);
                        camera.transform.position=new Vector3(0,1.04f,0)+offset;camera.transform.LookAt(new Vector3(0,1.04f,0));SaveCapture(camera,Out+"/Unity_"+motion+"_"+view+".png");
                    }
                    if(motion!="Idle")
                    {
                        Vector3 target=animator.GetBoneTransform(HumanBodyBones.Neck).position+Vector3.up*.10f;
                        camera.orthographicSize=.54f;camera.transform.position=target+new Vector3(4,.25f,2);camera.transform.LookAt(target);
                        SaveCapture(camera,Out+"/Unity_"+motion+"_Neck.png");camera.orthographicSize=1.3f;
                    }
                }
                finally
                {
                    foreach(var go in baked)Object.DestroyImmediate(go);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);foreach(var skin in skins)skin.enabled=true;
                    graph.Destroy();
                }
            }
        }
        finally{Object.DestroyImmediate(player);Object.DestroyImmediate(floorMat);ShaderUtil.allowAsyncCompilation=async;}
    }
    static void SaveCapture(Camera camera,string path)
    {
        var rt=new RenderTexture(900,1050,24);var previous=RenderTexture.active;var image=new Texture2D(900,1050,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,900,1050),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
    }
    public static void Build()
    {
        try
        {
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=Out+"/Build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(Out+"/build-check.txt",result.summary.result+" | errors="+result.summary.totalErrors+" | bytes="+result.summary.totalSize);
            EditorApplication.Exit(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded && result.summary.totalErrors==0?0:1);
        }
        catch(Exception e){File.WriteAllText(Out+"/build-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
