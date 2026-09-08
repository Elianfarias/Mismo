using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>NavMesh local del prototipo, construido sólo con su geometría estática.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class GoblinNavigation : MonoBehaviour
    {
        [SerializeField] private Transform geometry;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        public void Configure(Transform root) => geometry = root;

        private void OnEnable()
        {
            if (geometry == null) return;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(geometry, ~0, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            foreach (Collider item in geometry.GetComponentsInChildren<Collider>()) bounds.Encapsulate(item.bounds);
            bounds.Expand(4f);
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds,
                Vector3.zero, Quaternion.identity);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
            else Debug.LogError("No se pudo construir la navegación de la arena.", this);
        }

        private void OnDisable()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }
    }
}
