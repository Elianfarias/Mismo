using System;using System.IO;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Mismo.Gameplay.Enemies;using Mismo.Gameplay.Combat;using Object=UnityEngine.Object;
public static class EnemyMotionChecks {
 public static void Run(){Directory.CreateDirectory("output/enemy-motion");var log=new List<string>();Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception(msg);log.Add("PASS "+msg);};try{
 var actor=new GameObject("Enemy");var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx"),actor.transform);var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/VoxelLocomotion.controller");var health=actor.AddComponent<Health>();var equipment=actor.AddComponent<EnemyEquipment>();equipment.animator=animator;
 typeof(EnemyEquipment).GetMethod("OnEnable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(equipment,null);
 Func<string,AnimationClip> load=name=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Humanoid/Quaternius_"+name+".anim");
 equipment.idleClip=load("Idle");equipment.walkClip=load("Walk");equipment.runClip=load("Run");equipment.hitClip=load("Land");check(equipment.idleClip!=null&&equipment.hitClip!=null,"Fixture clips available");
 using(var playback=new EnemyActionPlayback()){
 playback.Tick(animator,null,EnemyAttackPhase.Active,0,0,0,0,.02f);check(playback.ActionClip==equipment.idleClip,"Idle override selected without weapons");
 playback.Tick(animator,null,EnemyAttackPhase.Active,0,1,0,1.6f,.02f);check(playback.ActionClip==equipment.walkClip,"Walk override selected");
 playback.Tick(animator,null,EnemyAttackPhase.Active,0,2,0,3.2f,.02f);check(playback.ActionClip==equipment.runClip,"Run override selected");
 var attack=new EnemyAttackAnimation{clip=load("Jump")};playback.Tick(animator,attack,EnemyAttackPhase.Active,.5f,3,.5f,0,.02f);check(playback.ActionClip==attack.clip,"Attack takes precedence over movement");
 health.Hit();playback.Tick(animator,attack,EnemyAttackPhase.Active,.5f,3,.5f,0,.02f);check(playback.ActionClip==equipment.hitClip,"Configured hit reaction takes precedence after damage");
 health.IsDead=true;playback.Tick(animator,null,EnemyAttackPhase.Active,1,4,1,0,.02f);check(playback.ActionClip==null,"Death is not replaced by locomotion or hit");health.IsDead=false;equipment.hitClip=null;equipment.runClip=null;
 playback.Tick(animator,null,EnemyAttackPhase.Active,0,2,0,3,.02f);check(playback.ActionClip==null,"Empty movement slot preserves controller fallback");
 }
 equipment.Release();Object.DestroyImmediate(actor);File.WriteAllLines("output/enemy-motion/checks.txt",log);EditorApplication.Exit(0);
 }catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines("output/enemy-motion/checks.txt",log);Debug.LogException(e);EditorApplication.Exit(1);}}
}
