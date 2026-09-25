using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public static class GameplayPause
    {
        public static bool IsPaused { get; private set; }
        public static bool BlocksInput => IsPaused || releasedFrame == Time.frameCount;
        static int releasedFrame = -1;
        static float previousScale;
        static CursorLockMode previousLock;
        static bool previousVisible;
        static object pauseOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { IsPaused = false; releasedFrame = -1; pauseOwner = null; }

        public static void Pause()
        { TryPause(null); }

        public static bool TryPause(object owner)
        {
            if (IsPaused) return false;
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
