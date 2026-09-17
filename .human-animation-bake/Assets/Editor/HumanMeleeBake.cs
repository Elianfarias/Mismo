using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HumanMeleeBake
{
    static readonly string[] Source = { "B-hips", "B-spine", "B-chest", "B-neck", "B-head", "B-shoulder.L", "B-upperArm.L", "B-forearm.L", "B-hand.L", "B-shoulder.R", "B-upperArm.R", "B-forearm.R", "B-hand.R", "B-thigh.L", "B-shin.L", "B-foot.L", "B-toe.L", "B-thigh.R", "B-shin.R", "B-foot.R", "B-toe.R" };
    static readonly string[] Target = { "Hips", "Spine", "Chest", "Neck", "Head", "Clavicle.L", "UpperArm.L", "LowerArm.L", "Hand.L", "Clavicle.R", "UpperArm.R", "LowerArm.R", "Hand.R", "UpperLeg.L", "LowerLeg.L", "Foot.L", "Toes.L", "UpperLeg.R", "LowerLeg.R", "Foot.R", "Toes.R" };
    static readonly int[] Child = { 1,2,3,4,-1,6,7,8,-1,10,11,12,-1,14,15,16,-1,18,19,20,-1 };
    static readonly List<string> Report = new List<string>();

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Assets/Baked"); AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles("Assets/Source", "*.fbx").Concat(Directory.GetFiles("Assets/Rigs", "*.fbx")))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                importer.isReadable = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
            }
            foreach (var path in Directory.GetFiles("Assets/Source", "*.fbx"))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                var name = Path.GetFileNameWithoutExtension(path).Replace("HumanM@", "");
                Bake(path, clip, "Player", name);
                if (name == "CombatDamage01" || name == "Attack1H01_R" || name == "CombatIdle1H01") Bake(path, clip, "Goblin", name);
            }
            ComposeDual("DualSequence", false);
            ComposeDual("DualCross", true);
            AssetDatabase.SaveAssets();
            File.WriteAllLines("bake-result.txt", new[] { "PASS" }.Concat(Report));
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("bake-result.txt", "FAIL\n" + e + "\n" + string.Join("\n", Report)); EditorApplication.Exit(1); }
    }

    static void BindPose(GameObject model)
    {
        var matrices = new Dictionary<Transform, Matrix4x4>();
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            for (int i = 0; i < skin.bones.Length; i++)
                if (skin.bones[i] != null) matrices[skin.bones[i]] = skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[i].inverse;
        foreach (var bone in model.GetComponentsInChildren<Transform>())
            if (matrices.TryGetValue(bone, out var m)) bone.SetPositionAndRotation(m.GetColumn(3), m.rotation);
    }

    static void ComposeDual(string name, bool together)
    {
        var right=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/Human_Player_Attack1H01_R.anim");
        var left=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/Human_Player_Attack1H01_L.anim");
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rigs/Player.fbx"));
        foreach(var a in model.GetComponentsInChildren<Animator>())a.enabled=false;
        try
        {
            var bones=model.GetComponentsInChildren<Transform>().Where(t=>Target.Contains(t.name)).ToArray();
            var tracks=bones.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
            float length=together?right.length:right.length+left.length-.14f;
            int count=Mathf.CeilToInt(length*60);
            for(int f=0;f<=count;f++)
            {
                float time=length*f/count;
                right.SampleAnimation(model,Mathf.Min(time,right.length));
                var positions=bones.Select(t=>t.localPosition).ToArray();var rotations=bones.Select(t=>t.localRotation).ToArray();
                left.SampleAnimation(model,together?time:Mathf.Max(0,time-right.length+.14f));
                for(int i=0;i<bones.Length;i++)
                {
                    float blend=together?(bones[i].name.EndsWith(".L")&&bones[i].IsChildOf(model.transform.Find("Armature_Humanoid/Root/Hips/Spine"))?1:0):Mathf.SmoothStep(0,1,Mathf.InverseLerp(right.length-.14f,right.length,time));
                    var p=Vector3.Lerp(positions[i],bones[i].localPosition,blend);var q=Quaternion.Slerp(rotations[i],bones[i].localRotation,blend);
                    float[] values={q.x,q.y,q.z,q.w,p.x,p.y,p.z};
                    for(int k=0;k<7;k++)tracks[bones[i]][k].AddKey(time,values[k]);
                }
            }
            var result=new AnimationClip{name="Human_Player_"+name,frameRate=60};
            foreach(var bone in bones)for(int k=0;k<7;k++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone,model.transform),typeof(Transform),k<4?"m_LocalRotation."+"xyzw"[k]:"m_LocalPosition."+"xyz"[k-4]),tracks[bone][k]);
            result.EnsureQuaternionContinuity();
            string path="Assets/Baked/"+result.name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing!=null){EditorUtility.CopySerialized(result,existing);Object.DestroyImmediate(result);result=existing;EditorUtility.SetDirty(result);}else AssetDatabase.CreateAsset(result,path);
            Render(model,result);Report.Add(result.name+" composed from imported left/right attacks");
        }
        finally{Object.DestroyImmediate(model);}
    }

    static void Bake(string path, AnimationClip clip, string rig, string name)
    {
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rigs/" + rig + ".fbx"));
        try
        {
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var a in source.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var a in target.GetComponentsInChildren<Animator>()) a.enabled = false;
            BindPose(source); BindPose(target);
            var st = source.GetComponentsInChildren<Transform>(); var tt = target.GetComponentsInChildren<Transform>();
            var pairs = Enumerable.Range(0, Target.Length).Where(i => st.Any(t => t.name == Source[i]) && tt.Any(t => t.name == Target[i])).ToArray();
            if (pairs.Length < 15) throw new Exception("Insufficient mapping " + rig + " " + pairs.Length + " source=" + string.Join(",",st.Select(t=>t.name)));
            var sb = pairs.Select(i => st.First(t => t.name == Source[i])).ToArray();
            var tb = pairs.Select(i => tt.First(t => t.name == Target[i])).ToArray();
            Transform S(int index) => st.First(t => t.name == Source[index]);
            Transform T(int index) => tt.First(t => t.name == Target[index]);
            source.transform.rotation = Quaternion.FromToRotation(Vector3.ProjectOnPlane(S(17).position-S(13).position,Vector3.up),Vector3.ProjectOnPlane(T(17).position-T(13).position,Vector3.up));
            foreach (int i in pairs)
                if (Child[i] >= 0 && pairs.Contains(Child[i]))
                    T(i).rotation = Quaternion.FromToRotation(T(Child[i]).position-T(i).position, S(Child[i]).position-S(i).position)*T(i).rotation;
            var sr = sb.Select(t=>t.rotation).ToArray(); var tr = tb.Select(t=>t.rotation).ToArray();
            var spos=st.Select(t=>t.localPosition).ToArray(); var srot=st.Select(t=>t.localRotation).ToArray();
            var tpos=tt.Select(t=>t.localPosition).ToArray(); var trot=tt.Select(t=>t.localRotation).ToArray();
            var sourceHip=S(0).position; var targetHip=T(0).position;
            float ratio=(Vector3.Distance(T(13).position,T(14).position)+Vector3.Distance(T(14).position,T(15).position))/(Vector3.Distance(S(13).position,S(14).position)+Vector3.Distance(S(14).position,S(15).position));
            var contacts=new List<(Transform bone,Vector3 point)>();
            foreach(var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i]; var bone=skin.bones[w.boneIndex0];
                    if(w.weight0>.999f && bone!=null && bone.name.StartsWith("Foot."))contacts.Add((bone,mesh.bindposes[w.boneIndex0].MultiplyPoint3x4(vertices[i])));
                }
            }
            float Floor()=>contacts.Count>0?contacts.Min(c=>c.bone.TransformPoint(c.point).y):Mathf.Min(T(15).position.y,T(19).position.y);
            float restFloor=Floor();
            var tracks=tb.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
            int count=Mathf.Max(1,Mathf.CeilToInt(clip.length*60));
            float maxDrift=0;
            for(int frame=0;frame<=count;frame++)
            {
                for(int i=0;i<st.Length;i++){st[i].localPosition=spos[i];st[i].localRotation=srot[i];}
                for(int i=0;i<tt.Length;i++){tt[i].localPosition=tpos[i];tt[i].localRotation=trot[i];}
                float time=clip.length*frame/count;
                clip.SampleAnimation(source,time);
                for(int i=0;i<tb.Length;i++) tb[i].rotation=sb[i].rotation*Quaternion.Inverse(sr[i])*tr[i];
                // Keep gameplay displacement in the motor. Only hip height follows the source.
                T(0).position=targetHip+Vector3.up*((S(0).position-sourceHip).y*ratio);
                T(0).position+=Vector3.up*(restFloor-Floor());
                maxDrift=Mathf.Max(maxDrift,new Vector2(T(0).position.x-targetHip.x,T(0).position.z-targetHip.z).magnitude);
                foreach(var bone in tb)
                {
                    var q=bone.localRotation; var p=bone.localPosition;
                    float[] values={q.x,q.y,q.z,q.w,p.x,p.y,p.z};
                    if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new Exception("Nonfinite pose");
                    for(int i=0;i<7;i++)tracks[bone][i].AddKey(time,values[i]);
                }
            }
            var baked=new AnimationClip{name="Human_"+rig+"_"+name,frameRate=60};
            foreach(var bone in tb)for(int k=0;k<7;k++)
                AnimationUtility.SetEditorCurve(baked,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone,target.transform),typeof(Transform),k<4?"m_LocalRotation."+"xyzw"[k]:"m_LocalPosition."+"xyz"[k-4]),tracks[bone][k]);
            baked.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(baked);settings.loopTime=name.Contains("Idle")||name.Contains("Hold");AnimationUtility.SetAnimationClipSettings(baked,settings);
            string output="Assets/Baked/"+baked.name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if(existing!=null){EditorUtility.CopySerialized(baked,existing);Object.DestroyImmediate(baked);baked=existing;EditorUtility.SetDirty(baked);}else AssetDatabase.CreateAsset(baked,output);
            Report.Add(baked.name+" length="+baked.length+" bones="+pairs.Length+" horizontal drift="+maxDrift);
            source.SetActive(false);
            if(name.Contains("Attack")||name.Contains("Damage")||name=="CombatIdle1H01") Render(target,baked);
        }
        finally { Object.DestroyImmediate(source);Object.DestroyImmediate(target); }
    }

    static void Render(GameObject model,AnimationClip clip)
    {
        var shader=Shader.Find("Standard");var material=new Material(shader){color=new Color(.55f,.7f,.6f)};
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
        var camera=new GameObject("Preview Camera").AddComponent<Camera>();
        var light=new GameObject("Preview Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(40,-35,0);
        RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var texture=new RenderTexture(480,480,24);camera.targetTexture=texture;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.09f,.11f);camera.orthographic=true;camera.orthographicSize=1.35f;
        camera.transform.position=new Vector3(2.4f,1.8f,3.5f);camera.transform.LookAt(new Vector3(0,1,0));
        Directory.CreateDirectory("Previews");
        try {for(int i=0;i<3;i++){
            clip.SampleAnimation(model,clip.length*(.15f+i*.3f));camera.Render();
            var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(480,480,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,480,480),0,0);image.Apply();RenderTexture.active=old;
            File.WriteAllBytes("Previews/"+clip.name+"_"+i+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
        }} finally {Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(texture);Object.DestroyImmediate(material);}
    }
}
