using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NpcPoseChecks
{
    const string Output = "output/village-humanoid";
    [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        string request = Application.dataPath + "/../Temp/NpcPoseChecks.request";
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        string command;
        try { command=File.ReadAllText(request).Trim();File.Delete(request); }
        catch(IOException) { return; }
        try { if(command=="repair")CraftpixNpcRigRepair.Run(); Run(); Validate(); } catch(Exception e) { File.WriteAllText(Output+"/pose-diagnostic.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Mismo/NPCs/Verificar poses Craftpix")]
    public static void Check() { Run(); Validate(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var report = new List<string>();
        foreach(string name in Directory.GetFiles(VillageNpcIntegration.Source+"/fbx/people_unity","*.fbx").Select(Path.GetFileNameWithoutExtension).OrderBy(n=>n))
        foreach(int variant in new[]{0,1})
        {
            bool voxel=variant==1;
            string path=voxel?VillageNpcIntegration.Prefabs+"/NPC_"+name+".prefab":VillageNpcIntegration.Source+"/fbx/people_unity/"+name+".fbx";
            var preview=new PreviewRenderUtility();
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));

            preview.AddSingleGO(model);
            try
            {
                var a=model.GetComponentInChildren<Animator>();
                foreach(string pose in new[]{"Rest","Idle","Walk"})
                {
                    if(pose!="Rest")
                    {
                        a.Rebind();
                        AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Humanoid/Quaternius_"+pose+".anim").SampleAnimation(a.gameObject,.3f);
                    }
                    report.Add(name+" "+variant+" "+pose);
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftFoot})
                    {var t=a.GetBoneTransform(bone); if(t!=null) report.Add(bone+" "+t.name+" p="+t.position.ToString("F3")+" r="+t.rotation.eulerAngles.ToString("F2")+" local="+t.localRotation.ToString("F4"));}
                    var bounds=new Bounds();bool first=true;
                    var frozen=new List<GameObject>();var bakedMeshes=new List<Mesh>();
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=new Mesh();skin.BakeMesh(mesh);foreach(var v in mesh.vertices){var p=skin.transform.TransformPoint(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                        var go=new GameObject("Frozen pose");go.transform.SetParent(skin.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;frozen.Add(go);bakedMeshes.Add(mesh);
                    }
                    preview.BeginPreview(new Rect(0,0,480,640),GUIStyle.none);
                    preview.camera.fieldOfView=30;
                    preview.camera.backgroundColor=new Color(.13f,.16f,.19f);preview.camera.clearFlags=CameraClearFlags.Color;
                    preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10000;
                    preview.camera.transform.position=bounds.center+new Vector3(1,.3f,2)*bounds.size.y;
                    preview.camera.transform.LookAt(bounds.center);
                    preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;
                    preview.ambientColor=new Color(.7f,.7f,.7f);
                    preview.Render(true);var rt=(RenderTexture)preview.EndPreview();var previous=RenderTexture.active;RenderTexture.active=rt;
                    var tex=new Texture2D(480,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,480,640),0,0);tex.Apply();RenderTexture.active=previous;
                    File.WriteAllBytes(Output+"/"+name+"-"+(voxel?"voxel":"source")+"-"+pose+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
                    foreach(var go in frozen)Object.DestroyImmediate(go);foreach(var mesh in bakedMeshes)Object.DestroyImmediate(mesh);
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=true;
                }
            }
            finally {preview.Cleanup();}
        }
        File.WriteAllLines(Output+"/pose-diagnostic.txt",report);
    }
    static void Validate()
    {
        var report=new List<string>();
        foreach(string path in Directory.GetFiles(VillageNpcIntegration.Source+"/fbx/people_unity","*.fbx"))
        {
            string name=Path.GetFileNameWithoutExtension(path);
            var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/')));
            var voxel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VillageNpcIntegration.Prefabs+"/NPC_"+name+".prefab"));
            try
            {
                var a=original.GetComponentInChildren<Animator>();var b=voxel.GetComponentInChildren<Animator>();
                if(a.GetBoneTransform(HumanBodyBones.Hips).name!="Pelvis")throw new Exception(name+": incorrect Hips mapping");
                float error=0;
                foreach(string motion in new[]{"Idle","Walk"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Humanoid/Quaternius_"+motion+".anim");
                    for(int frame=0;frame<12;frame++)
                    {
                        a.Rebind();b.Rebind();float time=clip.length*frame/12f;clip.SampleAnimation(a.gameObject,time);clip.SampleAnimation(b.gameObject,time);
                        foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightFoot})
                            error=Mathf.Max(error,Quaternion.Angle(a.GetBoneTransform(bone).localRotation,b.GetBoneTransform(bone).localRotation));
                    }
                }
                if(error>.15f)throw new Exception(name+": source/voxel bone orientation mismatch "+error);
                report.Add(name+": PASS, pelvis mapped; 24 animation samples, max bone rotation error "+error.ToString("F3")+" degrees.");
            }
            finally{Object.DestroyImmediate(original);Object.DestroyImmediate(voxel);}
        }
        File.WriteAllLines(Output+"/pose-regression.txt",report);
    }
}
