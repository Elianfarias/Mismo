using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Enemies;
using Object=UnityEngine.Object;

public static class HumanMeleeChecks
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("HUMAN_CHECK "+message);}
    static AnimationClip Clip(string rig,string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/Human_"+rig+"_"+name+".anim");
    static PlayableGraph Graph(WeaponActionPlayback playback)=>(PlayableGraph)typeof(WeaponActionPlayback).GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
    public static void Run()
    {
        try
        {
            foreach(var rig in new[]{"Player","Goblin"})
            {
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rigs/"+rig+".fbx"));
                try
                {
                    var animator=model.GetComponent<Animator>();animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var clip=Clip(rig,"CombatDamage01");
                    Check(clip!=null&&!clip.humanMotion,rig+" hit is Generic");
                    var curves=AnimationUtility.GetCurveBindings(clip);
                    Check(curves.Length>60&&curves.All(c=>model.transform.Find(c.path)!=null),rig+" all hit paths resolve");
                    var controller=AnimatorController.CreateAnimatorControllerAtPath("Assets/"+rig+"Check.controller");
                    controller.AddParameter("Motion",AnimatorControllerParameterType.Int);controller.AddParameter("ActionTime",AnimatorControllerParameterType.Float);controller.AddParameter("PlaybackRate",AnimatorControllerParameterType.Float);
                    var state=controller.layers[0].stateMachine.AddState("Idle");state.motion=rig=="Player"?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Baked/OriginalPlayerIdle.anim"):Clip(rig,"CombatIdle1H01");controller.layers[0].stateMachine.defaultState=state;
                    animator.runtimeAnimatorController=controller;
                    var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Masks/"+rig+"UpperBody.mask");
                    var leg=model.GetComponentsInChildren<Transform>().First(t=>t.name=="UpperLeg.R");
                    var chest=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Chest");
                    using(var playback=new WeaponActionPlayback(animator,controller))
                    {
                        var graph=Graph(playback);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        playback.SetAction(null,0,0);playback.Tick(1);graph.Evaluate(0);
                        var legBefore=leg.localRotation;var chestBefore=chest.localRotation;
                        var grounded=model.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hips"||t.name.StartsWith("Foot")||t.name.StartsWith("LowerLeg")).ToArray();
                        var groundBefore=grounded.Select(t=>t.position).ToArray();
                        var upper=model.GetComponentsInChildren<Transform>().Where(t=>t.name=="Chest"||t.name=="Spine"||t.name.StartsWith("UpperArm")).ToArray();
                        var before=upper.Select(t=>t.localRotation).ToArray();
                        playback.SetAction(clip,.4f,0,mask);playback.Tick(1);graph.Evaluate(0);
                        Check(Quaternion.Angle(legBefore,leg.localRotation)<.1f,rig+" reaction mask preserves locomotion legs");
                        float change=upper.Select((t,i)=>Quaternion.Angle(before[i],t.localRotation)).Sum();
                        Debug.Log(rig+" upper body reaction change: "+change+" chest: "+Quaternion.Angle(chestBefore,chest.localRotation));
                        Check(change>3,rig+" reaction moves upper body");
                        Check(grounded.Select((t,i)=>Vector3.Distance(t.position,groundBefore[i])).Max()<.0001f,rig+" hit preserves original hips and foot height");
                        if(rig=="Player")
                        {
                            foreach(var attack in new[]{"Attack1H01_R","Attack1H01_L","DualSequence","DualCross"})
                            {
                                playback.SetAction(Clip(rig,attack),.5f,0,mask);playback.Tick(1);graph.Evaluate(0);
                                Check(grounded.Select((t,i)=>Vector3.Distance(t.position,groundBefore[i])).Max()<.0001f,attack+" preserves original hips and feet");
                            }
                            playback.SetAction(null,0,0);playback.Tick(1);graph.Evaluate(0);
                            Check(upper.Select((t,i)=>Quaternion.Angle(before[i],t.localRotation)).Max()<.1f,"Player returns to original idle after action");
                        }
                    }
                    if(rig=="Goblin")
                    {
                        using(var playback=new EnemyActionPlayback())
                        {
                            var binding=new EnemyAttackAnimation{clip=Clip(rig,"Attack1H01_R")};
                            playback.Tick(animator,binding,EnemyAttackPhase.Active,.5f,3,.5f,0,.1f,clip,.4f,mask);
                            Check(playback.ActionClip==clip,"Goblin hit supersedes attack visually");
                            playback.Tick(animator,binding,EnemyAttackPhase.Recovery,.5f,3,.8f,0,.1f);
                            Check(playback.ActionClip==binding.clip,"Goblin returns to live attack clock after hit");
                            playback.Tick(animator,null,EnemyAttackPhase.Recovery,1,4,1,0,.1f);
                            Check(playback.ActionClip==null,"Death releases attack layer");
                        }
                    }
                    else
                    {
                        var dual=Clip(rig,"DualSequence");var right=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Hand.R");var left=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Hand.L");
                        dual.SampleAnimation(model,0);var r=right.position;var l=left.position;
                        dual.SampleAnimation(model,.45f);Check(Vector3.Distance(r,right.position)>.1f,"Dual sequence moves right hand");
                        dual.SampleAnimation(model,1.5f);Check(Vector3.Distance(l,left.position)>.1f,"Dual sequence moves left hand");
                    }
                }
                finally{Object.DestroyImmediate(model);}
            }
            File.WriteAllText("checks.txt","PASS "+checks+" runtime animation graph and mask checks");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("checks.txt","FAIL "+e);EditorApplication.Exit(1);}
    }
}
