using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class RespawnMountChecks
    {
        const string Key="Mismo.RespawnMountChecks";
        static IEnumerator routine;static int frame=-1,count;static double deadline;
        static string Save=>Path.GetFullPath("../../.validation/respawn-check.mismo");
        static string Output=>Path.GetFullPath("../../output/respawn-mounts");
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project.");
            TranslationTables.Import();
            var storage=new ProtectedProfileRepository(Save);storage.Read(_=>true,out _);
            storage.Write(File.ReadAllText("../../.validation/respawn-profile.json"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Ground";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(60,1,60);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));player.transform.position=Vector3.up*.1f;
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4,3,-7);camera.transform.LookAt(Vector3.up);
            new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/RespawnValidation.unity");
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void StorageOverride(){if(SessionState.GetBool(Key,false))typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new ProtectedProfileRepository(Save));}
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Step;}
        static void Step(){if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}if(!Application.isPlaying||Time.frameCount<15||frame==Time.frameCount)return;frame=Time.frameCount;try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}catch(Exception e){Finish(false,e.ToString());}}
        static void Finish(bool pass,string text){SessionState.SetBool(Key,false);EditorApplication.update-=Step;typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);File.WriteAllText(Path.Combine(Output,"checks.txt"),(pass?"PASS ":"FAIL ")+text);Debug.Log("RESPAWN_MOUNTS "+(pass?"PASS ":"FAIL ")+text);EditorApplication.Exit(pass?0:1);}
        static void Check(bool value,string text){if(!value)throw new Exception(text);count++;Debug.Log("RESPAWN_CHECK "+text);}
        static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static IEnumerator Run()
        {
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var inventory=player.GetComponent<PlayerInventory>();
            Check(inventory.IsReady&&!inventory.HasSaveProblem&&inventory.MaterialCount("MAT-03")==8,"Existing saved inventory loads with empty optional arrays");
            Check(inventory.Mounts.Length==1&&inventory.CompanionSpeciesId=="boar","Legacy mount migrates into permanent collection");
            var expected=inventory.Mounts[0];
            var loadout=player.GetComponent<EquipmentLoadout>();
            Check(inventory.TryGrantMaterial("MAT-02",2),"Recovered profile remains writable");
            int wood=inventory.MaterialCount("MAT-02"),weapons=inventory.Count;
            var previous=player.gameObject;player.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,null,Vector3.zero,Vector3.forward));
            while(previous!=null)yield return null;
            for(int i=0;i<10;i++)yield return null;
            player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;inventory=player.GetComponent<PlayerInventory>();loadout=player.GetComponent<EquipmentLoadout>();
            Check(inventory.IsReady&&!inventory.HasSaveProblem&&inventory.MaterialCount("MAT-02")==wood&&inventory.Count==weapons,"Death and real scene reload preserve inventory");
            Check(inventory.Mounts.Length==1&&inventory.Mounts[0].id==expected.id,"Death preserves owned mount identity");
            var herb=Resources.Load<GatheringSettings>("GatheringSettings").herb;
            var resource=new GameObject("Harvest after respawn").AddComponent<GatheringNode>();resource.transform.position=player.transform.position+Vector3.right;resource.Configure(herb,"respawn-test-node");
            for(int i=0;i<3;i++)yield return null;
            int herbs=inventory.MaterialCount("MAT-01");Check(resource.Available&&resource.Complete(inventory)&&inventory.MaterialCount("MAT-01")>herbs,"Gathering works after respawn");Object.Destroy(resource.gameObject);
            var sources=new List<NavMeshBuildSource>();Physics.SyncTransforms();NavMeshBuilder.CollectSources(GameObject.Find("Ground").transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Vector3.zero,new Vector3(60,10,60)),Vector3.zero,Quaternion.identity);var instance=NavMesh.AddNavMeshData(nav);
            var companion=player.GetComponent<CompanionPlayer>();
            Check(inventory.SummonMount(expected.id),"Owned mount can be summoned");
            for(int i=0;i<20;i++)yield return null;
            var model=Field<GameObject>(companion,"companion");Check(model!=null,"Summoned companion spawns");
            Check(inventory.DismissMount()&&inventory.Mounts.Length==1&&!inventory.CompanionSummoned,"Dismissal retains collection");yield return null;
            Check(Field<GameObject>(companion,"companion")==null,"Dismissal removes only world representation");
            var boar=CreatureSpecies.Find("boar");var alternative=boar.prefabs.First(p=>p.name!=expected.prefabName);
            Check(inventory.TryGrantVictory(0,null,null,"mount-second",null,null,boar.id,true,alternative.name)&&inventory.Mounts.Length==2,"Another recognized variant joins collection");
            Check(inventory.SelectedMountId==expected.id,"New recognition does not replace selected mount");
            var other=inventory.Mounts.First(m=>m.id!=expected.id);
            Check(inventory.SummonMount(other.id)&&inventory.Mounts.Length==2,"Switching mount retains both ownership records");
            for(int i=0;i<20;i++)yield return null;
            model=Field<GameObject>(companion,"companion");Check(model!=null,"Selected variant spawns");
            var visual=Field<Transform>(companion,"mountVisual");
            model.transform.position+=Vector3.up*.15f;Invoke(companion,"GroundVisual");
            Check(Mathf.Abs(visual.position.y)<.04f,"Visual compensates height above physical ground");
            Invoke(companion,"Mount");Check(companion.Riding,"Mount transfers control");
            var animator=Field<Animator>(companion,"mountAnimator");Invoke(companion,"TickAnimation",4f);animator.Update(.1f);
            Check(animator.GetInteger("Motion")==2&&animator.GetFloat("PlaybackRate")>0,"Mounted movement has nonzero animation playback");
            animator.Update(.3f);float time=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;animator.Update(.3f);
            Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime>time,"Movement animation advances");
            Check(!inventory.DismissMount()&&!inventory.SummonMount(expected.id),"Mounted player must dismount before replacing companion");
            player.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,null,Vector3.zero,Vector3.forward));
            Check(!companion.Riding&&player.GetComponent<CharacterController>().enabled,"Mounted death restores player controller state");
            previous=player.gameObject;while(previous!=null)yield return null;for(int i=0;i<10;i++)yield return null;
            player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;inventory=player.GetComponent<PlayerInventory>();
            Check(inventory.Mounts.Length==2&&inventory.SelectedMountId==other.id&&!inventory.HasSaveProblem,"Collection and selection survive mounted death");
            Check(inventory.DismissMount(),"Can store companion after mounted respawn");
            var panel=player.GetComponent<InventoryPanel>();var page=typeof(InventoryPanel).GetNestedType("Page",BindingFlags.NonPublic);Invoke(panel,"OpenPage",Enum.Parse(page,"Mounts"));
            for(int i=0;i<15;i++)yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"collection.png"));for(int i=0;i<30;i++)yield return null;
            instance.Remove();Object.Destroy(nav);
        }
    }
}
