using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using Object=UnityEngine.Object;
public class BladeDamageProbe:MonoBehaviour,IDamageReceiver {public int hits;public Vector3 point;public bool ReceiveDamage(DamageInfo d){hits++;point=d.HitPoint;return true;}}
public static class BladeChecks {
 static List<string> log=new List<string>();
 static void Check(bool pass,string s){if(!pass)throw new Exception(s);log.Add("PASS "+s);}
 public static void Run(){Directory.CreateDirectory("output/blade-contact");try{
 var actor=new GameObject("Actor");var combo=actor.AddComponent<BasicSwordCombo>();var hitObject=new GameObject("Query");hitObject.transform.SetParent(actor.transform);hitObject.AddComponent<BoxCollider>();var query=hitObject.AddComponent<AttackHitbox>();combo.Configure(query);
 // Explicit Awake for edit-mode harness; production invokes this automatically in Play Mode.
 typeof(AttackHitbox).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(query,null);
 var profile=ScriptableObject.CreateInstance<WeaponPoseProfile>();profile.trailBase=new Vector3(0,.5f,0);profile.trailTip=new Vector3(0,1.5f,0);profile.meleeTrail=true;profile.trailDuration=1;
 var loadout=actor.AddComponent<EquipmentLoadout>();loadout.ActiveDefinition=new WeaponDefinition{poseProfile=profile};var presentation=actor.AddComponent<WeaponPresentation>();var blade=new GameObject("Actual weapon");blade.transform.SetParent(actor.transform);presentation.ActiveVisual=blade.transform;
 var ability=ScriptableObject.CreateInstance<AbilityDefinition>();ability.usesSwordCombo=true;ability.comboSteps=new[]{JsonUtility.FromJson<ComboStep>("{\"duration\":1,\"shape\":2,\"bladeRadius\":0.035,\"impactStartPercent\":35,\"impactEndPercent\":65,\"damage\":10}")};
 var target=new GameObject("Touch target");target.transform.position=new Vector3(0,1,2);target.AddComponent<BoxCollider>().size=Vector3.one*.1f;var damage=target.AddComponent<BladeDamageProbe>();
 var decoy=new GameObject("In front but not touching blade");decoy.transform.position=new Vector3(0,1,.65f);decoy.AddComponent<BoxCollider>().size=Vector3.one*.2f;var miss=decoy.AddComponent<BladeDamageProbe>();
 for(int direction=-1;direction<=1;direction+=2){
 blade.transform.position=new Vector3(direction,0,2);Check(combo.RequestAttack(ability),"Start blade stroke "+direction);combo.Tick(.25f);query.EvaluateImpact();Check(damage.hits==(direction==-1?0:1),"No damage before window");
 blade.transform.position=new Vector3(-direction,0,2);combo.Tick(.25f);query.EvaluateImpact();Check(damage.hits==(direction==-1?1:2),"Sideways sword sweep connects "+direction);Check(miss.hits==0,"Forward enemy outside blade path is not damaged");
 Check(Mathf.Abs(damage.point.x-direction*.05f)<.015f,"VFX contact lies on entry surface "+direction);
 query.EvaluateImpact();Check(damage.hits==(direction==-1?1:2),"One hit per target per stroke");combo.Cancel();}
 blade.transform.position=new Vector3(-1,0,2);combo.RequestAttack(ability);combo.Tick(.2f);query.EvaluateImpact();combo.Cancel();blade.transform.position=new Vector3(1,0,2);query.EvaluateImpact();Check(damage.hits==2,"Cancelled sweep cannot damage");
 using(var ribbon=new WeaponTrailRibbon(actor.transform,null)){
 blade.transform.position=Vector3.zero;ribbon.SampleBlade(blade.transform,profile,0,true);blade.transform.position=Vector3.right;ribbon.SampleBlade(blade.transform,profile,.1f,true);int count=ribbon.SampleCount;
 blade.transform.position=Vector3.right*1.1f;ribbon.SampleBlade(blade.transform,profile,.1f,true);Check(ribbon.SampleCount==count,"Hit-stop does not append duplicate-time geometry");
 blade.transform.position=Vector3.zero;ribbon.SampleBlade(blade.transform,profile,.2f,true);Check(ribbon.Mesh.triangles.Length==6,"Direction reversal starts a new ribbon instead of a folded quad");}
 Object.DestroyImmediate(actor);Object.DestroyImmediate(target);Object.DestroyImmediate(decoy);Object.DestroyImmediate(profile);Object.DestroyImmediate(ability);
 File.WriteAllLines("output/blade-contact/checks.txt",log);EditorApplication.Exit(0);
 }catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines("output/blade-contact/checks.txt",log);Debug.LogException(e);EditorApplication.Exit(1);}}
}
