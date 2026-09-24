using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Combat;
public static class EliteAuraRevisionChecks
{
 const string Key="Mismo.EliteAuraRevision";
 static IEnumerator routine;static int frame=-1;static double deadline;
 public static void RunBatch(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Register(){deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update-=Step;EditorApplication.update+=Step;}
 static void Step(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL timeout",1);return;}if(!Application.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish("PASS: elite aura and particles clear on death, remain hidden on subsequent frames, restore on revival and respect disable/re-enable.",0);}catch(Exception e){Finish("FAIL "+e,1);}}
 static void Finish(string text,int code){SessionState.SetBool(Key,false);Directory.CreateDirectory("output/elite-aura");File.WriteAllText("output/elite-aura/checks.txt",text);EditorApplication.Exit(code);}
 static IEnumerator Run(){
  for(int i=0;i<5;i++)yield return null;
  var target=new GameObject("Elite aura test");var health=target.AddComponent<Health>();
  target.GetComponent<ActorCombatVisuals>().enabled=false;
  var elite=target.AddComponent<Mismo.Gameplay.Enemies.GoblinEliteVisual>();
  var ring=target.GetComponentInChildren<LineRenderer>();var particles=target.GetComponentInChildren<ParticleSystem>();
  if(!ring.enabled||!particles.isPlaying)throw new Exception("Living elite indicator missing");
  health.ApplyDamage(new DamageInfo(1000,null,Vector3.zero,Vector3.forward));
  if(ring.enabled||particles.isPlaying||particles.particleCount!=0)throw new Exception("Elite aura survived death");
  yield return null;yield return null;
  if(ring.enabled||particles.isPlaying)throw new Exception("Dead aura restarted");
  health.Revive();
  if(!ring.enabled||!particles.isPlaying)throw new Exception("Revival did not restore aura");
  elite.enabled=false;if(ring.enabled||particles.isPlaying)throw new Exception("Disabled elite aura still visible");
  elite.enabled=true;if(!ring.enabled||!particles.isPlaying)throw new Exception("Re-enabled elite aura missing");
  health.ApplyDamage(new DamageInfo(1000,null,Vector3.zero,Vector3.forward));
  elite.enabled=false;elite.enabled=true;
  if(ring.enabled||particles.isPlaying)throw new Exception("Re-enabling dead elite restored aura");
  UnityEngine.Object.Destroy(target);
 }
}
