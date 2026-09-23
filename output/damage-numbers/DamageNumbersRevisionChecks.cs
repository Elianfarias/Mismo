using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Combat;
public static class DamageNumbersRevisionChecks
{
 const string Key="Mismo.DamageNumberRevision";
 static IEnumerator routine;static int frame=-1;static double deadline;
 public static void RunBatch(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Register(){deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update-=Step;EditorApplication.update+=Step;}
 static void Step(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL timeout",1);return;}if(!Application.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish("PASS: actual damage, invalid inputs ignored, bounded simultaneous entries, ivory normal color, animation survives pause and expires after resume.",0);}catch(Exception e){Finish("FAIL "+e,1);}}
 static void Finish(string text,int code){SessionState.SetBool(Key,false);Directory.CreateDirectory("output/damage-numbers");File.WriteAllText("output/damage-numbers/checks.txt",text);EditorApplication.Exit(code);}
 static IEnumerator Run(){
  for(int i=0;i<12;i++)yield return null;
  DamageNumbers.Show(Vector3.zero,0,false);DamageNumbers.Show(Vector3.zero,float.NaN,false);
  if(DamageNumbers.ActiveCount!=0)throw new Exception("Invalid damage displayed");
  var target=new GameObject("Damage test");var health=target.AddComponent<Health>();health.ConfigureMaximum(200);
  health.ApplyDamage(new DamageInfo(10,null,Vector3.zero,Vector3.forward));
  if(DamageNumbers.ActiveCount!=1||health.LastDamageApplied!=10)throw new Exception("Health hit failed to create one number");
  var type=typeof(DamageNumbers);var flags=BindingFlags.NonPublic|BindingFlags.Instance;var instance=(DamageNumbers)type.GetField("instance",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  var entries=(IList)type.GetField("entries",flags).GetValue(instance);var entryType=entries[0].GetType();
  if((string)entryType.GetField("text").GetValue(entries[0])!="10")throw new Exception("Incorrect displayed damage");
  var color=(Color)entryType.GetField("color").GetValue(entries[0]);if(color.b<.85f)throw new Exception("Normal number is still yellow");
  for(int i=0;i<100;i++)DamageNumbers.Show(Vector3.zero,20,false);
  if(DamageNumbers.ActiveCount!=64)throw new Exception("Number pool exceeded limit");
  GameplayPause.Pause();yield return null;float age=(float)entryType.GetField("age").GetValue(entries[0]);
  float until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
  if((float)entryType.GetField("age").GetValue(entries[0])!=age)throw new Exception("Animation continued during pause");
  GameplayPause.Resume();until=Time.realtimeSinceStartup+1.3f;while(Time.realtimeSinceStartup<until)yield return null;
  if(DamageNumbers.ActiveCount!=0)throw new Exception("Numbers did not expire");UnityEngine.Object.Destroy(target);
 }
}
