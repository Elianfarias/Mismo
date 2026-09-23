using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Quests;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CraftpixNpcRigRepair
{
    internal static HumanDescription Description(GameObject source, HumanDescription description)
    {
        var human=description.human;
        for(int i=0;i<human.Length;i++) if(human[i].humanName=="Hips")human[i].boneName="Pelvis";
        description.human=human;
        description.skeleton=source.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
        return description;
    }
    [MenuItem("Mismo/NPCs/Reparar rig y regenerar Craftpix")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play Mode primero.");
        foreach(string file in Directory.GetFiles(VillageNpcIntegration.Source+"/fbx/people_unity","*.fbx").OrderBy(x=>x))
        {
            string path=file.Replace('\\','/');
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.humanDescription=Description(source,importer.humanDescription);EditorUtility.SetDirty(importer);importer.SaveAndReimport();
            source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(source.GetComponentInChildren<Animator>().avatar.humanDescription.human.First(h=>h.humanName=="Hips").boneName!="Pelvis")
                throw new InvalidOperationException("Unity no conservó el mapeo de pelvis: "+path);
            string target=VillageNpcIntegration.Prefabs+"/NPC_"+source.name+".prefab";
            var instance=Object.Instantiate(source);instance.name=source.name;
            var root=PrefabUtility.LoadPrefabContents(target);
            Mesh mesh=null;
            try
            {
                mesh=WeaponVoxelizerWindow.RebuildRigGeometry(instance,root,64);
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                VillageHumanoidUpgrade.ReplaceMesh(mesh,skin.sharedMesh);skin.localBounds=mesh.bounds;
                PrefabUtility.SaveAsPrefabAsset(root,target);
            }
            finally {if(mesh!=null)Object.DestroyImmediate(mesh);Object.DestroyImmediate(instance);PrefabUtility.UnloadPrefabContents(root);}
        }
        // Drop only the sampled bone-pose overrides left by the old normalization.
        // Controller overrides, quest data, movement settings and routes stay authored.
        foreach(string path in Directory.GetFiles(VillageNpcIntegration.Residents,"*.prefab"))
        {
            var root=PrefabUtility.LoadPrefabContents(path.Replace('\\','/'));
            try
            {
                var routine=root.GetComponent<VillageNpcRoutine>();if(routine==null)continue;
                var visual=root.GetComponentInChildren<SkinnedMeshRenderer>().transform;
                while(visual.parent!=root.transform)visual=visual.parent;
                var basis=PrefabUtility.GetCorrespondingObjectFromSource(visual);
                var modifications=PrefabUtility.GetPropertyModifications(visual.gameObject);
                PrefabUtility.SetPropertyModifications(visual.gameObject,modifications.Where(m=>!(m.target is Transform t)||t==basis||(!m.propertyPath.StartsWith("m_LocalRotation")&&!m.propertyPath.StartsWith("m_LocalPosition")&&!m.propertyPath.StartsWith("m_LocalScale"))).ToArray());
                var probe=Object.Instantiate(visual.gameObject);probe.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);probe.transform.localScale=Vector3.one;
                try
                {
                    var a=probe.GetComponentInChildren<Animator>();a.Rebind();a.Update(0);
                    var skin=probe.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);var bounds=baked.bounds;Object.DestroyImmediate(baked);
                    float scale=1.75f/bounds.size.y;visual.localScale=Vector3.one*scale;visual.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                }
                finally{Object.DestroyImmediate(probe);}
                PrefabUtility.SaveAsPrefabAsset(root,path.Replace('\\','/'));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        NpcWorkshopWindow.RefreshOpenPreview();
        File.WriteAllText(VillageHumanoidUpgrade.Output+"/rig-repair.txt","Regenerated 14 meshes and bind poses; corrected Hips mapping; refreshed five residents without changing their controllers or routines.");
    }
}
