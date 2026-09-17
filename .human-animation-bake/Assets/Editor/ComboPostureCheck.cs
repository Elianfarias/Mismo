using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
public static class ComboPostureCheck
{
    public static void Run()
    {
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rigs/Player.fbx"));
        var bones=model.GetComponentsInChildren<Transform>();
        Transform B(string name)=>bones.First(t=>t.name==name);
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/OriginalPlayerIdle.anim");
        var report="";
        foreach(var name in new[]{"Attack1H01_R","Attack1H01_L"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/Human_Player_"+name+".anim");
            for(int i=0;i<=4;i++)
            {
                idle.SampleAnimation(model,0);var hips=B("Hips").localRotation;
                clip.SampleAnimation(model,clip.length*i/4f);
                report+=name+" t="+i/4f+" full torso="+(B("Neck").position-B("Spine").position).normalized;
                B("Hips").localRotation=hips;
                var direction=(B("Neck").position-B("Spine").position).normalized;
                var feet=B("Foot.R").position;
                B("Spine").rotation=Quaternion.AngleAxis(-12,model.transform.right)*B("Spine").rotation;
                var corrected=(B("Neck").position-B("Spine").position).normalized;
                float before=Mathf.Atan2(direction.z,direction.y)*Mathf.Rad2Deg;
                float after=Mathf.Atan2(corrected.z,corrected.y)*Mathf.Rad2Deg;
                if(Mathf.Abs(before-after-12)>.01f||Vector3.Distance(feet,B("Foot.R").position)>.00001f)
                    throw new System.Exception("Torso correction changed feet or incorrect angle");
                report+=" pitch="+before+" corrected="+after+" feet unchanged PASS\n";
            }
        }
        File.WriteAllText("combo-posture.txt",report);Object.DestroyImmediate(model);EditorApplication.Exit(0);
    }
}
