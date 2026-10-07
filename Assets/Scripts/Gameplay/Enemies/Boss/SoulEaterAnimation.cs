using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Mismo.Gameplay.Enemies
{
    // Manual clip time keeps anticipation, collision and sustained exhalation on the same clock.
    public sealed class SoulEaterAnimation : System.IDisposable
    {
        readonly Animator animator;
        readonly RuntimeAnimatorController previousController;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        AnimationClip selected;
        float blend = 1;
        public SoulEaterAnimation(Animator animator)
        {
            this.animator = animator;
            previousController = animator.runtimeAnimatorController;
            // The sample controller otherwise evaluates after Update and overwrites this pose.
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("SoulEater combat"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "SoulEater", animator).SetSourcePlayable(mixer);
            graph.Play();
        }
        public void Sample(AnimationClip clip, float normalized, float dt, bool immediate = false)
        {
            if (clip == null || !graph.IsValid()) return;
            if (selected != clip)
            {
                if (previous.IsValid()) { graph.Disconnect(mixer, 1); graph.DestroyPlayable(previous); }
                bool outgoing = current.IsValid();
                if (outgoing) { graph.Disconnect(mixer, 0); previous = current; graph.Connect(previous, 0, mixer, 1); }
                selected = clip; current = AnimationClipPlayable.Create(graph, clip);
                current.SetApplyFootIK(false); current.SetApplyPlayableIK(false); current.SetSpeed(0);
                graph.Connect(current, 0, mixer, 0); blend = outgoing && !immediate ? 0 : 1;
            }
            blend = immediate ? 1 : Mathf.MoveTowards(blend, 1, dt / .12f);
            current.SetTime(Mathf.Clamp01(normalized) * clip.length);
            mixer.SetInputWeight(0, blend); mixer.SetInputWeight(1, 1 - blend); graph.Evaluate(0);
            if (blend >= 1 && previous.IsValid()) { graph.Disconnect(mixer, 1); graph.DestroyPlayable(previous); previous = default; }
        }
        public void Dispose()
        {
            if (!graph.IsValid()) return;
            graph.Destroy();
            if (animator != null) animator.runtimeAnimatorController = previousController;
        }
    }
}
