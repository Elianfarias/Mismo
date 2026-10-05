using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterWingRepair
    {
        public static bool WingWeight(BoneWeight w,HashSet<int> wings)
        {return w.weight0>.01f&&wings.Contains(w.boneIndex0)||w.weight1>.01f&&wings.Contains(w.boneIndex1)||w.weight2>.01f&&wings.Contains(w.boneIndex2)||w.weight3>.01f&&wings.Contains(w.boneIndex3);}
        public static Mesh Patch(GameObject source,VoxelRigInstance rig,Mesh current,string clipName="Fly Glide",float referenceTime=.5f)
        {
            var wings=new HashSet<int>(rig.surface.bones.Select((b,i)=>(b,i)).Where(p=>p.b.name.StartsWith("Wing")).Select(p=>p.i));
            var open=VoxelArticulationRebuilder.Build(source,rig,rig.clips.First(c=>c.name==clipName),referenceTime,
                new[]{"Root_Pelvis/Spine/Wing01_Left","Root_Pelvis/Spine/Wing01_Right"},"Root_Pelvis/Spine/Wing01_Left");
            try{return Merge(current,open,wings);}finally{Object.DestroyImmediate(open);}
        }
        static Mesh Merge(Mesh current,Mesh open,HashSet<int> wings)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var weights=new List<BoneWeight>();
            var triangles=Enumerable.Range(0,current.subMeshCount).Select(_=>new List<int>()).ToArray();
            // Keep the existing eye detail vertices last, as required by the facial checks.
            foreach(var mesh in new[]{open,current})
            {
                var v=mesh.vertices;var n=mesh.normals;var u=mesh.uv;var c=mesh.colors;var w=mesh.boneWeights;
                var remap=Enumerable.Repeat(-1,v.Length).ToArray();
                for(int start=0;start<v.Length;start+=24)
                {
                    bool wing=false;for(int i=start;i<start+24;i++)wing|=WingWeight(w[i],wings);
                    if(mesh==current?wing:!wing)continue;
                    for(int i=start;i<start+24;i++){remap[i]=positions.Count;positions.Add(v[i]);normals.Add(n[i]);uv.Add(u[i]);colors.Add(c.Length==v.Length?c[i]:Color.white);weights.Add(w[i]);}
                }
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var indices=mesh.GetTriangles(s);
                    for(int i=0;i<indices.Length;i+=3)
                    {int a=remap[indices[i]],b=remap[indices[i+1]],cIndex=remap[indices[i+2]];if(a<0||b<0||cIndex<0)continue;triangles[s].Add(a);triangles[s].Add(b);triangles[s].Add(cIndex);}
                }
            }
            var result=new Mesh{name=current.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            result.SetVertices(positions);result.SetNormals(normals);result.SetUVs(0,uv);result.SetColors(colors);result.boneWeights=weights.ToArray();result.bindposes=current.bindposes;result.subMeshCount=triangles.Length;
            for(int s=0;s<triangles.Length;s++)result.SetTriangles(triangles[s],s);result.RecalculateBounds();return result;
        }
        public static void RepairAndCheck(){Run();SoulEaterWingChecks.RunAndWorld();}
        [MenuItem("Mismo/Modelos/Corregir alas de SoulEater verde")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play Mode antes de corregir la malla.");
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterMouthRepair.SourcePath));
            var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SoulEaterGreenVariant.PrefabPath));Mesh generated=null;
            try
            {
                source.GetComponentInChildren<Animator>().enabled=false;var rig=target.GetComponent<VoxelRigInstance>();rig.animator.enabled=false;
                foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())skin.sharedMaterials=Enumerable.Repeat(rig.surface.sharedMaterial,skin.sharedMesh.subMeshCount).ToArray();
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(SoulEaterMouthRepair.MeshPath);generated=Patch(source,rig,mesh);
                EditorUtility.CopySerialized(generated,mesh);EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();Debug.Log("SOUL_WINGS_REPAIRED vertices="+mesh.vertexCount);
            }
            finally{if(generated!=null)Object.DestroyImmediate(generated);Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
        }
    }
}
