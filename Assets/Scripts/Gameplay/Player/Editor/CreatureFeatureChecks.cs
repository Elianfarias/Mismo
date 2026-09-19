using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class CreatureFeatureChecks
    {
        const string Key="Mismo.CreatureFeatureChecks";static IEnumerator routine;static double deadline;static int frame=-1,count;
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use an isolated validation project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Ground";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(60,1,60);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));Object.DestroyImmediate(player.GetComponent<RegionRespawn>());player.transform.position=Vector3.up*.1f;
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,2,-7);camera.transform.LookAt(new Vector3(0,1,4));
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,30,0);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;}
        static void Step(){if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}if(!Application.isPlaying||Time.frameCount<15||frame==Time.frameCount)return;frame=Time.frameCount;try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}catch(Exception e){Finish(false,e.ToString());}}
        static void Finish(bool pass,string text){SessionState.SetBool(Key,false);EditorApplication.update-=Step;Directory.CreateDirectory("../../output/creatures");File.WriteAllText("../../output/creatures/checks.txt",(pass?"PASS ":"FAIL ")+text);Debug.Log("CREATURE_CHECKS "+text);EditorApplication.Exit(pass?0:1);}
        static void Check(bool valid,string message){if(!valid)throw new Exception(message);count++;Debug.Log("CREATURE_CHECK "+message);}
        sealed class Failure:IProfileRepository{public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}public void Write(string payload)=>throw new IOException("Injected failure");}
        static IEnumerator Run()
        {
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var inventory=player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),new ProtectedProfileRepository(Path.GetFullPath("../../.validation/creature-"+Guid.NewGuid().ToString("N")+".mismo")));
            var book=Mismo.Core.ProjectAssets.Load<BestiaryBook>("BestiaryBook");var boar=CreatureSpecies.Find("boar");var spider=CreatureSpecies.Find("spider");
            Check(book!=null&&book.pages.Length>=4&&boar!=null,"Book and species assets load");
            Check(Array.TrueForAll(book.pages,s=>!inventory.HasSeenSpecies(s.id)),"New bestiary is empty");
            inventory.TryGrantVictory(0,null,null,"test:unseen",null,null,"golem");
            Check(!inventory.HasSeenSpecies("golem"),"Defeat count alone does not reveal an unseen species");
            Check(inventory.DiscoverSpecies(boar.id)&&inventory.HasSeenSpecies(boar.id)&&!inventory.HasSeenSpecies(spider.id),"Discovery reveals only the observed species");
            var hidden=new GameObject(spider.prefabs[0].name);hidden.transform.position=new Vector3(0,0,4);hidden.AddComponent<Mismo.Gameplay.Combat.Health>();var observer=hidden.AddComponent<Mismo.Gameplay.Enemies.EnemyProgressionReward>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,-1);wall.transform.localScale=new Vector3(4,3,.5f);Physics.SyncTransforms();
            var observe=typeof(Mismo.Gameplay.Enemies.EnemyProgressionReward).GetMethod("Observe",BindingFlags.Instance|BindingFlags.NonPublic);observe.Invoke(observer,null);
            Check(!inventory.HasSeenSpecies(spider.id),"Wall prevents bestiary discovery");Object.Destroy(wall);hidden.transform.position=new Vector3(0,0,-15);yield return null;Physics.SyncTransforms();observe.Invoke(observer,null);
            Check(!inventory.HasSeenSpecies(spider.id),"Creature behind camera remains unknown");hidden.transform.position=new Vector3(0,0,4);Physics.SyncTransforms();observe.Invoke(observer,null);
            Check(inventory.HasSeenSpecies(spider.id),"Visible unobstructed creature is discovered");Object.Destroy(hidden);yield return null;
            var field=typeof(PlayerInventory).GetField("repository",BindingFlags.Instance|BindingFlags.NonPublic);var storage=field.GetValue(inventory);field.SetValue(inventory,new Failure());
            Check(!inventory.TryGrantVictory(1,null,null,"test:failed",null,null,boar.id,true,boar.prefabs[0].name)&&inventory.SpeciesDefeats(boar.id)==0&&string.IsNullOrEmpty(inventory.CompanionSpeciesId),"Failed save grants no kill count or mount");field.SetValue(inventory,storage);
            Check(inventory.TryGrantVictory(1,null,null,"test:spider",null,null,spider.id,true,spider.prefabs[0].name)&&string.IsNullOrEmpty(inventory.CompanionSpeciesId),"Non-domesticable species cannot become mount");
            Check(inventory.TryGrantVictory(1,null,null,"test:boar1",null,null,boar.id,false,boar.prefabs[0].name)&&string.IsNullOrEmpty(inventory.CompanionSpeciesId),"Ordinary victory does not force recognition");
            Check(inventory.TryGrantVictory(1,null,null,"test:boar2",null,null,boar.id,true,boar.prefabs[0].name)&&inventory.CompanionSpeciesId==boar.id,"Recognized victory grants configured mount");
            inventory.TryGrantVictory(1,null,null,"test:boar2",null,null,boar.id,true,boar.prefabs[0].name);
            Check(inventory.SpeciesDefeats(boar.id)==2,"Retry cannot duplicate count or mount");
            string retained=inventory.CompanionPrefabName;
            inventory.TryGrantVictory(1,null,null,"test:another-boar",null,null,boar.id,true,boar.prefabs[boar.prefabs.Length-1].name);
            Check(inventory.CompanionPrefabName==retained,"A new recognition cannot replace the existing companion");
            ((IProfileRepository)storage).Read(_=>true,out string json);var saved=JsonUtility.FromJson<InventoryProfile>(json);
            Check(saved.companionSpeciesId==boar.id&&saved.seenSpecies.Contains(boar.id),"Discovery and mount persist");
            var copy=saved.Copy();copy.speciesDefeats[0].quantity++;Check(copy.speciesDefeats[0].quantity!=saved.speciesDefeats[0].quantity,"Species progress copies independently");
            var sources=new System.Collections.Generic.List<NavMeshBuildSource>();Physics.SyncTransforms();NavMeshBuilder.CollectSources(GameObject.Find("Ground").transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new System.Collections.Generic.List<NavMeshBuildMarkup>(),sources);
            var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Vector3.zero,new Vector3(60,10,60)),Vector3.zero,Quaternion.identity);var instance=NavMesh.AddNavMeshData(nav);
            var companion=player.gameObject.AddComponent<CompanionPlayer>();for(int i=0;i<15;i++)yield return null;
            var model=GameObject.Find("Companion - "+boar.displayName);Check(model!=null&&model.GetComponent<Mismo.Gameplay.Enemies.GoblinController>()==null,"Companion spawns without hostile scripts");
            typeof(CompanionPlayer).GetMethod("Mount",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(companion,null);yield return null;
            Check(companion.Riding&&!player.enabled&&!player.GetComponent<CharacterController>().enabled,"Mount transfers movement and disables player body");
            Check(player.GetComponent<CapsuleCollider>()?.enabled==true,"Mounted player remains a damage target");
            var camera=UnityEngine.Camera.main;camera.transform.position=model.transform.position+new Vector3(4,3,-6);camera.transform.LookAt(model.transform.position+Vector3.up);
            Directory.CreateDirectory("../../output/creatures");for(int i=0;i<10;i++)yield return null;ScreenCapture.CaptureScreenshot("../../output/creatures/mount.png");for(int i=0;i<10;i++)yield return null;
            typeof(CompanionPlayer).GetMethod("Dismount",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(companion,null);yield return null;
            Check(!companion.Riding&&player.GetComponent<CharacterController>().enabled,"Dismount finds a clear grounded position");player.enabled=false;
            var mineral=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings").minerals[0];var resource=Object.Instantiate(mineral.availablePrefab,new Vector3(3,0,3),Quaternion.identity);
            Check(resource.GetComponent<ResourceImpactPalette>()?.colors.Length>0,"Mineral has baked source colors");ResourceChips.Emit(resource,player.transform.position);yield return null;
            Check(Object.FindObjectsByType<ResourceChips>(FindObjectsSortMode.None).Length>0,"Resource strike emits voxel fragments");
            camera.transform.position=resource.transform.position+new Vector3(2,1.5f,-2);camera.transform.LookAt(resource.transform.position+Vector3.up*.7f);ResourceChips.Emit(resource,camera.transform.position,14);
            ScreenCapture.CaptureScreenshot("../../output/creatures/fragments.png");for(int i=0;i<10;i++)yield return null;
            var panel=player.GetComponent<InventoryPanel>()??player.gameObject.AddComponent<InventoryPanel>();yield return null;
            var page=typeof(InventoryPanel).GetNestedType("Page",BindingFlags.NonPublic);typeof(InventoryPanel).GetMethod("OpenPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new[]{Enum.Parse(page,"Bestiary")});
            Directory.CreateDirectory("../../output/creatures");for(int i=0;i<12;i++)yield return null;ScreenCapture.CaptureScreenshot("../../output/creatures/bestiary.png");for(int i=0;i<10;i++)yield return null;
            instance.Remove();Object.Destroy(nav);
        }
    }
}
