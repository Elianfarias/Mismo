using UnityEngine;
using UnityEditor;
using System.IO;
public static class SwordTipCheck
{
    public static void Run()
    {
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sword.prefab"));
        float farthest=-1;Vector3 tip=Vector3.zero;var bounds=new Bounds();bool first=true;
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
        foreach(var vertex in filter.sharedMesh.vertices)
        {
            var p=model.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
            if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            if(p.sqrMagnitude>farthest){farthest=p.sqrMagnitude;tip=p;}
        }
        File.WriteAllText("sword-tip.txt","Farthest vertex: "+tip.ToString("F6")+" bounds: "+bounds);
        Object.DestroyImmediate(model);EditorApplication.Exit(0);
    }
}
