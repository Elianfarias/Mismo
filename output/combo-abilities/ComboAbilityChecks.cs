using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.Playables;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Object=UnityEngine.Object;
public class ComboDamageProbe : MonoBehaviour, IDamageReceiver {
 public int hits; public float damage; public Action onHit;
 public bool ReceiveDamage(DamageInfo info){hits++;damage+=info.Amount;onHit?.Invoke();return true;}
}
public static class ComboAbilityChecks {
 const string Pending="Mismo.ComboAbilityChecks";
 static readonly List<string> lines=new List<string>();
 static readonly List<Object> objects=new List<Object>();
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.update+=Tick;}
 static void Tick(){if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;SessionState.SetBool(Pending,false);Check();}
 static void Require(bool value,string message){if(!value)throw new Exception(message);lines.Add("PASS "+message);}
 static ComboStep Step(float duration=.8f,int shape=0)=>JsonUtility.FromJson<ComboStep>("{\"duration\":"+duration.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"impactStartPercent\":35,\"impactEndPercent\":65,\"inputStartPercent\":35,\"inputEndPercent\":80,\"transitionDuration\":0.06,\"recoveryDuration\":0.16,\"damage\":10,\"shape\":"+shape+",\"center\":{\"x\":0,\"y\":1,\"z\":0},\"size\":{\"x\":0.3,\"y\":0.3,\"z\":0.3},\"radius\":0.15}");
 static AbilityDefinition Ability(params ComboStep[] steps){var a=ScriptableObject.CreateInstance<AbilityDefinition>();a.usesSwordCombo=true;a.comboSteps=steps;objects.Add(a);return a;}
 static BasicSwordCombo Actor(out AttackHitbox hitbox){var g=new GameObject("Combo actor");objects.Add(g);var combo=g.AddComponent<BasicSwordCombo>();var h=new GameObject("Hitbox");h.transform.SetParent(g.transform);h.AddComponent<BoxCollider>();hitbox=h.AddComponent<AttackHitbox>();combo.Configure(hitbox);return combo;}
 static ComboDamageProbe Target(float z){var g=new GameObject("Target");objects.Add(g);g.transform.position=new Vector3(0,1,z);g.AddComponent<BoxCollider>().size=Vector3.one*.1f;return g.AddComponent<ComboDamageProbe>();}
 static void Clear(){foreach(var o in objects)if(o!=null)Object.DestroyImmediate(o);objects.Clear();}
 static void Check(){Directory.CreateDirectory("output/combo-abilities");try{
 foreach(int shape in new[]{0,1}){
 var a=Ability(Step(.8f,shape));var c=Actor(out var h);var target=Target(1);
 Require(c.RequestAttack(a),"Start configured combo shape "+shape);
 c.Tick(.24f);h.EvaluateImpact();Require(target.hits==0,"No hit during preparation");
 c.transform.position=Vector3.forward*2;c.Tick(.24f);h.EvaluateImpact();Require(target.hits==1,"Swept impact catches enemy between endpoints after movement");
 h.EvaluateImpact();c.Tick(.02f);h.EvaluateImpact();Require(target.hits==1,"No duplicate hit on another evaluation");
 c.Cancel();c.transform.position=Vector3.zero;c.Tick(.2f);h.EvaluateImpact();Require(target.hits==1&&!h.IsAttacking,"Cancellation closes impact immediately");
 Require(c.RequestAttack(a),"Same ability can restart");c.Tick(.4f);c.transform.position=Vector3.forward;h.EvaluateImpact();Require(target.hits==2,"New execution can damage same enemy again");Clear();}
 {
 var a=Ability(Step());var c=Actor(out var h);var early=Target(.2f);var middle=Target(1);var late=Target(1.8f);
 c.RequestAttack(a);c.transform.position=Vector3.forward*2;c.Tick(.8f);h.EvaluateImpact();
 Require(early.hits==0&&middle.hits==1&&late.hits==0,"One frame crossing whole window clips sweep to 35–65 percent");
 Require(!h.IsAttacking,"Final pending impact is processed then closed");Clear();}
 {
 var a=Ability(Step(2));var c=Actor(out var h);var t=Target(0);c.RequestAttack(a);c.Tick(.6f);h.EvaluateImpact();Require(t.hits==0&&!c.CanQueue,"Longer duration delays impact and queue together");c.Tick(.2f);h.EvaluateImpact();Require(t.hits==1&&c.CanQueue,"Impact and queue retain normalized timing");Clear();}
 {
 var a=Ability(Step(),Step());var c=Actor(out var h);var other=Actor(out var otherHit);c.RequestAttack(a);other.RequestAttack(a);c.Tick(.4f);h.EvaluateImpact();Require(other.CurrentStepNormalized==0&&a.comboSteps[0].Duration==.8f,"Shared asset keeps independent character state");Require(c.RequestAttack(),"Queue next step inside percentage window");c.Tick(.4f);h.EvaluateImpact();Require(c.CurrentStepNormalized==1,"Transition holds final pose without rewinding animation");c.Tick(.07f);Require(c.CurrentStepIndex==1&&c.CurrentStepNormalized==0,"Next step starts its own clock");Clear();}
 {
 var c=Actor(out var h);Require(!c.RequestAttack(Ability()),"Empty combo refused");Require(!c.RequestAttack(Ability(new ComboStep[]{null})),"Null step refused");Clear();}
 {
 var c=Actor(out var h);var t=Target(0);t.onHit=()=>c.Cancel();c.RequestAttack(Ability(Step()));c.Tick(.4f);h.EvaluateImpact();Require(t.hits==1&&!h.IsAttacking,"Parry-style cancellation inside damage callback is safe");Clear();}
 {
 var c=Actor(out var h);var t=Target(0);var extra=new GameObject("Second hurtbox");extra.transform.SetParent(t.transform,false);extra.AddComponent<BoxCollider>().size=Vector3.one*.1f;
 c.RequestAttack(Ability(Step()));c.Tick(.4f);h.EvaluateImpact();Require(t.hits==1&&t.damage==10,"Multiple colliders on one actor receive one hit");Clear();}
 RootMotionIntegration();
 File.WriteAllLines("output/combo-abilities/checks.txt",lines);EditorApplication.Exit(0);
 }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines("output/combo-abilities/checks.txt",lines);Debug.LogException(e);EditorApplication.Exit(1);}}
 static void RootMotionIntegration(){
 var actor=new GameObject("Animated combo actor");objects.Add(actor);var body=actor.AddComponent<CharacterController>();body.height=1.8f;body.radius=.3f;body.center=Vector3.up*.9f;
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(20,.2f,20);
 var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath),actor.transform);
 var motor=actor.AddComponent<PlayerMotor>();var settings=ScriptableObject.CreateInstance<MovementSettings>();objects.Add(settings);motor.Configure(settings,model.transform);motor.AnimationMovementRequested=()=>true;
 var c=actor.AddComponent<BasicSwordCombo>();var child=new GameObject("Query");child.transform.SetParent(actor.transform);child.AddComponent<BoxCollider>();var h=child.AddComponent<AttackHitbox>();c.Configure(h);
 var t=Target(1.8f);var animator=model.GetComponent<Animator>();var receiver=model.AddComponent<PlayerAnimationMotion>();receiver.Move=delta=>motor.ApplyAnimationDisplacement(delta);animator.applyRootMotion=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(QuaterniusHumanoidLocomotion.ControllerPath);
 var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/DoubleL/FBX Unity/One Hand Up/Attack_A/1Hand_Up_Attack_A_1.fbx").OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));
 using(var playback=new WeaponActionPlayback(animator,controller)){
 var graph=(PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);playback.SetParameters(0,0,1);playback.SetLocomotionSpeed(0);
 c.RequestAttack(Ability(new ComboStep()));float firstHit=-1;
 for(int frame=0;frame<=48;frame++){
 if(frame>0)c.Tick(1f/60);motor.Tick(Vector3.zero,false,false,false,1f/60);
 playback.SetAction(clip,c.CurrentStepNormalized,0,null,1,0);playback.Tick(1f/60);graph.Evaluate(1f/60);h.EvaluateImpact();
 if(t.hits>0&&firstHit<0)firstHit=c.CurrentStepNormalized;
 }
 Require(actor.transform.position.z>.8f,"Real sword clip moves the character during configured combo");
 Require(t.hits==1&&firstHit>=.35f&&firstHit<=.65f,"Enemy initially out of reach receives one hit after animated advance (progress="+firstHit+")");
 }
 Clear();
 }

}
