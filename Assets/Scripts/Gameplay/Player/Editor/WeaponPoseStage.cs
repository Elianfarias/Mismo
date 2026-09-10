using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mismo.Gameplay.Player.Editor
{
    // Isolated stage: sampling clips never changes the gameplay scene or its prefab.
    public sealed class WeaponPoseStage : PreviewSceneStage
    {
        protected override bool OnOpenStage()
        {
            if(!base.OnOpenStage())return false;
            var lighting=new GameObject("Preview lighting");
            SceneManager.MoveGameObjectToScene(lighting,scene);
            var light=lighting.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;
            lighting.transform.rotation=Quaternion.Euler(45,-30,0);
            return true;
        }
        protected override GUIContent CreateHeaderContent() => new GUIContent("Taller de armas");
        public GameObject Clone(GameObject prefab)
        {
            var clone=Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(clone,scene);
            foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            foreach(var camera in clone.GetComponentsInChildren<UnityEngine.Camera>(true))camera.enabled=false;
            foreach(var audio in clone.GetComponentsInChildren<AudioListener>(true))audio.enabled=false;
            return clone;
        }
    }
}
