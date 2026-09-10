using System;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponLayerChecks
    {
        private static int count;
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("LAYER_CHECK "+message);}
        public static void ConfigureAndRunBatch()
        {
            ConfigureBowMask();RunBatch();
        }
        private static void ConfigureBowMask()
        {
            var catalog=Resources.Load<ItemCatalog>("ItemCatalog");
            var bow=catalog.weapons.Single(w=>w.Id=="bow.basic");
            Check(bow.family!=null && bow.family.animations!=null,"Bow family exists");
            if(bow.family.animations.actionMask!=null)return;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            var driver=prefab.GetComponent<PlayerAnimationDriver>();
            var animator=driver.Animator;
            var spine=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Spine):
                animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.Split(':').Last()=="Spine");
            Check(spine!=null,"Torso root resolved on the actual character rig");
            string spinePath=AnimationUtility.CalculateTransformPath(spine,animator.transform);
            var mask=new AvatarMask {name="Bow Upper Body"};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,i!=(int)AvatarMaskBodyPart.Root && i!=(int)AvatarMaskBodyPart.LeftLeg && i!=(int)AvatarMaskBodyPart.RightLeg && i!=(int)AvatarMaskBodyPart.LeftFootIK && i!=(int)AvatarMaskBodyPart.RightFootIK);
            var bones=animator.GetComponentsInChildren<Transform>(true);mask.transformCount=bones.Length;
            for(int i=0;i<bones.Length;i++)
            {
                string path=AnimationUtility.CalculateTransformPath(bones[i],animator.transform);
                mask.SetTransformPath(i,path);mask.SetTransformActive(i,path==spinePath || path.StartsWith(spinePath+"/",StringComparison.Ordinal));
            }
            string assetPath=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/WeaponFamilies/BowUpperBody.mask");
            AssetDatabase.CreateAsset(mask,assetPath);bow.family.animations.actionMask=mask;
            EditorUtility.SetDirty(bow.family.animations);AssetDatabase.SaveAssets();
            Check(!mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root) && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg),"Bow mask excludes root and legs");
        }
        public static void RunBatch()
        {
            var root=new GameObject("Layer fixture");var body=new GameObject("Visual body");body.transform.SetParent(root.transform,false);
            var animator=body.AddComponent<Animator>();animator.applyRootMotion=false;
            var holster=new GameObject("Holstered weapon");holster.transform.SetParent(root.transform,false);
            var pose=new WeaponAttachmentPose{anchor=WeaponAnchor.Character,offset=new Vector3(.1f,1.2f,-.3f),rotation=new Vector3(0,0,25)};
            var upper=new GameObject("Upper");upper.transform.SetParent(body.transform,false);
            var lower=new GameObject("Lower");lower.transform.SetParent(body.transform,false);
            var baseClip=new AnimationClip();var actionClip=new AnimationClip();
            AnimationUtility.SetEditorCurve(baseClip,EditorCurveBinding.FloatCurve("Lower",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,2,1,3));
            AnimationUtility.SetEditorCurve(baseClip,EditorCurveBinding.FloatCurve("Upper",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Constant(0,1,1));
            AnimationUtility.SetEditorCurve(actionClip,EditorCurveBinding.FloatCurve("Lower",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Constant(0,1,99));
            AnimationUtility.SetEditorCurve(actionClip,EditorCurveBinding.FloatCurve("Upper",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,10,1,20));
            var controller=new AnimatorController();controller.AddLayer("Base");
            var state=controller.layers[0].stateMachine.AddState("Walk");state.motion=baseClip;controller.layers[0].stateMachine.defaultState=state;
            var mask=new AvatarMask {transformCount=3};
            mask.SetTransformPath(0,"");mask.SetTransformActive(0,false);
            mask.SetTransformPath(1,"Upper");mask.SetTransformActive(1,true);
            mask.SetTransformPath(2,"Lower");mask.SetTransformActive(2,false);
            var lowerMask=Object.Instantiate(mask);
            lowerMask.SetTransformActive(1,false);lowerMask.SetTransformActive(2,true);
            WeaponActionPlayback playback=null;
            try
            {
                for(int yaw=0;yaw<360;yaw+=45)
                {
                    body.transform.rotation=Quaternion.Euler(0,yaw,0);pose.Apply(holster.transform,root.transform,animator);
                    Check(Vector3.Distance(holster.transform.position,root.transform.position+body.transform.rotation*pose.offset)<.0001f,"Holster follows visual turn while physics root stays still: "+yaw);
                    Check(Quaternion.Angle(holster.transform.rotation,body.transform.rotation*Quaternion.Euler(pose.rotation))<.01f,"Holster orientation follows visual turn: "+yaw);
                }
                body.transform.rotation=Quaternion.identity;
                animator.runtimeAnimatorController=controller;
                playback=new WeaponActionPlayback(animator,controller,mask);
                var graph=(PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playback.SetAction(actionClip,.5f,0);playback.Tick(.1f);graph.Evaluate(.1f);
                Check(Mathf.Abs(upper.transform.localPosition.x-15)<.01f,"Masked action controls upper body");
                Check(lower.transform.localPosition.x>=2 && lower.transform.localPosition.x<3,"Action leg curves do not replace locomotion");
                float previousLeg=lower.transform.localPosition.x;graph.Evaluate(.2f);
                Check(lower.transform.localPosition.x>previousLeg+.1f,"Leg animation advances while draw pose is held");
                Check(Mathf.Abs(upper.transform.localPosition.x-15)<.01f,"Held upper body does not inherit the walking clock");
                var binding=new AbilityAnimationBinding();
                Check(binding.ResolveMask(mask)==mask,"Existing actions inherit the family mask by default");
                binding.maskMode=ActionMaskMode.FullBody;
                Check(binding.ResolveMask(mask)==null,"Full Body explicitly removes the family mask");
                playback.SetAction(actionClip,.5f,0,binding.ResolveMask(mask));playback.Tick(0);graph.Evaluate(0);
                Check(Mathf.Abs(lower.transform.localPosition.x-99)<.01f && Mathf.Abs(upper.transform.localPosition.x-15)<.01f,"Full-body exception animates both body regions using the same clip");
                binding.maskMode=ActionMaskMode.Custom;binding.customMask=lowerMask;
                playback.SetAction(actionClip,.5f,0,binding.ResolveMask(mask));playback.Tick(0);graph.Evaluate(0);
                Check(Mathf.Abs(lower.transform.localPosition.x-99)<.01f && Mathf.Abs(upper.transform.localPosition.x-1)<.01f,"Custom mask uses its own bones instead of the family mask");
                binding.customMask=null;
                Check(binding.ResolveMask(mask)==mask,"Unassigned custom mask falls back to family mask");
                binding.maskMode=ActionMaskMode.InheritFamily;
                playback.SetAction(actionClip,.5f,.1f,binding.ResolveMask(mask));playback.Tick(.05f);graph.Evaluate(0);
                Check(lower.transform.localPosition.x>previousLeg+.1f && lower.transform.localPosition.x<3,"Returning to inherited mask preserves the locomotion clock and excludes outgoing leg curves");
                playback.Tick(.05f);graph.Evaluate(0);
                Check(Mathf.Abs(upper.transform.localPosition.x-15)<.01f,"Inherited upper-body action resumes after a masked transition");
                playback.SetAction(null,0,0);playback.Tick(.1f);graph.Evaluate(.1f);
                Check(lower.transform.localPosition.x>previousLeg,"Cancelling aim preserves locomotion");
            }
            finally
            {
                playback?.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(mask);Object.DestroyImmediate(lowerMask);
                Object.DestroyImmediate(controller);Object.DestroyImmediate(baseClip);Object.DestroyImmediate(actionClip);
            }
            VerifyActualBow();
            Debug.Log("WEAPON_LAYER_PASS "+count+" checks");
        }
        private static void VerifyActualBow()
        {
            var bow=Resources.Load<ItemCatalog>("ItemCatalog").weapons.Single(w=>w.Id=="bow.basic");
            var set=bow.family.animations;
            var clip=set.Find(bow.GetAbility(AbilitySlot.Basic)).clip;
            Check(clip!=null && set.actionMask!=null,"Actual bow clip and upper-body mask are configured");
            var stage=ScriptableObject.CreateInstance<WeaponPoseStage>();StageUtility.GoToStage(stage,true);
            WeaponActionPlayback playback=null;
            try
            {
                var character=stage.Clone(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
                var animator=character.GetComponent<PlayerAnimationDriver>().Animator;
                var controller=bow.poseProfile!=null && bow.poseProfile.animations!=null ? bow.poseProfile.animations : set.locomotion!=null?set.locomotion:animator.runtimeAnimatorController;
                var bones=animator.GetComponentsInChildren<Transform>(true);
                var leg=bones.Single(t=>t.name=="UpperLeg.L");var hand=bones.Single(t=>t.name=="UpperArm.R");
                playback=new WeaponActionPlayback(animator,controller,set.actionMask);
                var graph=(PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playback.SetParameters((int)CharacterMotion.Walk,0,1);
                playback.SetAction(null,0,0);playback.Tick(0);graph.Evaluate(.3f);
                Quaternion legWalking=leg.localRotation,handWalking=hand.localRotation;
                playback.SetAction(clip,.35f,0);playback.Tick(0);graph.Evaluate(0);
                Check(Quaternion.Angle(legWalking,leg.localRotation)<.1f,"Real bow clip leaves the walking leg pose intact");
                Check(Quaternion.Angle(handWalking,hand.localRotation)>.1f,"Real bow clip replaces the walking arm pose");
                Quaternion heldHand=hand.localRotation;
                graph.Evaluate(.2f);
                Check(Quaternion.Angle(legWalking,leg.localRotation)>.1f,"Real character legs keep stepping while drawing");
                Check(Quaternion.Angle(heldHand,hand.localRotation)<.1f,"Real draw arm stays on the held pose while legs advance");
            }
            finally{playback?.Dispose();StageUtility.GoToMainStage();}
        }
    }
}
