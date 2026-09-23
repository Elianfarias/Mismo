using UnityEngine;

namespace Mismo.Gameplay.Combat
{
    public sealed class CombatTimeFeedback : MonoBehaviour
    {
        public const float FreezeDuration = .18f;
        public const float SlowDuration = .35f;
        public const float RecoveryDuration = .22f;
        public const float SlowScale = .15f;
        static CombatTimeFeedback instance;
        float elapsed, previousScale, previousFixed, appliedScale;
        bool active;
        bool impactOnly;
        float impactDuration;

        public static void PerfectDodge() => PerfectDefense();
        public static void CancelForPause() { if (instance != null) instance.Restore(); }
        public static void PerfectDefense()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.IsPaused) return;
            if (instance == null)
                instance = new GameObject("Combat timing feedback").AddComponent<CombatTimeFeedback>();
            // A perfect defense takes priority over a shorter contact stop, without stacking clocks.
            if (instance.active && instance.impactOnly) instance.Restore();
            if (instance.active || !Mathf.Approximately(Time.timeScale, 1f)) return;
            instance.previousScale = Time.timeScale;
            instance.previousFixed = Time.fixedDeltaTime;
            instance.elapsed = 0f;
            instance.active = true;
            instance.impactOnly = false;
            instance.ApplyScale(0f);
        }

        public static void HitStop(float seconds)
        {
            if (seconds <= 0 || Mismo.Gameplay.Player.Presentation.GameplayPause.IsPaused || !Mathf.Approximately(Time.timeScale, 1f)) return;
            if (instance == null) instance = new GameObject("Combat timing feedback").AddComponent<CombatTimeFeedback>();
            if (instance.active) return;
            instance.previousScale = Time.timeScale; instance.previousFixed = Time.fixedDeltaTime;
            instance.elapsed = 0; instance.impactDuration = Mathf.Clamp(seconds, 0, .08f);
            instance.impactOnly = true; instance.active = true; instance.ApplyScale(0);
        }

        void Update()
        {
            if (Mismo.Gameplay.Player.Presentation.GameplayPause.IsPaused) return;
            if (!active) return;
            // Respect a time-scale change made by another system.
            if (!Mathf.Approximately(Time.timeScale, appliedScale)) { Restore(); return; }
            elapsed += Time.unscaledDeltaTime;
            if (impactOnly) { if (elapsed >= impactDuration) Restore(); return; }
            if (elapsed < FreezeDuration) return;
            if (elapsed < FreezeDuration + SlowDuration) { ApplyScale(SlowScale); return; }
            float recovery = (elapsed - FreezeDuration - SlowDuration) / RecoveryDuration;
            if (recovery >= 1f) { Restore(); return; }
            ApplyScale(Mathf.Lerp(SlowScale, previousScale, Mathf.SmoothStep(0f, 1f, recovery)));
        }

        void ApplyScale(float scale)
        {
            appliedScale = scale;
            Time.timeScale = scale;
            // Physics needs a positive interval even during the full stop.
            Time.fixedDeltaTime = previousFixed * (scale > 0f ? scale : 1f);
        }

        void Restore()
        {
            if (!active) return;
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = previousScale;
            Time.fixedDeltaTime = previousFixed;
            active = false;
        }

        void OnDisable() => Restore();
        void OnDestroy() { Restore(); if (instance == this) instance = null; }
    }
}
