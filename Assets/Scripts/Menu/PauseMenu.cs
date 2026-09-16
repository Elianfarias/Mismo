using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using L = Mismo.Gameplay.Player.Localization.GameLanguage;

namespace Mismo.Menu
{
    [DefaultExecutionOrder(-10000)]
    public sealed class PauseMenu : MonoBehaviour
    {
        GameObject overlay;
        Button resume;
        GameObject previousSelection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlayerController>(true) != null)
                {
                    if (Object.FindAnyObjectByType<PauseMenu>() != null) return;
                    var go = new GameObject("Pause menu");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<PauseMenu>();
                    return;
                }
        }

        void Awake()
        {
            overlay = new GameObject("Pause overlay", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            overlay.SetActive(false);
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var scaler = overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            overlay.AddComponent<GraphicRaycaster>();
            var shade = MainMenuView.Box("Shade", overlay.transform, Vector2.zero, Vector2.zero, new Color(.015f,.025f,.03f,.88f));
            shade.raycastTarget = true;
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var panel = MainMenuView.Box("Pause panel", overlay.transform, new Vector2(525,140), new Vector2(550,620), new Color(.055f,.075f,.085f,.98f));
            MainMenuView.Label(panel.transform, L.Text("PAUSA"), new Vector2(50,30), new Vector2(450,65), 42, Color.white);
            MainMenuView.Label(panel.transform, L.Text("Sonido"), new Vector2(50,100), new Vector2(450,35), 24, new Color(.86f,.72f,.43f));
            var controls = MainMenuView.Box("Sound options", panel.transform, new Vector2(50,155), new Vector2(450,420), Color.clear);
            var music = MainMenuView.SliderAt(controls.transform, L.Text("Música"), 0);
            var sfx = MainMenuView.SliderAt(controls.transform, L.Text("Efectos"), 92);
            var ui = MainMenuView.SliderAt(controls.transform, L.Text("Interfaz"), 184);
            controls.gameObject.AddComponent<VolumeSettings>().Configure(AudioRuntime.Mixer, music, sfx, ui);
            resume = MainMenuView.ButtonAt(controls.transform, L.Text("Continuar [ESC]"), 305, true);
            resume.onClick.AddListener(Resume);
            foreach (var control in overlay.GetComponentsInChildren<Selectable>(true))
                control.gameObject.AddComponent<UISoundFeedback>();
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (GameplayPause.IsPaused) { Resume(); return; }
            // These panels handle Escape themselves later in the frame.
            // Opening pause here would hide them before they can close.
            if (InventoryPanel.AnyOpen || WorldMapPanel.BlocksGameplay) return;
            Open();
        }

        public void Open()
        {
            if (GameplayPause.IsPaused || InventoryPanel.AnyOpen || WorldMapPanel.BlocksGameplay) return;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Pause EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            previousSelection = EventSystem.current.currentSelectedGameObject;
            GameplayPause.Pause();
            overlay.SetActive(true);
            EventSystem.current.SetSelectedGameObject(resume.gameObject);
            GameAudio.Play(GameSound.MenuOpen);
        }

        public void Resume()
        {
            if (!GameplayPause.IsPaused) return;
            overlay.SetActive(false);
            GameplayPause.Resume();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            PlayerPrefs.Save();
            GameAudio.Play(GameSound.MenuClose);
        }

        void OnDisable() { if (overlay != null) Resume(); }
    }
}
