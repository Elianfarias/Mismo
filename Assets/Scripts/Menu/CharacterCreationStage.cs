using System;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mismo.Menu
{
    /// <summary>A reversible camera move within the authored main-menu diorama.</summary>
    public sealed class CharacterCreationStage : IDisposable
    {
        readonly Camera camera;
        readonly Transform cottage;
        readonly Vector3 homePosition;
        readonly Quaternion homeRotation;
        readonly float homeSize, homeFov, homeNear;
        readonly GameObject root;
        Vector3 fromPosition, toPosition;
        Quaternion fromRotation, toRotation;
        float fromSize, toSize, fromFov, toFov, elapsed, duration, yaw;
        bool returning;
        public GameObject Model { get; private set; }
        public bool Moving { get; private set; }
        public bool Ready => !Moving && !returning && Model != null;
        public float Progress => duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
        public Camera Camera => camera;
        public Transform Cottage => cottage;

        public CharacterCreationStage(Scene scene, Camera selectedCamera = null, Transform selectedCottage = null)
        {
            camera = selectedCamera;
            cottage = selectedCottage;
            // Older saved menu scenes need no regeneration. Resolve only inside this scene.
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                if(camera == null)
                    foreach(var candidate in sceneRoot.GetComponentsInChildren<Camera>())
                        if(candidate.enabled && candidate.targetTexture == null && candidate.CompareTag("MainCamera")) camera = candidate;
                if(cottage == null)
                    foreach(var candidate in sceneRoot.GetComponentsInChildren<Transform>())
                        if(candidate.name == "Cottage_Exterior") { cottage = candidate; break; }
            }
            if(camera == null || cottage == null) throw new InvalidOperationException("Falta la cámara o la casita del menú.");
            homePosition = camera.transform.position; homeRotation = camera.transform.rotation;
            homeSize = camera.orthographicSize; homeFov = camera.fieldOfView; homeNear = camera.nearClipPlane;
            root = new GameObject("Character at cottage");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = cottage.TransformPoint(new Vector3(0, .015f, -3.4f));
            yaw = cottage.eulerAngles.y + 210;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            camera.nearClipPlane = Mathf.Min(homeNear, .1f);
        }
        public void Show(string skin)
        {
            var prefab = PlayerAppearance.Prefab(skin);
            if(prefab == null) throw new InvalidOperationException("No se pudo cargar esta apariencia.");
            if(Model != null) { Model.SetActive(false); Release(Model); }
            Model = Object.Instantiate(prefab, root.transform, false);
            Model.transform.localPosition = Vector3.zero;
            Model.transform.localScale = prefab.transform.localScale * Mathf.Abs(cottage.lossyScale.x);
            Model.transform.localRotation = Quaternion.identity;
            foreach(var t in Model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = cottage.gameObject.layer;
            var animator = Model.GetComponent<Animator>();
            animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
        }
        public void Enter()
        {
            returning = false;
            float scale = Mathf.Abs(cottage.lossyScale.x);
            // Approach from the pond side so the blossom tree and low wall never cover the character.
            toRotation = Quaternion.Euler(12, cottage.eulerAngles.y + 30, 0);
            toSize = 2.5f * scale; toFov = 36;
            // Keep the character to the right of the small creation panel.
            var focus = root.transform.position + Vector3.up * (1.25f * scale)
                - toRotation * Vector3.right * (toSize * camera.aspect * .30f);
            float distance = camera.orthographic ? 10 * scale : toSize / Mathf.Tan(toFov * Mathf.Deg2Rad * .5f);
            toPosition = focus - toRotation * Vector3.forward * distance;
            BeginMove(1.65f);
        }
        public void Return()
        {
            returning = true; toPosition = homePosition; toRotation = homeRotation;
            toSize = homeSize; toFov = homeFov; BeginMove(1.15f);
        }
        void BeginMove(float seconds)
        {
            fromPosition = camera.transform.position; fromRotation = camera.transform.rotation;
            fromSize = camera.orthographicSize; fromFov = camera.fieldOfView;
            elapsed = 0; duration = seconds; Moving = true;
        }
        public void Advance(float seconds)
        {
            if(!Moving || camera == null) return;
            elapsed += Mathf.Max(0, seconds);
            float t = Progress; t = t * t * t * (t * (t * 6 - 15) + 10);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t));
            camera.orthographicSize = Mathf.Lerp(fromSize, toSize, t); camera.fieldOfView = Mathf.Lerp(fromFov, toFov, t);
            if(Progress >= 1) Moving = false;
        }
        public void Rotate(float horizontalPixels)
        {
            if(!Ready) return;
            yaw -= horizontalPixels * .5f;
            // Rotate outside the animated hierarchy; clips must not overwrite the drag pose.
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        public void Dispose()
        {
            if(camera != null)
            {
                camera.transform.SetPositionAndRotation(homePosition, homeRotation);
                camera.orthographicSize = homeSize; camera.fieldOfView = homeFov; camera.nearClipPlane = homeNear;
            }
            if(root != null) Release(root);
            Model = null;
        }
        static void Release(Object value){if(Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);}
    }
}
