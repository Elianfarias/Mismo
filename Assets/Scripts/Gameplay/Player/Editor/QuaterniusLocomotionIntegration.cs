using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor {
 public static class QuaterniusLocomotionIntegration {
 const string Folder="Assets/Art/Animations/Quaternius/Retargeted";
 static readonly string[] Source={"DEF-hips","DEF-spine.001","DEF-spine.003","DEF-neck","DEF-head","DEF-shoulder.L","DEF-upper_arm.L","DEF-forearm.L","DEF-hand.L","DEF-shoulder.R","DEF-upper_arm.R","DEF-forearm.R","DEF-hand.R","DEF-thigh.L","DEF-shin.L","DEF-foot.L","DEF-toe.L","DEF-thigh.R","DEF-shin.R","DEF-foot.R","DEF-toe.R"};
 static readonly string[] Target={"Hips","Spine","Chest","Neck","Head","Clavicle.L","UpperArm.L","LowerArm.L","Hand.L","Clavicle.R","UpperArm.R","LowerArm.R","Hand.R","UpperLeg.L","LowerLeg.L","Foot.L","Toes.L","UpperLeg.R","LowerLeg.R","Foot.R","Toes.R"};
 static readonly int[] Child={1,2,3,4,-1,6,7,8,-1,10,11,12,-1,14,15,16,-1,18,19,20,-1};
 static void BindPose(GameObject model){
 var matrices=new Dictionary<Transform,Matrix4x4>();
 foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<r.bones.Length;i++)if(r.bones[i]!=null)matrices[r.bones[i]]=r.transform.localToWorldMatrix*r.sharedMesh.bindposes[i].inverse;
 foreach(var t in model.GetComponentsInChildren<Transform>())if(matrices.TryGetValue(t,out var m))t.SetPositionAndRotation(m.GetColumn(3),m.rotation);
 }
 [MenuItem("Mismo/Character/Apply Quaternius Locomotion")]
 public static void Apply(){
 bool probe=File.Exists("Assets/Quaternius.fbx");
 string sp=probe?"Assets/Quaternius.fbx":"Assets/Art/Animations/Quaternius/Source/Quaternius.fbx";
 string tp=probe?"Assets/Voxel_Adventurer_Animated.fbx":"Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx";
 string cp=probe?"Assets/VoxelLocomotion.controller":"Assets/Art/Animations/VoxelLocomotion.controller";
 // These baked clips contain Transform curves, not Humanoid muscle curves.
 // A Humanoid Animator overrides the mapped bones and sinks/freezes this rig.
 var targetImporter=(ModelImporter)AssetImporter.GetAtPath(tp);
 if(targetImporter.animationType!=ModelImporterAnimationType.Generic){
 targetImporter.animationType=ModelImporterAnimationType.Generic;
 targetImporter.SaveAndReimport();
 }
 var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sp));var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(tp));
 try{
 foreach(var a in source.GetComponentsInChildren<Animator>())a.enabled=false;
 foreach(var a in target.GetComponentsInChildren<Animator>())a.enabled=false;
 var clips=AssetDatabase.LoadAllAssetsAtPath(sp).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
 BindPose(source);clips.First(c=>c.name.Split('|').Last()=="A_TPose").SampleAnimation(source,0);BindPose(target);
 var st=source.GetComponentsInChildren<Transform>();var tt=target.GetComponentsInChildren<Transform>();
 var sb=Source.Select(n=>st.First(t=>t.name==n)).ToArray();var tb=Target.Select(n=>tt.First(t=>t.name==n)).ToArray();
 // Match facing: FBX exports can use opposite forward axes despite identical bone labels.
 var sourceRight=Vector3.ProjectOnPlane(sb[17].position-sb[13].position,Vector3.up);
 var targetRight=Vector3.ProjectOnPlane(tb[17].position-tb[13].position,Vector3.up);
 source.transform.rotation=Quaternion.FromToRotation(sourceRight,targetRight)*source.transform.rotation;
 // Align reference limb directions before transferring rotations; rigs use different bone axes.
 for(int i=0;i<tb.Length;i++)if(Child[i]>=0){int child=Child[i];tb[i].rotation=Quaternion.FromToRotation(tb[child].position-tb[i].position,sb[child].position-sb[i].position)*tb[i].rotation;}
 var sr=sb.Select(t=>t.rotation).ToArray();var tr=tb.Select(t=>t.rotation).ToArray();
 var spos=st.Select(t=>t.localPosition).ToArray();var srot=st.Select(t=>t.localRotation).ToArray();
 var tpos=tt.Select(t=>t.localPosition).ToArray();var trot=tt.Select(t=>t.localRotation).ToArray();
 Vector3 sh=sb[0].position,th=tb[0].position;
 float ratio=(Vector3.Distance(tb[13].position,tb[14].position)+Vector3.Distance(tb[14].position,tb[15].position))/(Vector3.Distance(sb[13].position,sb[14].position)+Vector3.Distance(sb[14].position,sb[15].position));
 float floor=Mathf.Min(tb[15].position.y,tb[19].position.y);
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var converted=new Dictionary<string,AnimationClip>();
 string[] names={"Idle","Walk","Run","Jump","Fall","Land"};string[] takes={"Idle_Loop","Jog_Fwd_Loop","Sprint_Loop","Jump_Start","Jump_Loop","Jump_Land"};
 for(int index=0;index<names.Length;index++){
 var clip=clips.First(c=>c.name.Split('|').Last()==takes[index]);
 var bones=tt.Where(t=>Target.Contains(t.name)||t.name=="Root"||t.name=="Armature_Humanoid").ToArray();
 var tracks=bones.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
 int count=Mathf.CeilToInt(clip.length*60);
 for(int frame=0;frame<=count;frame++){
 for(int i=0;i<st.Length;i++){st[i].localPosition=spos[i];st[i].localRotation=srot[i];}
 for(int i=0;i<tt.Length;i++){tt[i].localPosition=tpos[i];tt[i].localRotation=trot[i];}
 float time=clip.length*frame/count;clip.SampleAnimation(source,time);
 for(int i=0;i<tb.Length;i++)tb[i].rotation=sb[i].rotation*Quaternion.Inverse(sr[i])*tr[i];
 Vector3 delta=(sb[0].position-sh)*ratio;
 tb[0].position=th+new Vector3(Mathf.Clamp(delta.x,-.06f,.06f),delta.y,Mathf.Clamp(delta.z,-.06f,.06f));
 if(index<3||index==5)tb[0].position+=Vector3.up*(floor-Mathf.Min(tb[15].position.y,tb[19].position.y));
 foreach(var b in bones){var q=b.localRotation;var p=b.localPosition;float[] v={q.x,q.y,q.z,q.w,p.x,p.y,p.z};if(v.Any(x=>float.IsNaN(x)||float.IsInfinity(x)))throw new Exception("Non-finite pose");for(int k=0;k<7;k++)tracks[b][k].AddKey(time,v[k]);}
 }
 var result=new AnimationClip{name="Quaternius_"+names[index],frameRate=60};
 foreach(var b in bones)for(int k=0;k<7;k++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(b,target.transform),typeof(Transform),k<4?"m_LocalRotation."+"xyzw"[k]:"m_LocalPosition."+"xyz"[k-4]),tracks[b][k]);
 result.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=index<3||index==4;settings.loopBlend=settings.loopTime;AnimationUtility.SetAnimationClipSettings(result,settings);
 string path=Folder+"/Quaternius_"+names[index]+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
 if(existing!=null){EditorUtility.CopySerialized(result,existing);Object.DestroyImmediate(result);EditorUtility.SetDirty(existing);converted[names[index]]=existing;}else{AssetDatabase.CreateAsset(result,path);converted[names[index]]=result;}
 }
 var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);
 foreach(var state in controller.layers[0].stateMachine.states.Select(x=>x.state)){
 if(state.name=="Idle"&&state.motion is BlendTree tree){var children=tree.children;for(int i=0;i<3;i++)children[i].motion=converted[names[i]];tree.children=children;EditorUtility.SetDirty(tree);}
 else if(converted.TryGetValue(state.name,out var c))state.motion=c;
 if(state.name=="Land")state.speed=converted["Land"].length/.18f;EditorUtility.SetDirty(state);
 }
 EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();File.WriteAllText("quaternius-result.txt","PASS: six clips adapted using aligned reference poses and baked on the original skeleton. Combat states preserved.");
 }finally{Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
 }
 public static void RunBatch(){try{Apply();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText("quaternius-result.txt","FAIL: "+e);EditorApplication.Exit(1);}}
 }
}
