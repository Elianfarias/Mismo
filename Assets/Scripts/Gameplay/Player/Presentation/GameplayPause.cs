using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public static class GameplayPause
    {
        public static bool IsPaused { get; private set; }
        public static bool CameraMode { get; set; }
        public static bool InterfaceHidden => CameraMode || inputOwner != null;
        static readonly HashSet<Canvas> cinematicCanvases = new HashSet<Canvas>();
        public static bool BlocksInput => IsPaused || inputOwner != null || releasedFrame == Time.frameCount;
        static object inputOwner;
        static CursorLockMode inputLock;
        static bool inputVisible;
        static int releasedFrame = -1;
        static float previousScale;
        static CursorLockMode previousLock;
        static bool previousVisible;
        static object pauseOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { RestoreCinematicCanvases(); IsPaused = false; CameraMode = false; releasedFrame = -1; pauseOwner = null; inputOwner = null; }

        // Cinematics hide gameplay UI and own input/cursor while world animation keeps running.
        public static bool TryBlockInput(object owner)
        {
            if(owner == null || BlocksInput) return false;
            inputOwner=owner;inputLock=Cursor.lockState;inputVisible=Cursor.visible;
            Canvas.preWillRenderCanvases += HideCinematicCanvases;
            HideCinematicCanvases();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;return true;
        }
        public static void ReleaseInput(object owner)
        {
            if(!ReferenceEquals(inputOwner,owner))return;
            inputOwner=null;releasedFrame=Time.frameCount;RestoreCinematicCanvases();
            Cursor.lockState=inputLock;Cursor.visible=inputVisible;
        }

        static void HideCinematicCanvases()
        {
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.enabled){cinematicCanvases.Add(canvas);canvas.enabled=false;}
        }
        static void RestoreCinematicCanvases()
        {
            Canvas.preWillRenderCanvases -= HideCinematicCanvases;
            foreach(var canvas in cinematicCanvases)if(canvas!=null)canvas.enabled=true;
            cinematicCanvases.Clear();
        }

        public static void Pause()
        { TryPause(null); }

        public static bool TryPause(object owner)
        {
            if (IsPaused || inputOwner != null) return false;
            Mismo.Gameplay.Combat.CombatTimeFeedback.CancelForPause();
            previousScale = Time.timeScale;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            IsPaused = true;
            pauseOwner = owner;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public static void Resume()
        { Resume(null); }

        public static void Resume(object owner)
        {
            if (!IsPaused || !ReferenceEquals(pauseOwner, owner)) return;
            IsPaused = false;
            pauseOwner = null;
            releasedFrame = Time.frameCount;
            Time.timeScale = previousScale;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }
}
