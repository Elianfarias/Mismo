using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    // Bind once after placement/scale; rebind only when a harvested visual respawns.
    public sealed class VegetationMotionBinding : MonoBehaviour
    {
        public WorldAssetKind kind;
        readonly HashSet<Renderer> bound = new HashSet<Renderer>();
        static readonly int Shape = Shader.PropertyToID("_VegetationShape");
        static readonly int Response = Shader.PropertyToID("_VegetationResponse");
        public static bool Supports(WorldAssetKind value) => value == WorldAssetKind.Tree || value == WorldAssetKind.Grass || value == WorldAssetKind.Bush || value == WorldAssetKind.Flower;
        public static GameObject Attach(GameObject go, WorldAssetEntry entry)
        {
            if (!entry.disableVegetationMotion && Supports(entry.kind))
            {
                var binding = go.GetComponent<VegetationMotionBinding>() ?? go.AddComponent<VegetationMotionBinding>();
                binding.kind = entry.kind;
            }
            return go;
        }
        void Start() => Apply();
        public static void FreezeVisual(GameObject visual)
        {
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                var shape = block.GetVector(Shape); shape.w = 0;
                block.SetVector(Shape, shape); block.SetVector(Response, Vector4.zero);
                renderer.SetPropertyBlock(block);
            }
        }
        public void Apply()
        {
            var world = VegetationMotionWorld.Current;
            if (world == null || world.Settings?.shader == null) return;
            bound.RemoveWhere(r => r == null);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (bound.Contains(renderer)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var source = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < source.Length; i++)
                {
                    var material = world.MaterialFor(source[i]);
                    if (material == null) continue;
                    source[i] = material; changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = source;
                var bounds = filter.sharedMesh.bounds;
                bool tree = kind == WorldAssetKind.Tree;
                float amplitude = tree ? .085f : kind == WorldAssetKind.Bush ? .055f : .045f;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetVector(Shape, new Vector4(bounds.min.y, Mathf.Max(.01f, bounds.size.y), tree ? .58f : 0, amplitude));
                block.SetVector(Response, new Vector4(tree ? 0 : 1, 0, 0, 0));
                renderer.SetPropertyBlock(block);
                // Deformation is in world metres. Expand only runtime renderer bounds.
                Vector3 scale = renderer.transform.lossyScale;
                bounds.Expand(new Vector3(2 / Mathf.Max(.01f, Mathf.Abs(scale.x)), 2 / Mathf.Max(.01f, Mathf.Abs(scale.y)), 2 / Mathf.Max(.01f, Mathf.Abs(scale.z))));
                renderer.localBounds = bounds;
                bound.Add(renderer);
            }
        }
    }
}
