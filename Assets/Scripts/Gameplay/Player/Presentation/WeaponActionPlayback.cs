using Unity.Collections;
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
        private readonly Animator animator;
        private AnimationScriptPlayable captureUpperBody, upperBodyAlignment;
        private NativeArray<Quaternion> authoredTorsoRotation;
        private readonly AvatarMask familyMask;
        private AvatarMask currentMask;
        private AnimationClipPlayable action;
        private AnimationClipPlayable previous;
        private AnimationClip clip;
        private float weight;
        private bool requested;
        private float blend=.06f;
        private float actionBlend=1;
        private long executionId;
        private int segment;
        private float lastNormalized;
        public WeaponActionPlayback(Animator animator,RuntimeAnimatorController baseController,AvatarMask mask=null)
        {
            this.animator=animator;
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
            if(upperBodyAlignment.IsValid())
            {graph.Disconnect(upperBodyAlignment,0);graph.DestroyPlayable(upperBodyAlignment);upperBodyAlignment=default;}
            // Recreate only the layer node to remove an old mask when switching to full body.
            // The locomotion controller and its playback time remain intact.
            if(layers.IsValid())
            {graph.Disconnect(layers,0);graph.Disconnect(layers,1);graph.DestroyPlayable(layers);}
            if(captureUpperBody.IsValid())
            {graph.Disconnect(captureUpperBody,0);graph.DestroyPlayable(captureUpperBody);captureUpperBody=default;}
            layers=AnimationLayerMixerPlayable.Create(graph,2);
            graph.Connect(controller,0,layers,0);layers.SetInputWeight(0,1);
            layers.SetInputWeight(1,0);
            if(mask!=null)layers.SetLayerMaskFromAvatarMask(1,mask);
            output.SetSourcePlayable(layers);currentMask=mask;
            if(NeedsUpperBodyAlignment(mask))
            {
                var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
                if(spine!=null)
                {
                    if(!authoredTorsoRotation.IsCreated)authoredTorsoRotation=new NativeArray<Quaternion>(1,Allocator.Persistent);
                    var handle=animator.BindStreamTransform(spine);
                    // Capture the authored orientation before the layer mask discards Root.
                    // Both jobs stay in one chain, so the reference is sampled once per frame.
                    captureUpperBody=AnimationScriptPlayable.Create(graph,new CaptureUpperBodyJob {spine=handle,rotation=authoredTorsoRotation},1);
                    graph.Connect(mixer,0,captureUpperBody,0);captureUpperBody.SetInputWeight(0,1);
                    graph.Connect(captureUpperBody,0,layers,1);
                    upperBodyAlignment=AnimationScriptPlayable.Create(graph,new UpperBodyAlignmentJob {spine=handle,rotation=authoredTorsoRotation},1);
                    graph.Connect(layers,0,upperBodyAlignment,0);upperBodyAlignment.SetInputWeight(0,1);
                    output.SetSourcePlayable(upperBodyAlignment);
                }
            }
            if(!captureUpperBody.IsValid())graph.Connect(mixer,0,layers,1);
        }
        private bool NeedsUpperBodyAlignment(AvatarMask mask) => animator.isHuman && mask!=null &&
            !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root) &&
            !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg) && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg) &&
            mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head) &&
            mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) &&
            mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers);

        private struct CaptureUpperBodyJob : IAnimationJob
        {
            public TransformStreamHandle spine;
            [WriteOnly] public NativeArray<Quaternion> rotation;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {if(spine.IsValid(stream))rotation[0]=spine.GetRotation(stream);}
        }

        private struct UpperBodyAlignmentJob : IAnimationJob
        {
            public TransformStreamHandle spine;
            [ReadOnly] public NativeArray<Quaternion> rotation;
            public float weight;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if(weight<=0 || !spine.IsValid(stream))return;
                // Upper-body muscles are authored relative to the clip's body orientation.
                // Root masking replaces that orientation with locomotion's. Restore the
                // authored torso orientation while keeping hips, feet and root untouched.
                spine.SetRotation(stream,Quaternion.Slerp(spine.GetRotation(stream),rotation[0],weight));
            }
        }
        public void SetParameters(int motion,float actionTime,float playbackRate)
        {controller.SetInteger("Motion",motion);controller.SetFloat("ActionTime",actionTime);controller.SetFloat("PlaybackRate",playbackRate);}
        public void SetLocomotionSpeed(float speed) => controller.SetFloat("LocomotionSpeed",speed);
        public void SetAction(AnimationClip selected,float normalized,float transition) => SetAction(selected,normalized,transition,familyMask);
        public void SetAction(AnimationClip selected,float normalized,float transition,AvatarMask mask,long actionExecutionId=0,int actionSegment=0)
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
            if(clip!=selected || executionId!=actionExecutionId || segment!=actionSegment || normalized+.00001f<lastNormalized)
            {
                if(previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);}
                if(action.IsValid())
                {graph.Disconnect(mixer,0);previous=action;graph.Connect(previous,0,mixer,1);actionBlend=0;}
                else actionBlend=1;
                clip=selected;action=AnimationClipPlayable.Create(graph,clip);
                executionId=actionExecutionId;segment=actionSegment;
                action.SetApplyFootIK(false);action.SetApplyPlayableIK(false);action.SetSpeed(0);
                graph.Connect(action,0,mixer,0);
            }
            action.SetTime(Mathf.Clamp01(normalized)*clip.length);
            lastNormalized=normalized;
        }
        public void Tick(float dt)
        {
            weight=blend<=0?(requested?1:0):Mathf.MoveTowards(weight,requested?1:0,Mathf.Max(0,dt)/blend);
            actionBlend=blend<=0?1:Mathf.MoveTowards(actionBlend,1,Mathf.Max(0,dt)/blend);
            layers.SetInputWeight(1,weight);
            if(upperBodyAlignment.IsValid())
            {var job=upperBodyAlignment.GetJobData<UpperBodyAlignmentJob>();job.weight=weight;upperBodyAlignment.SetJobData(job);}
            mixer.SetInputWeight(0,actionBlend);mixer.SetInputWeight(1,1-actionBlend);
            if(actionBlend>=1 && previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);previous=default;}
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();if(authoredTorsoRotation.IsCreated)authoredTorsoRotation.Dispose();}
    }
}
