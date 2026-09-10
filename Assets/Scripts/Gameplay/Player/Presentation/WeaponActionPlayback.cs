using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Layers arbitrary action clips over the character controller, without adding weapon states to it.</summary>
    public sealed class WeaponActionPlayback : System.IDisposable
    {
        private PlayableGraph graph;
        private AnimatorControllerPlayable controller;
        private AnimationMixerPlayable mixer;
        private AnimationLayerMixerPlayable layers;
        private AnimationPlayableOutput output;
        private readonly AvatarMask familyMask;
        private AvatarMask currentMask;
        private AnimationClipPlayable action;
        private AnimationClipPlayable previous;
        private AnimationClip clip;
        private float weight;
        private bool requested;
        private float blend=.06f;
        private float actionBlend=1;
        public WeaponActionPlayback(Animator animator,RuntimeAnimatorController baseController,AvatarMask mask=null)
        {
            graph=PlayableGraph.Create("Weapon family animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            controller=AnimatorControllerPlayable.Create(graph,baseController);
            mixer=AnimationMixerPlayable.Create(graph,2);
            familyMask=mask;
            output=AnimationPlayableOutput.Create(graph,"Character",animator);
            ConfigureMask(mask);
            graph.Play();
        }
        private void ConfigureMask(AvatarMask mask)
        {
            // Recreate only the layer node to remove an old mask when switching to full body.
            // The locomotion controller and its playback time remain intact.
            if(layers.IsValid())
            {graph.Disconnect(layers,0);graph.Disconnect(layers,1);graph.DestroyPlayable(layers);}
            layers=AnimationLayerMixerPlayable.Create(graph,2);
            graph.Connect(controller,0,layers,0);layers.SetInputWeight(0,1);
            graph.Connect(mixer,0,layers,1);layers.SetInputWeight(1,0);
            if(mask!=null)layers.SetLayerMaskFromAvatarMask(1,mask);
            output.SetSourcePlayable(layers);currentMask=mask;
        }
        public void SetParameters(int motion,float actionTime,float playbackRate)
        {controller.SetInteger("Motion",motion);controller.SetFloat("ActionTime",actionTime);controller.SetFloat("PlaybackRate",playbackRate);}
        public void SetLocomotionSpeed(float speed) => controller.SetFloat("LocomotionSpeed",speed);
        public void SetAction(AnimationClip selected,float normalized,float transition) => SetAction(selected,normalized,transition,familyMask);
        public void SetAction(AnimationClip selected,float normalized,float transition,AvatarMask mask)
        {
            requested=selected!=null;blend=Mathf.Max(0,transition);
            if(!requested)return;
            if(currentMask!=mask)
            {
                // An outgoing clip must never gain access to bones excluded by its own mask.
                if(previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);previous=default;}
                if(action.IsValid()){graph.Disconnect(mixer,0);graph.DestroyPlayable(action);action=default;}
                clip=null;weight=0;actionBlend=1;ConfigureMask(mask);
            }
            if(clip!=selected)
            {
                if(previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);}
                if(action.IsValid())
                {graph.Disconnect(mixer,0);previous=action;graph.Connect(previous,0,mixer,1);actionBlend=0;}
                else actionBlend=1;
                clip=selected;action=AnimationClipPlayable.Create(graph,clip);
                action.SetApplyFootIK(false);action.SetApplyPlayableIK(false);action.SetSpeed(0);
                graph.Connect(action,0,mixer,0);
            }
            action.SetTime(Mathf.Clamp01(normalized)*clip.length);
        }
        public void Tick(float dt)
        {
            weight=blend<=0?(requested?1:0):Mathf.MoveTowards(weight,requested?1:0,Mathf.Max(0,dt)/blend);
            actionBlend=blend<=0?1:Mathf.MoveTowards(actionBlend,1,Mathf.Max(0,dt)/blend);
            layers.SetInputWeight(1,weight);
            mixer.SetInputWeight(0,actionBlend);mixer.SetInputWeight(1,1-actionBlend);
            if(actionBlend>=1 && previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);previous=default;}
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();}
    }
}
