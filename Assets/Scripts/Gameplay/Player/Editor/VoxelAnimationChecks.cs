using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
 public static class VoxelAnimationChecks
 {
  const string Pending="Mismo.VoxelAnimationChecksV2";
  public static void Begin()
  {
   var clips=AssetDatabase.LoadAllAssetsAtPath(VoxelCharacterIntegration.ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
   foreach(CharacterMotion state in Enum.GetValues(typeof(CharacterMotion)))
   {
    var clip=clips.First(c=>c.name.Split('|').Last()==state.ToString());
    bool loop=state==CharacterMotion.Idle||state==CharacterMotion.Walk||state==CharacterMotion.Run;
    if(clip.isLooping!=loop)throw new Exception("Wrong loop setting: "+state);
   }
   var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(VoxelCharacterIntegration.ControllerPath);
   var run=clips.First(c=>c.name.Split('|').Last()=="Run");
   if(Mathf.Abs(run.length-37f/60f)>.02f)throw new Exception("Run export frame rate changed its intended duration: "+run.length);
   foreach(var item in controller.layers[0].stateMachine.states)
   {
    var clip=item.state.motion as AnimationClip;
    if(clip==null||clip.name.StartsWith("__preview")||clip.name.Split('|').Last()!=item.state.name)
     throw new Exception("State is not bound to its runtime clip: "+item.state.name);
   }
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(100,1,100);
   for(int i=0;i<20;i++)
   {
    var step=GameObject.CreatePrimitive(PrimitiveType.Cube);
    float height=2f-i*.1f;
    step.transform.position=new Vector3(10,height*.5f,i+.5f);
    step.transform.localScale=new Vector3(3,height,1);
   }
   var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCharacterIntegration.PrefabPath));
   player.GetComponent<PlayerController>().enabled=false;
   SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void StartProbe()
  {
   if(!Application.isBatchMode||!SessionState.GetBool(Pending,false))return;
   var motor=Object.FindFirstObjectByType<PlayerMotor>();
   foreach(var b in motor.GetComponents<MonoBehaviour>())
    if(b is PlayerController||b is PlayerHUD||b is MovementFeedback||b.GetType().Name=="PlayerInputReader")b.enabled=false;
   motor.gameObject.AddComponent<VoxelMotionProbe>();
  }
  public static void Finish(bool ok,string message)
  {
   SessionState.SetBool(Pending,false);Directory.CreateDirectory("Docs/Validation");
   File.WriteAllText("Docs/Validation/VoxelAnimationChecks.txt",(ok?"PASS ":"FAIL ")+message);
   Debug.Log((ok?"VOXEL_ANIMATION_CHECKS_OK ":"VOXEL_ANIMATION_CHECKS_FAILED ")+message);EditorApplication.Exit(ok?0:1);
  }
 }
 [DefaultExecutionOrder(-100)]
 public sealed class VoxelMotionProbe:MonoBehaviour
 {
  PlayerMotor motor;Animator animator;BasicSwordCombo combo;int stage;float elapsed;float total;bool first=true;
  SwordSpinAttack spin;Transform leg;Transform root;Transform head;Vector3 upright;Quaternion lastLeg;float legTravel;float stillTime;float poseSampleAt;
  Quaternion lastSpin;float spinSampleAt;float spinTravel;
  Health frontTarget;Health rearTarget;
  readonly HashSet<string> observed=new HashSet<string>();
  void Awake()
  {
   motor=GetComponent<PlayerMotor>();animator=GetComponent<PlayerAnimationDriver>().Animator;combo=GetComponentInChildren<BasicSwordCombo>();
   spin=GetComponentInChildren<SwordSpinAttack>();
   var bones=animator.GetComponentsInChildren<Transform>();
   leg=bones.First(t=>t.name=="UpperLeg.L");root=bones.First(t=>t.name=="Root");head=bones.First(t=>t.name=="Head");
   upright=root.InverseTransformDirection(Vector3.up);lastLeg=leg.localRotation;
  }
  void Update()
  {
   try
   {
    float dt=Time.deltaTime;elapsed+=dt;total+=dt;if(total>30)throw new Exception("Runtime timeout");
    if(stage>=17&&first)
    {
     motor.ResetPosition(Vector3.zero);
     motor.Visual.rotation=Quaternion.Euler(0,(stage-17)*90,0);
     foreach(var old in new[]{frontTarget,rearTarget})if(old!=null)Object.Destroy(old.gameObject);
     frontTarget=Target("Front",motor.Facing);rearTarget=Target("Rear",-motor.Facing);
     Physics.SyncTransforms();Require(combo.RequestAttack(),"Directional attack request");
     var particles=GetComponentInChildren<ParticleSystem>();
     var feedbackParticles=GetComponentsInChildren<ParticleSystem>().First(p=>p.name=="Combat Feedback Particles");
     var emitted=new ParticleSystem.Particle[128];int count=feedbackParticles.GetParticles(emitted);
     Require(count>0,"Attack particles missing");
     Require(Vector3.Dot(emitted[count-1].position-transform.position,motor.Facing)>.4f,"Attack particles spawned behind facing");
    }
    if(stage==8&&first)Require(combo.RequestAttack(),"First attack request");
    if(stage==10)
    {
     if(first)Require(combo.RequestAttack(),"Combo request");
     else if(combo.CanQueue)combo.RequestAttack();
    }
    combo.Tick(dt);
    if(stage==14&&first)Require(spin.RequestSpin(),"Spin request");
    spin.Tick(dt);
    if(stage==16&&first)motor.ResetPosition(new Vector3(10,2,.2f));
    if(stage==12)motor.RequestControlledDisplacement(Vector3.forward*dt*4);
    motor.Tick(stage==1||stage==2||stage==16?Vector3.forward:Vector3.zero,stage==2||stage==16,stage==4&&first,true,dt);
    first=false;
   }
   catch(Exception e){enabled=false;VoxelAnimationChecks.Finish(false,e.ToString());}
  }
  void LateUpdate()
  {
   try
   {
    string actual=Enum.GetNames(typeof(CharacterMotion)).FirstOrDefault(n=>animator.GetCurrentAnimatorStateInfo(0).IsName(n))??"Unknown";
    observed.Add(actual);
    if(stage==2&&elapsed>.2f&&elapsed-poseSampleAt>=.05f)
    {
     float change=Quaternion.Angle(lastLeg,leg.localRotation);legTravel+=change;
     stillTime=change<.05f?stillTime+(elapsed-poseSampleAt):0;
     poseSampleAt=elapsed;lastLeg=leg.localRotation;
     Require(stillTime<.15f,"Running pose stopped while motor moves: elapsed="+elapsed+" state="+actual+" normalized="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" speed="+animator.GetFloat("PlaybackRate")+" travel="+legTravel+" rotation="+leg.localEulerAngles+" clips="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+" length="+c.clip.length+" fps="+c.clip.frameRate+" loop="+c.clip.isLooping)));
    }
    if(stage!=2){lastLeg=leg.localRotation;poseSampleAt=0;}
    if(stage==14&&elapsed>.08f&&spin.IsActive)
    {
     Require(actual=="Spin","Spin state must play");
     Require(Vector3.Dot(root.TransformDirection(upright),Vector3.up)>.98f,"Spin tipped around horizontal axis");
     Require(head.position.y-transform.position.y>1f,"Spin head fell below standing height");
     if(elapsed-spinSampleAt>.04f)
     {
      if(spinSampleAt>0)spinTravel+=Quaternion.Angle(lastSpin,root.rotation);
      lastSpin=root.rotation;spinSampleAt=elapsed;
     }
    }
    if(stage==16&&elapsed>.65f)Require(actual=="Run","Small descending steps interrupted Run: "+actual+" speed="+motor.Speed);
    if(stage==6)
    {
     if(actual!="Land") {if(elapsed>1.2f)throw new Exception("Landing clip never entered");return;}
     Advance();return;
    }
    float duration=stage==2?4f:stage==16?2f:stage==4?.22f:stage==5?.30f:stage==8?.15f:stage==10?1.65f:.70f;
    if(elapsed<duration)return;
    string expected=stage==1?"Walk":stage==2||stage==16?"Run":stage==4?"Jump":stage==5?"Fall":stage==8?"Attack1":stage==12?"Dash":"Idle";
    Require(actual==expected,"Stage "+stage+" expected "+expected+" got "+actual+" speed="+motor.Speed);
    Require(!animator.applyRootMotion,"Root motion must remain off");
    if(stage==2)
    {
     Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime>3f,"Run did not repeat multiple cycles");
     Require(legTravel>300f,"Run leg motion missing across repeated loops");
    }
    if(stage==14)Require(spinTravel>220f,"Spin did not visibly rotate around the vertical: "+spinTravel);
    if(stage==10)foreach(var name in new[]{"Attack1","Attack2","Attack3"})Require(observed.Contains(name),"Missing combo pose "+name);
    if(stage==12)
    {
     var weapon=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Sword_E_RightHand");
     Require(weapon.bones.Any(b=>b!=null&&b.name=="Hand.R"),"Sword attached");
    }
    if(stage>=17)
    {
     Require(frontTarget.Current<frontTarget.Maximum,"Attack missed front at heading "+(stage-17)*90);
     Require(rearTarget.Current==rearTarget.Maximum,"Attack damaged rear at heading "+(stage-17)*90);
     var sword=animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Sword_E_RightHand");
     var baked=new Mesh();sword.BakeMesh(baked);
     var tip=GetComponentsInChildren<Transform>().First(t=>t.name=="Sword trail");
     float distance=baked.vertices.Min(v=>Vector3.Distance(sword.transform.TransformPoint(v),tip.position));
     Object.Destroy(baked);Require(distance<.015f,"Trail anchor detached from blade: "+distance);
    }
    if(stage==20)
    {
     VoxelAnimationChecks.Finish(true,"Run loops, steps, upright Spin, combo and jump regression; attacks damage front and spare rear at 0/90/180/270 degrees; particles follow facing; trail anchor follows sword mesh");enabled=false;return;
    }
    Advance();
   }
   catch(Exception e){enabled=false;VoxelAnimationChecks.Finish(false,e.ToString());}
  }
  void Advance(){Debug.Log("VOXEL_MOTION_STAGE_OK "+stage);stage++;elapsed=0;first=true;}
  Health Target(string name,Vector3 direction)
  {
   var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name=name;
   target.transform.position=transform.position+Vector3.up+direction;
   target.transform.localScale=Vector3.one*.25f;
   var health=target.AddComponent<Health>();target.AddComponent<DamageReceiver>();return health;
  }
  static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 }
}
