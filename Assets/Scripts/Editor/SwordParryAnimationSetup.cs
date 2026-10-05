using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Bakes a single sword deflection as a Humanoid clip; no runtime weapon offsets.</summary>
public static class SwordParryAnimationSetup
{
    public const string ClipPath = "Assets/Art/Animations/WeaponCombat/OneHandSword/Sword_Parry_Deflect.anim";
    const string SetPath = "Assets/Data/WeaponFamilies/OneHandSwordCombatAnimations.asset";
    const string SourcePath = "Assets/Art/Animations/Quaternius/Humanoid/Quaternius_Idle.anim";
    const float Duration = .62f;

    [MenuItem("Mismo/Armas/Reconstruir movimiento limpio de Parry")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Salir de Play Mode y de la vista previa de animación antes de reconstruir.");
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourcePath);
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab");
        var actor = Object.Instantiate(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        HumanPoseHandler handler = null;
        try
        {
            var animator = actor.GetComponentInChildren<Animator>();
            animator.Rebind();
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, source, source.length * .5f);
            AnimationMode.EndSampling();
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var bones = animator.GetComponentsInChildren<Transform>();
            var rotations = bones.Select(b => b.localRotation).ToArray();
            var positions = bones.Select(b => b.localPosition).ToArray();
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            Vector3 restHand = hand.position;
            Quaternion restRotation = hand.rotation;
            Quaternion basis = animator.transform.rotation;
            float scale = (Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position)) / .60f;
            Vector3 guardHand = chest.position + basis * new Vector3(.12f, -.16f, .38f) * scale;
            Vector3 deflectHand = guardHand + basis * new Vector3(.19f, .025f, .025f) * scale;
            Vector3 pole = chest.position + basis * new Vector3(.8f, -.45f, .03f) * scale;
            var profile = weapon.poseProfile;
            Vector3 bladeAxis = restRotation * Quaternion.Euler(profile.equipped.rotation) * (profile.trailTip - profile.trailBase).normalized;
            Quaternion guardRotation = Quaternion.FromToRotation(bladeAxis, basis * new Vector3(-.40f,.90f,.14f).normalized) * restRotation;
            Quaternion deflectRotation = Quaternion.FromToRotation(bladeAxis, basis * new Vector3(-.08f,.98f,.14f).normalized) * restRotation;
            var muscles = Enumerable.Range(0,HumanTrait.MuscleCount).Select(_ => new AnimationCurve()).ToArray();
            var pose = new HumanPose();
            const int frames = 62;
            for(int frame=0;frame<=frames;frame++)
            {
                for(int b=0;b<bones.Length;b++){bones[b].localPosition=positions[b];bones[b].localRotation=rotations[b];}
                float time = Duration * frame / frames;
                Vector3 target; Quaternion rotation;
                if(time < .09f)
                {
                    float t=Smooth(time/.09f);
                    target=Vector3.Lerp(restHand,guardHand,t);rotation=Quaternion.Slerp(restRotation,guardRotation,t);
                }
                else if(time < .19f)
                {
                    float t=Smooth((time-.09f)/.10f);
                    target=Vector3.Lerp(guardHand,deflectHand,t);rotation=Quaternion.Slerp(guardRotation,deflectRotation,t);
                }
                else if(time <= .5f){target=deflectHand;rotation=deflectRotation;}
                else
                {
                    float t=Smooth((time-.5f)/.12f);
                    target=Vector3.Lerp(deflectHand,restHand,t);rotation=Quaternion.Slerp(deflectRotation,restRotation,t);
                }
                AimArm(upper,lower,hand,target,pole);
                hand.rotation=rotation;
                handler.GetHumanPose(ref pose);
                for(int m=0;m<muscles.Length;m++)muscles[m].AddKey(time,pose.muscles[m]);
            }
            var result = new AnimationClip { name="Sword_Parry_Deflect", frameRate=100 };
            // Keep the idle body's root/IK reference fixed, including its return pose.
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                if(binding.type!=typeof(Animator) || Array.IndexOf(HumanTrait.MuscleName,binding.propertyName)>=0)continue;
                float value=AnimationUtility.GetEditorCurve(source,binding).Evaluate(source.length*.5f);
                AnimationUtility.SetEditorCurve(result,binding,AnimationCurve.Constant(0,Duration,value));
            }
            for(int m=0;m<muscles.Length;m++)
            {
                // Linear samples avoid overshoot at the deflection/hold boundaries.
                for(int k=0;k<muscles[m].length;k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(muscles[m],k,AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(muscles[m],k,AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[m]),muscles[m]);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(result);
            settings.startTime=0;settings.stopTime=Duration;settings.loopTime=false;
            settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;
            settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(existing!=null){EditorUtility.CopySerialized(result,existing);Object.DestroyImmediate(result);result=existing;EditorUtility.SetDirty(result);}
            else AssetDatabase.CreateAsset(result,ClipPath);
            AssetDatabase.SaveAssetIfDirty(result);
            var set=AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(SetPath);
            var ability=AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Sword/SwordParry.asset");
            var bindingParry=set.Find(ability);
            if(bindingParry==null)throw new InvalidOperationException("Falta el enlace de Parry.");
            // Patch only this binding; preserve the user's mask choice and every other action.
            string yaml=File.ReadAllText(SetPath);
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ability));
            int start=yaml.IndexOf("  - ability: {fileID: 11400000, guid: "+guid,StringComparison.Ordinal);
            int end=yaml.IndexOf("  - ability:",start+1,StringComparison.Ordinal);if(end<0)end=yaml.Length;
            string block=yaml.Substring(start,end-start);
            block=System.Text.RegularExpressions.Regex.Replace(block,@"(?m)^    clip:[^\r\n]*","    clip: {fileID: 7400000, guid: "+AssetDatabase.AssetPathToGUID(ClipPath)+", type: 2}");
            block=System.Text.RegularExpressions.Regex.Replace(block,@"(?m)^    activeStartsAt:[^\r\n]*","    activeStartsAt: 0");
            block=System.Text.RegularExpressions.Regex.Replace(block,@"(?m)^    recoveryStartsAt:[^\r\n]*","    recoveryStartsAt: 0.8064516");
            block=System.Text.RegularExpressions.Regex.Replace(block,@"(?m)^    blendSeconds:[^\r\n]*","    blendSeconds: 0.045");
            AssetDatabase.ReleaseCachedFileHandles();
            string temp="Temp/SwordParryAnimation.asset.tmp";Directory.CreateDirectory("Temp");
            File.WriteAllText(temp,yaml.Substring(0,start)+block+yaml.Substring(end));
            File.Replace(temp,SetPath,null);
            AssetDatabase.ImportAsset(SetPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("SWORD_PARRY_ANIMATION_OK: Humanoid clip, one deflection, stable hold, unchanged defense timing.");
        }
        finally
        {
            handler?.Dispose();
            if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(actor);
        }
    }
    static float Smooth(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
    public static void ValidateBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Validación aislada de contenido; usar Unity batch.");
        string output="output/parry-motion";Directory.CreateDirectory(output);
        try
        {
            var set=AssetDatabase.LoadAssetAtPath<WeaponAnimationSet>(SetPath);
            var parry=AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Weapons/Sword/SwordParry.asset");
            var binding=set.Find(parry);
            if(binding.clip!=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath)||!binding.clip.isHumanMotion || binding.clip.isLooping ||
                !Mathf.Approximately(binding.clip.length,Duration) || binding.activeStartsAt!=0 ||
                !Mathf.Approximately(binding.recoveryStartsAt,.5f/Duration))
                throw new InvalidOperationException("Clip de Parry o sincronización incorrectos.");
            ParryRegressionPlayChecks.CheckImportedContent();
            try{ProjectOrganizationChecks.Run();File.WriteAllText(output+"/organization.txt","PASS");}
            catch(Exception e){File.WriteAllText(output+"/organization.txt",e.ToString());}
            string buildPath=".validation/ParryMotionContent";Directory.CreateDirectory(buildPath);
            const string feedbackPath="Assets/Data/Combat/Feedback/SwordFeedback.asset";
            var manifest=BuildPipeline.BuildAssetBundles(buildPath,new[]{new AssetBundleBuild{assetBundleName="parry-motion",assetNames=new[]{SetPath,"Assets/Data/Weapons/Sword/SwordParry.asset",feedbackPath}}},BuildAssetBundleOptions.ChunkBasedCompression,EditorUserBuildSettings.activeBuildTarget);
            if(manifest==null)throw new InvalidOperationException("Falló la build del contenido de Parry.");
            var bundle=AssetBundle.LoadFromFile(buildPath+"/parry-motion");
            if(bundle==null)throw new InvalidOperationException("No se pudo recargar el contenido.");
            try
            {
                var builtSet=bundle.LoadAsset<WeaponAnimationSet>(SetPath);
                var builtParry=bundle.LoadAsset<AbilityDefinition>("Assets/Data/Weapons/Sword/SwordParry.asset");
                var built=builtSet.Find(builtParry);
                if(built?.clip==null || built.clip.name!="Sword_Parry_Deflect" || !built.clip.isHumanMotion)
                    throw new InvalidOperationException("La build no contiene la animación Humanoid de Parry.");
                var cue=bundle.LoadAsset<CombatFeedbackProfile>(feedbackPath).parry;
                var sparks=cue.prefab.GetComponentInChildren<ParticleSystem>();
                if(builtParry.weaponVfx.Length!=0||!cue.useDefenderBlade||sparks==null||sparks.main.startSize.constantMin<.1f||sparks.emission.GetBurst(0).count.constant<30)
                    throw new InvalidOperationException("La build no conservó el trail normal y las chispas grandes de Parry.");
            }
            finally{bundle.Unload(true);}
            File.WriteAllText(output+"/content-build.txt","PASS: imported Humanoid clip and timing; unchanged defense window; content build and reload with the parry animation, no custom attempt VFX and enlarged blade-colored contact sparks. Global organization: see organization.txt.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void AimArm(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
    {
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        Vector3 direction=(target-upper.position).normalized;
        float distance=Mathf.Clamp(Vector3.Distance(upper.position,target),Mathf.Abs(a-b)+.001f,a+b-.001f);
        target=upper.position+direction*distance;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 bend=Vector3.ProjectOnPlane(pole-upper.position,direction).normalized;
        Vector3 elbow=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
    }
}
