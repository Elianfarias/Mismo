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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { IsPaused = false; releasedFrame = -1; }

        public static void Pause()
        {
            if (IsPaused) return;
            previousScale = Time.timeScale;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            IsPaused = true;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            releasedFrame = Time.frameCount;
            Time.timeScale = previousScale;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }
}
