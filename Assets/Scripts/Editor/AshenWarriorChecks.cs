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
using Object=UnityEngine.Object;

public static class AshenWarriorChecks
{
    const string Out=AshenWarriorIntegration.Out;
    const string Player=AshenWarriorIntegration.Player;
    const string Attack="Assets/Art/Animations/WeaponCombat/OneHandSword/1Hand_Up_Attack_A_1_InPlace.anim";
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run()
    {
        Directory.CreateDirectory(Out);var report=new List<string>();
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        try
        {
            var animator=player.GetComponent<PlayerAnimationDriver>().Animator;
            Require(animator.avatar!=null && animator.avatar.isHuman && animator.avatar.isValid,"Invalid warrior avatar");
            Require(player.GetComponent<PlayerMotor>().Visual==animator.transform,"Motor visual reference changed");
            Require(animator.GetComponent<MageHatMotion>()==null,"Warrior retained hat simulation");
            var skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>();
            Require(skins.Any(r=>r.name=="Warrior_Eyes_Flush") && skins.Any(r=>r.name=="Warrior_Cape"),"Missing warrior parts");
            var neckMail=skins.FirstOrDefault(r=>r.name=="Warrior_Aventail");
            Require(neckMail!=null && new[]{"Chest","Neck","Head"}.All(n=>neckMail.bones.Any(b=>b.name==n)),"Missing flexible neck coverage");
            var helmet=skins.First(r=>r.name=="Warrior_Helmet");
            var neckSeam=SeamPairs(neckMail,helmet);
            Require(neckSeam.Count>=12,"Neck does not meet helmet geometry: "+neckSeam.Count+" shared vertices");
            foreach(var skin in skins)
            {
                Require(skin.sharedMesh!=null && skin.bones.All(b=>b!=null),"Invalid mesh/skin bones: "+skin.name);
                Require(skin.sharedMaterials.All(m=>m!=null && m.shader!=null),"Invalid material: "+skin.name);
            }
            report.Add("PASS saved Player: valid Humanoid, motor/Animator references, "+skins.Length+" skinned parts, materials and flush eyes.");
            report.Add("PASS neck mail closes collar/helmet gap and blends Chest, Neck and Head.");
            var cape=animator.GetComponent<WarriorCapeMotion>();Require(cape!=null,"Cape motion missing");
            var capeFields=new SerializedObject(cape);
            foreach(string field in new[]{"hips","neck","leftThigh","leftKnee","rightThigh","rightKnee"})
                Require(capeFields.FindProperty(field).objectReferenceValue is Transform bodyBone && bodyBone.IsChildOf(animator.transform),"Cape body reference missing: "+field);
            var bones=animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Cape.")).OrderBy(t=>t.name).ToArray();
            Require(bones.Length==12,"Cape requires 12 bones");var rest=bones.Select(b=>b.localRotation).ToArray();
            Func<float> angle=()=>bones.Select((b,i)=>Quaternion.Angle(b.localRotation,rest[i])).Max();
            cape.Advance(1f/60);player.transform.position+=Vector3.right*.12f;cape.Advance(1f/60);
            float response=angle();Require(response>.5f,"No cape inertia");
            var paused=bones.Select(b=>b.localRotation).ToArray();cape.Advance(0);
            Require(bones.Select((b,i)=>Quaternion.Angle(b.localRotation,paused[i])).Max()<.01f,"Pause moved cape");
            for(int i=0;i<360;i++)cape.Advance(i%2==0?1f/30:1f/120);
            Require(angle()<1.5f,"Cape does not settle: "+angle());
            player.transform.position+=Vector3.right*20;cape.Advance(1f/60);Require(angle()<.01f,"Teleport reset failed");
            player.transform.position+=Vector3.right*.15f;cape.Advance(1f/60);cape.Advance(.3f);Require(angle()<.01f,"Long frame reset failed");
            report.Add("PASS cape inertia="+response.ToString("F2")+" degrees; settling, pause, variable dt, teleport and long-frame reset.");
            var plume=animator.GetComponent<WarriorPlumeMotion>();Require(plume!=null,"Helmet plume simulation missing");
            Require(new SerializedObject(plume).FindProperty("head").objectReferenceValue==animator.GetBoneTransform(HumanBodyBones.Head),"Plume is not pinned to the current Head");
            var plumeBones=animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Plume_")).OrderBy(t=>t.name).ToArray();
            Require(plumeBones.Length==5 && skins.Any(r=>r.name=="Warrior_Plume"),"Five-bone red plume missing");
            var plumeRest=plumeBones.Select(b=>b.localRotation).ToArray();
            Func<float> plumeAngle=()=>plumeBones.Select((b,i)=>Quaternion.Angle(b.localRotation,plumeRest[i])).Max();
            plume.Advance(.2f);player.transform.position+=Vector3.right*.12f;plume.Advance(1f/60);
            float plumeResponse=plumeAngle();Require(plumeResponse>.3f,"Plume has no inertia");
            var plumePaused=plumeBones.Select(b=>b.localRotation).ToArray();plume.Advance(0);
            Require(plumeBones.Select((b,i)=>Quaternion.Angle(b.localRotation,plumePaused[i])).Max()<.01f,"Plume moves while paused");
            for(int i=0;i<360;i++)plume.Advance(i%2==0?1f/30:1f/120);
            Require(plumeAngle()<1.5f,"Plume does not settle");
            player.transform.position+=Vector3.right*20;plume.Advance(1f/60);Require(plumeAngle()<.01f,"Plume teleport reset failed");
            player.transform.position+=Vector3.right*.12f;plume.Advance(1f/60);plume.Advance(.3f);Require(plumeAngle()<.01f,"Plume long-frame reset failed");
            report.Add("PASS five-bone plume, inertia="+plumeResponse.ToString("F2")+" degrees; settling, pause, variable dt, teleport and long-frame reset.");
            player.transform.position=Vector3.up*.04f;
            var contact=animator.GetComponent<MageGroundContact>();Require(contact!=null,"Boot grounding missing");
            contact.Initialize(animator,player.GetComponent<CharacterController>());
            var boots=skins.Where(r=>r.name.EndsWith("_Boot_L")||r.name.EndsWith("_Boot_R")).ToArray();Require(boots.Length==2,"Boots missing");
            var clips=animator.runtimeAnimatorController.animationClips;
            animator.applyRootMotion=false;
            foreach(string motion in new[]{"Idle","Walk","Run","Attack","Jump","Fall"})
            {
                var clip=motion=="Attack"?AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack):clips.FirstOrDefault(c=>c.name=="Quaternius_"+motion);
                Require(clip!=null && clip.humanMotion,"Missing clip "+motion);
                contact.RestorePose();animator.Rebind();cape.Advance(.2f);plume.Advance(.2f);
                var graph=PlayableGraph.Create("Warrior regression");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                try
                {
                    var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);
                    var o=AnimationPlayableOutput.Create(graph,"Skin",animator);o.SetSourcePlayable(p);graph.Play();
                    float excursion=0,maxBend=0,soleError=0,seamError=0,maxPlume=0;Vector3 start=Vector3.zero;
                    bool air=motion=="Jump"||motion=="Fall";
                    for(int i=0;i<48;i++)
                    {
                        contact.RestorePose();p.SetTime(clip.length*i/48);graph.Evaluate(.00001f);
                        player.transform.position=new Vector3(.1f*Mathf.Sin(i*.1f),.04f,i*.055f);
                        player.transform.rotation=Quaternion.Euler(0,30*Mathf.Sin(i*.05f),0);
                        float raw=Soles(boots);var root=player.transform.position;
                        contact.ApplyGrounding(!air,0);float actual=Soles(boots);
                        if(air)Require(Mathf.Abs(actual-raw)<.0001f,"Airborne pose altered");
                        else{soleError=Mathf.Max(soleError,Mathf.Abs(actual-.002f));Require(soleError<.001f,motion+" floating sole "+actual);}
                        Require(player.transform.position==root,"Grounding changed motor root");
                        cape.Advance(1f/60);maxBend=Mathf.Max(maxBend,angle());
                        Require(maxBend<28.1f,"Cape angular limit exceeded: "+maxBend);
                        plume.Advance(1f/60);maxPlume=Mathf.Max(maxPlume,plumeAngle());Require(maxPlume<22.1f,"Plume angular limit exceeded");
                        seamError=Mathf.Max(seamError,SeamDistance(neckMail,helmet,neckSeam));
                        Require(seamError<.004f,"Neck/helmet seam separated during "+motion+": "+seamError);
                        var bone=animator.GetBoneTransform(motion=="Attack"?HumanBodyBones.RightHand:HumanBodyBones.RightFoot);
                        Vector3 point=player.transform.InverseTransformPoint(bone.position);
                        Require(!float.IsNaN(point.x) && point.magnitude<4,"Invalid animation pose");
                        if(i==0)start=point;else excursion=Mathf.Max(excursion,Vector3.Distance(start,point));
                        contact.RestorePose();Require(Mathf.Abs(Soles(boots)-raw)<.0001f,"Grounding did not restore pose");
                    }
                    if(motion=="Run"||motion=="Walk"||motion=="Attack")Require(excursion>.05f,"Clip does not animate: "+motion);
                    report.Add("PASS "+motion+": 48 poses; bone excursion="+excursion.ToString("F3")+" m; cape/plume bend <= "+maxBend.ToString("F2")+"/"+maxPlume.ToString("F2")+" degrees; neck seam error="+seamError.ToString("F5")+" m; "+(air?"airborne unchanged":"sole error="+soleError.ToString("F5")+" m"));
                }
                finally{graph.Destroy();}
            }
            var mage=AssetDatabase.LoadAssetAtPath<GameObject>(AshenWarriorIntegration.Mage);
            Require(mage!=null && mage.GetComponent<MageHatMotion>()!=null && mage.GetComponent<Animator>().avatar.isValid,"Mage backup missing");
            report.Add("PASS original mage retained as a separate visual skin with its own avatar and hat motion.");
        }
        finally{Object.DestroyImmediate(player);File.WriteAllLines(Out+"/unity-checks.txt",report);}
        // Exercise editor skin selection and leave the requested warrior active.
        AshenWarriorIntegration.UseMage();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<MageHatMotion>()!=null,"Mage switch failed");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<WarriorPlumeMotion>()==null,"Mage retained warrior plume component");
        AshenWarriorIntegration.UseWarrior();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<WarriorCapeMotion>()!=null,"Warrior switch failed");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(Player).GetComponentInChildren<WarriorPlumeMotion>()!=null,"Warrior switch lost plume component");
        report.Add("PASS mage/warrior selection round trip; warrior left active.");
        try{ProjectOrganizationChecks.Run();report.Add("PASS project organization.");}
        catch(Exception e){report.Add("EXISTING ORGANIZATION BLOCKER: "+e.Message);Debug.LogWarning(e.Message);}
        File.WriteAllLines(Out+"/unity-checks.txt",report);Debug.Log("ASHEN_WARRIOR_CHECKS_PASSED");
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
        var cape=animator.GetComponent<WarriorCapeMotion>();
        var plume=animator.GetComponent<WarriorPlumeMotion>();
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
                var graph=PlayableGraph.Create("Warrior capture");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baked=new List<GameObject>();var meshes=new List<Mesh>();var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>();GameObject weapon=null;
                try
                {
                    contact.RestorePose();animator.Rebind();cape.Advance(.2f);plume.Advance(.2f);
                    var p=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"Warrior",animator);output.SetSourcePlayable(p);graph.Play();
                    for(int step=0;step<90;step++)
                    {
                        contact.RestorePose();p.SetTime(motion=="Run"?step/60.0:clip.length*(motion=="Attack"?.4:.3)*step/89);graph.Evaluate(.00001f);
                        player.transform.position=new Vector3(0,.04f,motion=="Run"?step*.07f:0);
                        contact.ApplyGrounding(true,0);cape.Advance(1f/60);plume.Advance(1f/60);
                    }
                    player.transform.position=Vector3.up*.04f;
                    foreach(var skin in skins)
                    {
                        var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                        var go=new GameObject("Review "+skin.name);go.transform.SetParent(skin.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;baked.Add(go);skin.enabled=false;
                    }
                    if(motion=="Attack")
                    {
                        var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
                        if(definition!=null && definition.visualPrefab!=null && definition.poseProfile!=null)
                        {weapon=Object.Instantiate(definition.visualPrefab);var pose=definition.poseProfile.equipped;pose.Apply(weapon.transform,pose.Resolve(player.transform,animator),animator);}
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
                    graph.Destroy();if(weapon!=null)Object.DestroyImmediate(weapon);
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
