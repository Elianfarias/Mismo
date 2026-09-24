using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Scene-local Feel playback. Render-only rotation preserves orbit, aim and collision.</summary>
    public sealed class CombatFeelPlayer : MonoBehaviour
    {
        public const int Channel = 7401;
        const int Capacity = 16;
        readonly Dictionary<MMF_Player, MMF_Player> players = new Dictionary<MMF_Player, MMF_Player>();
        UnityEngine.Camera view;
        UnityEngine.Camera shakenView;
        Quaternion originalRotation;
        Image flash;
        float shakeAge, shakeDuration, shakeAmplitude, shakeFrequency;
        float flashAge, flashDuration, flashAlpha;
        Color flashColor;
        public int CachedSequenceCount => players.Count;
        public int PlayedCount { get; private set; }
        public float ShakeAmplitude => shakeAmplitude;

        void OnEnable()
        {
            MMCameraShakeEvent.Register(Shake);
            MMCameraShakeStopEvent.Register(StopShake);
            MMFlashEvent.Register(Flash);
            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        public bool Play(MMF_Player template, Vector3 point)
        {
            if (template == null || GameplayPause.BlocksInput) return false;
            view = UnityEngine.Camera.main;
            var orbit = view != null ? view.GetComponent<Camera.ThirdPersonCamera>() : null;
            if (orbit == null || orbit.FollowTarget == null) return false;
            float intensity = 1f - Mathf.InverseLerp(6f, 16f, Vector3.Distance(point, orbit.FollowTarget.position));
            if (intensity <= 0) return false;
            if (!players.TryGetValue(template, out var player))
            {
                // Bound cached custom profiles as well as the five standard sequences.
                if (players.Count >= Capacity) return false;
                player = Instantiate(template, transform);
                player.name = template.name;
                player.Initialization();
                players.Add(template, player);
            }
            player.PlayFeedbacks(point, intensity);
            PlayedCount++;
            return true;
        }

        static bool Matches(MMChannelData data) => data != null &&
            data.MMChannelMode == MMChannelModes.Int && data.Channel == Channel;

        void Shake(float duration, float amplitude, float frequency, float x, float y, float z,
            bool infinite, MMChannelData channel, bool unscaled)
        {
            if (!Matches(channel) || GameplayPause.BlocksInput) return;
            // Values are angular degrees here, avoiding camera displacement through nearby walls.
            float strength = Mathf.Clamp(Mathf.Max(amplitude, Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) * 8f, 0, 1.2f);
            float remaining = shakeDuration > 0 ? shakeAmplitude * (1f - Mathf.Clamp01(shakeAge / shakeDuration)) : 0;
            if (strength < remaining) return;
            shakeAge = 0; shakeDuration = Mathf.Clamp(duration, .01f, .35f);
            shakeAmplitude = strength; shakeFrequency = Mathf.Clamp(frequency, 10, 40);
        }

        void StopShake(MMChannelData channel)
        { if (Matches(channel)) { shakeDuration = 0; shakeAmplitude = 0; RestoreCamera(); } }

        void Flash(Color color, float duration, float alpha, int id, MMChannelData channel, TimescaleModes timescale, bool stop)
        {
            if (!Matches(channel) || GameplayPause.BlocksInput) return;
            if (stop) { flashDuration = 0; if (flash != null) flash.color = Color.clear; return; }
            if (flash == null)
            {
                var canvasObject = new GameObject("Combat Feel flash", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
                var imageObject = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(canvasObject.transform, false);
                flash = imageObject.GetComponent<Image>(); flash.raycastTarget = false;
                flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
                flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
                flash.color = Color.clear;
            }
            flashAge = 0; flashDuration = Mathf.Clamp(duration, .01f, .3f);
            flashColor = color; flashAlpha = Mathf.Clamp(alpha * color.a, 0, .08f);
        }

        void Update()
        {
            if (GameplayPause.BlocksInput)
            {
                shakeDuration = flashDuration = 0; shakeAmplitude = 0; RestoreCamera();
                if (flash != null) flash.color = Color.clear;
                foreach (var player in players.Values) if (player != null && player.IsPlaying) player.StopFeedbacks();
                return;
            }
            // Continue during combat hit stop; a menu pause clears all feedback instead.
            shakeAge += Time.unscaledDeltaTime; flashAge += Time.unscaledDeltaTime;
            if (flash != null)
            {
                float envelope = flashDuration > 0 ? Mathf.Clamp01(1f - flashAge / flashDuration) : 0;
                flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * envelope * envelope);
            }
        }

        void BeforeRender(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (camera != view || GameplayPause.BlocksInput || shakeDuration <= 0 || shakeAge >= shakeDuration) return;
            RestoreCamera();
            float envelope = 1f - shakeAge / shakeDuration;
            float amplitude = shakeAmplitude * envelope * envelope;
            float phase = shakeAge * shakeFrequency * Mathf.PI * 2;
            originalRotation = camera.transform.rotation; shakenView = camera;
            camera.transform.rotation *= Quaternion.Euler(Mathf.Sin(phase) * amplitude, Mathf.Sin(phase * 1.37f) * amplitude * .65f, 0);
        }

        void AfterRender(ScriptableRenderContext context, UnityEngine.Camera camera)
        { if (camera == shakenView) RestoreCamera(); }
        void RestoreCamera()
        {
            if (shakenView != null) shakenView.transform.rotation = originalRotation;
            shakenView = null;
        }
        void OnDisable()
        {
            MMCameraShakeEvent.Unregister(Shake); MMCameraShakeStopEvent.Unregister(StopShake);
            MMFlashEvent.Unregister(Flash);
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            RenderPipelineManager.endCameraRendering -= AfterRender;
            RestoreCamera();
            if (flash != null) flash.color = Color.clear;
        }
    }
}
