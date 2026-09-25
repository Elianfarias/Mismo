using Mismo.Gameplay.Player.World.Structures;
using UnityEngine;

// Render-only copies: previewing an enemy never runs its AI or writes player state.
public static class StructureContentPreview
{
    public static void Create(StructureInstance instance)
    {
        var root=new GameObject("Content preview");root.tag="EditorOnly";root.transform.SetParent(instance.transform,false);
        foreach(var point in instance.GetComponentsInChildren<StructureContentPoint>())
        {
            var prefab=point.data.kind==StructureContentKind.Resource?point.data.resource?.availablePrefab:point.data.prefab;if(prefab==null)continue;
            var holder=new GameObject(point.data.label);holder.transform.SetParent(root.transform,false);holder.transform.localPosition=point.transform.localPosition;holder.transform.localRotation=point.transform.localRotation;holder.transform.localScale=prefab.transform.localScale*point.data.scale;
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin)mesh=skin.sharedMesh;
                else if(renderer is MeshRenderer&&renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                if(mesh==null)continue;
                var part=new GameObject(renderer.name);part.transform.SetParent(holder.transform,false);
                var matrix=prefab.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                part.transform.localPosition=matrix.GetColumn(3);part.transform.localRotation=matrix.rotation;part.transform.localScale=matrix.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
        }
    }
}
