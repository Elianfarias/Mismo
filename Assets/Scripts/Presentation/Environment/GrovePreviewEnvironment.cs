using Mismo.Gameplay.Player.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mismo.Presentation.Environment
{
    // Only used by the standalone art preview scene, never by world generation.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
    public sealed class GrovePreviewEnvironment : MonoBehaviour
    {
        [SerializeField] Color ambientFill = new Color(.24f, .29f, .3f);
        [SerializeField] Shader vegetationShader;
        SphericalHarmonicsL2 previousProbe;
        bool applied;
        GameObject windHost;
        WorldContentCatalog previewCatalog;

        void OnEnable()
        {
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            ApplyAmbient();
            if (!Application.isPlaying || vegetationShader == null || VegetationMotionWorld.Current != null) return;
            previewCatalog = ScriptableObject.CreateInstance<WorldContentCatalog>();
            previewCatalog.hideFlags = HideFlags.DontSave;
            previewCatalog.vegetationMotion = new VegetationMotionSettings { shader = vegetationShader, windStrength = .65f, playerInteraction = false };
            windHost = new GameObject("Preview wind") { hideFlags = HideFlags.DontSave };
            windHost.transform.SetParent(transform, false);
            windHost.AddComponent<VegetationMotionWorld>().Initialize(previewCatalog, null);
        }
        void ApplyAmbient()
        {
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
            if (!applied) previousProbe = RenderSettings.ambientProbe;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambientFill);
            RenderSettings.ambientProbe = probe;
            applied = true;
        }
        void ActiveSceneChanged(Scene previous, Scene next)
        {
            // RenderSettings belongs to the active scene. Never restore into another scene.
            applied = false;
            if (next == gameObject.scene) ApplyAmbient();
        }
        void OnDisable()
        {
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            if (applied && SceneManager.GetActiveScene() == gameObject.scene) RenderSettings.ambientProbe = previousProbe;
            applied = false;
            if (windHost != null) { if (Application.isPlaying) Destroy(windHost); else DestroyImmediate(windHost); }
            if (previewCatalog != null) { if (Application.isPlaying) Destroy(previewCatalog); else DestroyImmediate(previewCatalog); }
        }
    }
}
