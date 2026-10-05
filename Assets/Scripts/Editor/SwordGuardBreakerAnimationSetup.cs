using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Reproducible, in-place Humanoid overhead strike with a two-handed sword grip.</summary>
public static class SwordGuardBreakerAnimationSetup
{
    public const string ClipPath = "Assets/Art/Animations/WeaponCombat/OneHandSword/Sword_GuardBreaker_TwoHanded.anim";
    public const string SetPath = "Assets/Data/WeaponFamilies/OneHandSwordCombatAnimations.asset";
    public const string AbilityPath = "Assets/Data/Weapons/Skills/GuardBreaker.asset";
    public const float Duration = .9f;
    const string IdlePath = "Assets/Art/Animations/Quaternius/Humanoid/Quaternius_Idle.anim";

    struct KeyPose
    {
        public float time, pitch, crouch, lean, shift, step;
        public Vector3 hand;
        public KeyPose(float t, Vector3 h, float p, float c, float l, float z, float s)
        { time=t;hand=h;pitch=p;crouch=c;lean=l;shift=z;step=s; }
    }

    [MenuItem("Mismo/Armas/Reconstruir Rompeguardia a dos manos")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Salir de Play Mode y de la vista previa antes de reconstruir.");
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset");
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Skins/MageSkin.prefab"));
        actor.hideFlags=HideFlags.HideAndDontSave;
        HumanPoseHandler handler=null;
        try
        {
            var animator=actor.GetComponentInChildren<Animator>();animator.Rebind();
            AnimationMode.StartAnimationMode();AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject,idle,idle.length*.5f);AnimationMode.EndSampling();
            handler=new HumanPoseHandler(animator.avatar,animator.transform);
            var bones=animator.GetComponentsInChildren<Transform>();
            var rotations=bones.Select(b=>b.localRotation).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();
            Transform Bone(HumanBodyBones b)=>animator.GetBoneTransform(b);
            var right=Bone(HumanBodyBones.RightHand);var left=Bone(HumanBodyBones.LeftHand);
            var hips=Bone(HumanBodyBones.Hips);var spine=Bone(HumanBodyBones.Spine);var chest=Bone(HumanBodyBones.Chest);
            Vector3 restRight=right.position,restLeft=left.position;
            Quaternion restRightRotation=right.rotation,restLeftRotation=left.rotation;
            var leftFoot=Bone(HumanBodyBones.LeftFoot);var rightFoot=Bone(HumanBodyBones.RightFoot);
            Vector3 restLeftFoot=leftFoot.position,restRightFoot=rightFoot.position;
            Quaternion leftFootRotation=leftFoot.rotation,rightFootRotation=rightFoot.rotation;
            Quaternion weaponToHand=Quaternion.Inverse(Quaternion.Euler(weapon.poseProfile.equipped.rotation));
            Quaternion leftRelative=Quaternion.Inverse(restRightRotation)*restLeftRotation;
            var poses=new[]{
                new KeyPose(0,restRight,0,0,0,0,0),
                new KeyPose(.12f,new Vector3(.06f,1.02f,.32f),20,-.10f,9,-.035f,0),
                new KeyPose(.27f,new Vector3(.05f,1.84f,.22f),100,-.02f,-7,-.02f,.09f),
                new KeyPose(.34f,new Vector3(.05f,1.86f,-.14f),140,-.02f,-9,-.015f,.14f),
                new KeyPose(.40f,new Vector3(.04f,1.86f,-.14f),145,-.025f,-7,0,.18f),
                new KeyPose(.50f,new Vector3(.035f,1.16f,.49f),-5,-.12f,20,.075f,.18f),
                new KeyPose(.59f,new Vector3(.025f,.91f,.40f),-38,-.15f,27,.09f,.18f),
                new KeyPose(.70f,new Vector3(.025f,.91f,.40f),-38,-.15f,27,.09f,.18f),
                new KeyPose(Duration,restRight,0,0,0,0,0)
            };
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            var pose=new HumanPose();Quaternion previousBody=Quaternion.identity;
            const int frames=90;
            for(int frame=0;frame<=frames;frame++)
            {
                for(int b=0;b<bones.Length;b++){bones[b].localPosition=positions[b];bones[b].localRotation=rotations[b];}
                float time=Duration*frame/frames;int next=1;while(next<poses.Length-1 && time>poses[next].time)next++;
                var a=poses[next-1];var bPose=poses[next];float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(a.time,bPose.time,time));
                float weight=time<.12f?Mathf.SmoothStep(0,1,time/.12f):time>.70f?1-Mathf.SmoothStep(0,1,(time-.70f)/.20f):1;
                hips.position+=new Vector3(0,Mathf.Lerp(a.crouch,bPose.crouch,t),Mathf.Lerp(a.shift,bPose.shift,t));
                float lean=Mathf.Lerp(a.lean,bPose.lean,t);
                spine.rotation=Quaternion.AngleAxis(lean*.55f,Vector3.right)*spine.rotation;
                chest.rotation=Quaternion.AngleAxis(lean*.45f,Vector3.right)*chest.rotation;
                float shoulderLift=time<.27f?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.27f,time)):
                    1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.50f,time));
                var rightShoulder=Bone(HumanBodyBones.RightShoulder);var leftShoulder=Bone(HumanBodyBones.LeftShoulder);
                rightShoulder.rotation=Quaternion.AngleAxis(shoulderLift*45,Vector3.forward)*rightShoulder.rotation;
                leftShoulder.rotation=Quaternion.AngleAxis(-shoulderLift*45,Vector3.forward)*leftShoulder.rotation;
                var neck=Bone(HumanBodyBones.Neck);
                neck.rotation=Quaternion.AngleAxis(shoulderLift*18,Vector3.right)*neck.rotation;
                float step=Mathf.Lerp(a.step,bPose.step,t);
                Vector3 footTarget=restLeftFoot+Vector3.forward*step;
                // Lift only during the short step, then plant through the strike and hold.
                if(time>.12f&&time<.40f)footTarget+=Vector3.up*(Mathf.Sin((time-.12f)/.28f*Mathf.PI)*.06f);
                Solve(Bone(HumanBodyBones.LeftUpperLeg),Bone(HumanBodyBones.LeftLowerLeg),leftFoot,footTarget,new Vector3(-.2f,.5f,1));leftFoot.rotation=leftFootRotation;
                Solve(Bone(HumanBodyBones.RightUpperLeg),Bone(HumanBodyBones.RightLowerLeg),rightFoot,restRightFoot,new Vector3(.2f,.5f,1));rightFoot.rotation=rightFootRotation;
                Vector3 target=Vector3.Lerp(a.hand,bPose.hand,t);
                float pitch=Mathf.Lerp(a.pitch,bPose.pitch,t);
                if(next==1)pitch=bPose.pitch;
                if(next==poses.Length-1)pitch=a.pitch;
                // The blade lies in local XY (width X, thickness Z). Roll about its
                // length so that plane follows the vertical swing, with an edge leading.
                Quaternion gripRotation=Quaternion.AngleAxis(90-pitch,Vector3.right)
                    *Quaternion.AngleAxis(90,Vector3.up)*weaponToHand;
                Quaternion rightRotation=Quaternion.Slerp(restRightRotation,gripRotation,weight);
                Solve(Bone(HumanBodyBones.RightUpperArm),Bone(HumanBodyBones.RightLowerArm),right,target,new Vector3(.95f,1.1f,.1f));right.rotation=rightRotation;
                Vector3 blade=right.rotation*Quaternion.Euler(weapon.poseProfile.equipped.rotation)*Vector3.up;
                Vector3 support=right.position-blade*.13f;
                Solve(Bone(HumanBodyBones.LeftUpperArm),Bone(HumanBodyBones.LeftLowerArm),left,Vector3.Lerp(restLeft,support,weight),new Vector3(-.95f,1.1f,.1f));
                left.rotation=Quaternion.Slerp(restLeftRotation,right.rotation*leftRelative,weight);
                handler.GetHumanPose(ref pose);
                for(int m=0;m<HumanTrait.MuscleCount;m++)curves[m].AddKey(time,pose.muscles[m]);
                int r=HumanTrait.MuscleCount;Vector3 position=pose.bodyPosition;Quaternion rotation=pose.bodyRotation;
                if(frame>0&&Quaternion.Dot(previousBody,rotation)<0)rotation=new Quaternion(-rotation.x,-rotation.y,-rotation.z,-rotation.w);
                previousBody=rotation;
                curves[r].AddKey(time,position.x);curves[r+1].AddKey(time,position.y);curves[r+2].AddKey(time,position.z);
                curves[r+3].AddKey(time,rotation.x);curves[r+4].AddKey(time,rotation.y);curves[r+5].AddKey(time,rotation.z);curves[r+6].AddKey(time,rotation.w);
            }
            var result=new AnimationClip{name="Sword_GuardBreaker_TwoHanded",frameRate=100};
            string[] root={"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"};
            for(int c=0;c<curves.Length;c++)
            {
                for(int k=0;k<curves[c].length;k++)
                {AnimationUtility.SetKeyLeftTangentMode(curves[c],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[c],k,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),c<HumanTrait.MuscleCount?HumanTrait.MuscleName[c]:root[c-HumanTrait.MuscleCount]),curves[c]);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(result);
            settings.startTime=0;settings.stopTime=Duration;settings.loopTime=false;
            settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;
            settings.loopBlendPositionXZ=true;settings.loopBlendPositionY=true;settings.loopBlendOrientation=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(existing!=null){EditorUtility.CopySerialized(result,existing);Object.DestroyImmediate(result);result=existing;EditorUtility.SetDirty(result);}
            else AssetDatabase.CreateAsset(result,ClipPath);
            AssetDatabase.SaveAssetIfDirty(result);Bind();
            Debug.Log("GUARD_BREAKER_ANIMATION_OK: two-handed overhead, Humanoid, 0.9 s, impact at 0.5 s.");
        }
        finally{handler?.Dispose();if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();Object.DestroyImmediate(actor);}
    }

    static void Bind()
    {
        string yaml=File.ReadAllText(SetPath),guid=AssetDatabase.AssetPathToGUID(AbilityPath);
        int start=yaml.IndexOf("  - ability: {fileID: 11400000, guid: "+guid,StringComparison.Ordinal);
        if(start<0)throw new InvalidOperationException("Falta el enlace propio de Rompeguardia.");
        int end=yaml.IndexOf("  - ability:",start+1,StringComparison.Ordinal);if(end<0)end=yaml.Length;
        string block=yaml.Substring(start,end-start);
        string[] fields={"clip","maskMode","useAnimationMovement","activeStartsAt","recoveryStartsAt","blendSeconds","torsoUprightDegrees"};
        string[] values={"{fileID: 7400000, guid: "+AssetDatabase.AssetPathToGUID(ClipPath)+", type: 2}","1","0","0.5555556","0.7777778","0.045","0"};
        for(int i=0;i<fields.Length;i++)block=Regex.Replace(block,@"(?m)^    "+fields[i]+@":[^\r\n]*","    "+fields[i]+": "+values[i]);
        AssetDatabase.ReleaseCachedFileHandles();Directory.CreateDirectory("Temp");
        const string temp="Temp/GuardBreakerBinding.tmp";File.WriteAllText(temp,yaml.Substring(0,start)+block+yaml.Substring(end));File.Replace(temp,SetPath,null);
        AssetDatabase.ImportAsset(SetPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    }

    static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
    {
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,end.position);
        Vector3 direction=(target-upper.position).normalized;
        float distance=Mathf.Clamp(Vector3.Distance(upper.position,target),Mathf.Abs(a-b)+.001f,a+b-.001f);
        target=upper.position+direction*distance;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 bend=Vector3.ProjectOnPlane(pole-upper.position,direction).normalized;
        Vector3 middle=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,middle-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
    }
}
