using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Combat;
public static class InterruptChecks {
 static List<string> lines=new List<string>();
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static void Require(bool pass,string msg){if(!pass)throw new Exception(msg);lines.Add("PASS "+msg);}
 public static void Run(){Directory.CreateDirectory("output");GameObject g=null,source=null;GoblinSettings settings=null;try{
 g=new GameObject("Enemy");g.SetActive(false);var controller=g.AddComponent<GoblinController>();var health=g.GetComponent<Health>() ?? g.AddComponent<Health>();var combat=g.AddComponent<CombatState>();var dealer=g.AddComponent<DamageDealer>();settings=ScriptableObject.CreateInstance<GoblinSettings>();controller.Configure(settings,dealer);Set(controller,"health",health);Set(controller,"combat",combat);
 source=new GameObject("Combo source");source.AddComponent<AttackHitbox>();var damage=new DamageInfo{Source=source,Amount=10};var hit=new HitResult{Outcome=HitOutcome.Hit,HealthDamage=10};
 foreach(var state in new[]{GoblinState.Telegraph,GoblinState.Attack,GoblinState.Recovery}){
 Set(controller,"attack",new GoblinAttack());Call(controller,"Enter",state,.2f);Call(controller,"OnResolved",damage,hit);
 Require(controller.State==GoblinState.Stagger&&controller.CurrentAttack==null,"Confirmed combo cancels "+state+" and discards its action");Call(controller,"ApplyHits");Require(dealer.hits==0,"Cancelled "+state+" cannot apply pending damage");}
 Set(controller,"timer",.1f);Set(controller,"staggerResistance",10f);combat.RecoveringFromBreak=true;Call(controller,"OnResolved",damage,hit);
 Require(controller.StateProgress==0,"Next combo hit renews stun despite old stagger resistance/posture recovery");
 Call(controller,"Enter",GoblinState.Stagger,2f);Call(controller,"OnResolved",damage,hit);Require((float)typeof(GoblinController).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller)==2f,"Combo never shortens an existing longer stun");
 combat.Broken=true;Call(controller,"Enter",GoblinState.Stagger,1f);Call(controller,"OnResolved",damage,hit);Require(controller.StateProgress==0,"Posture-break state remains protected");combat.Broken=false;combat.RecoveringFromBreak=false;
 foreach(var mode in new[]{"boss","disabled","armored","blocked","zero","ranged"}){
 settings.isBoss=mode=="boss";settings.interruptibleByCombos=mode!="disabled";Set(controller,"attack",new GoblinAttack{resistComboInterrupt=mode=="armored"});Call(controller,"Enter",GoblinState.Attack,.2f);
 var d=damage;d.Ranged=mode=="ranged";var r=hit;if(mode=="blocked")r.Outcome=HitOutcome.Block;if(mode=="zero")r.HealthDamage=0;Call(controller,"OnResolved",d,r);Require(controller.State==GoblinState.Attack,mode+" does not trigger combo interruption");}
 settings.isBoss=false;settings.interruptibleByCombos=true;Set(controller,"attack",new GoblinAttack{resistComboInterrupt=true});Call(controller,"Enter",GoblinState.Recovery,.9f);Call(controller,"OnResolved",damage,hit);Require(controller.State==GoblinState.Stagger,"Armored attack becomes vulnerable during recovery");
 Call(controller,"Enter",GoblinState.Attack,.2f);Set(controller,"attack",new GoblinAttack{resistComboInterrupt=true});controller.OnAttackParried(damage);Require(controller.State==GoblinState.Stagger&&controller.CurrentAttack==null,"Parry still cancels an armored attack");
 File.WriteAllLines("output/checks.txt",lines);EditorApplication.Exit(0);
 }catch(Exception e){lines.Add("FAIL "+e);File.WriteAllLines("output/checks.txt",lines);Debug.LogException(e);EditorApplication.Exit(1);}}
}

