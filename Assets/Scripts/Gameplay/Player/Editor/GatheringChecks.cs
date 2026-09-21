using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class GatheringChecks
    {
        const string Key="Mismo.GatheringChecks";
        static IEnumerator routine;static int frame=-1,count;static double deadline;
        static string Output=>Path.GetFullPath("../../output/gathering");
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use an isolated validation project.");
            GatheringAssets.Create();TranslationTables.Import();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(80,1,80);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
            Object.DestroyImmediate(player.GetComponent<RegionRespawn>());player.transform.position=Vector3.up*.2f;
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(5,4,-8);camera.transform.LookAt(Vector3.up);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,30,0);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<15||Time.frameCount==frame)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks passed");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string text){SessionState.SetBool(Key,false);EditorApplication.update-=Step;Time.timeScale=1;File.WriteAllText(Path.Combine(Output,"checks.txt"),(pass?"PASS ":"FAIL ")+text);Debug.Log("GATHERING_"+(pass?"PASS ":"FAIL ")+text);EditorApplication.Exit(pass?0:1);}
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);count++;Debug.Log("GATHERING_CHECK "+text);}
        sealed class FailingStorage:IProfileRepository
        {
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}
            public void Write(string payload)=>throw new IOException("Intentional storage failure for transaction test.");
        }
        static IEnumerator Run()
        {
            Application.runInBackground=true;
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var inventory=player.gameObject.AddComponent<PlayerInventory>();var path=Path.Combine(Output,Guid.NewGuid().ToString("N")+".mismo");
            inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),new ProtectedProfileRepository(path));
            var gathering=player.gameObject.AddComponent<GatheringPlayer>();var health=player.GetComponent<Health>();
            var settings=Mismo.Core.ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            Check(inventory.IsReady&&settings.recipes.Length==2,"Surface assets and recipes load");
            if(settings.minerals!=null&&settings.minerals.Length>0)
            {
                Check(settings.minerals.Length==30&&settings.Mineral(int.MinValue)!=null,"Mineral variants load and accept negative world seeds");
                var rose=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/Nature/flower_rose.prefab");
                Check(settings.herb.availablePrefab==rose&&inventory.Material(MaterialCatalog.Herb).pickupPrefab==rose,"Harvestable rose and inventory ingredient share the existing model");
            }
            var herb=Object.Instantiate(settings.herb);herb.harvestSeconds=.1f;herb.regenerationSeconds=30;
            var node=GatheringDistribution.Place(herb,"test:herb",new Vector3(0,0,1.5f),null);
            yield return null;yield return null;
            Check(node.Available&&node.InRange(player.transform),"Available node is reachable");
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.G));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check(gathering.IsHarvesting,"G starts gathering through the normal interaction");
            for(int i=0;i<25;i++)yield return null;
            Check(!node.Available&&inventory.MaterialCount(MaterialCatalog.Herb)==1,"Completion grants material and exhausts node once");
            Check(!node.Complete(inventory)&&inventory.MaterialCount(MaterialCatalog.Herb)==1,"Repeated completion cannot duplicate harvest");
            var repository=new ProtectedProfileRepository(path);repository.Read(_=>true,out var json);var saved=JsonUtility.FromJson<InventoryProfile>(json);
            Check(saved.version==6&&saved.harvestedNodes.Exists(n=>n.id=="test:herb"&&n.readyAt>saved.worldPlaySeconds),"Harvest and deadline persist in the same transaction");
            Object.Destroy(node.gameObject);yield return null;
            node=GatheringDistribution.Place(herb,"test:herb",new Vector3(0,0,1.5f),null);yield return null;yield return null;
            Check(!node.Available,"Reloading node preserves depletion");
            double clock=inventory.WorldPlaySeconds;Time.timeScale=0;for(int i=0;i<5;i++)yield return null;
            Check(Math.Abs(inventory.WorldPlaySeconds-clock)<.001,"Paused world does not advance regeneration clock");Time.timeScale=1;
            typeof(PlayerInventory).GetField("worldClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(inventory,clock+40);
            player.transform.position=Vector3.right*15;yield return null;yield return null;
            Check(node.Available,"Expired node restores when player is away");player.transform.position=Vector3.up*.2f;
            herb.harvestSeconds=3;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.G));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check(gathering.IsHarvesting,"Second harvest can begin after regeneration");
            health.ApplyDamage(new DamageInfo(5,null,player.transform.position,Vector3.forward));
            Check(!gathering.IsHarvesting&&node.Available&&inventory.MaterialCount(MaterialCatalog.Herb)==1,"Damage cancels gathering without granting or exhausting");
            var limits=InventorySettings.Current;int columns=limits.backpackColumns,rows=limits.backpackRows;limits.backpackColumns=1;limits.backpackRows=1;
            Check(!node.Complete(inventory)&&node.Available&&inventory.MaterialCount(MaterialCatalog.Herb)==1,"Full grid preserves node and does not grant partial rewards");limits.backpackColumns=columns;limits.backpackRows=rows;
            var storageField=typeof(PlayerInventory).GetField("repository",BindingFlags.Instance|BindingFlags.NonPublic);var workingStorage=storageField.GetValue(inventory);storageField.SetValue(inventory,new FailingStorage());
            Check(!inventory.TryHarvest("failed:node",herb,new Dictionary<string,int>{{MaterialCatalog.Herb,1}})&&inventory.NodeReadyAt("failed:node")==0&&inventory.MaterialCount(MaterialCatalog.Herb)==1,"Failed save cannot grant harvest or exhaust node");storageField.SetValue(inventory,workingStorage);
            var station=new GameObject("Test workbench").AddComponent<CraftingStation>();station.transform.position=new Vector3(1,0,0);station.recipes=settings.recipes;
            Check(!inventory.TryCraft(settings.recipes[0],inventory.EquippedId(0),station),"Insufficient ingredients do not craft");
            Check(inventory.TryGrantMaterial(MaterialCatalog.Herb,1),"Second herb is acquired");
            Check(inventory.TryCraft(settings.recipes[0],inventory.EquippedId(0),station)&&inventory.MaterialCount(MaterialCatalog.Herb)==0&&inventory.MaterialCount("MAT-05")==1,"Exact recipe consumes ingredients and grants one salve");
            health.Revive();Check(!inventory.TryUseConsumable("MAT-05")&&inventory.MaterialCount("MAT-05")==1,"Full health blocks consumption");
            health.ApplyDamage(new DamageInfo(40,null,player.transform.position,Vector3.forward));float before=health.Current;
            Check(inventory.TryUseConsumable("MAT-05")&&inventory.MaterialCount("MAT-05")==0,"Using salve pays once");
            for(int i=0;i<15;i++)yield return null;
            Check(health.Current>before&&health.Current<=health.Maximum,"Salve heals gradually without exceeding maximum");
            inventory.TryGrantMaterial("MAT-05",1);Check(!inventory.TryUseConsumable("MAT-05")&&inventory.MaterialCount("MAT-05")==1,"Active salve blocks another consumption");
            health.ApplyDamage(new DamageInfo(5,null,player.transform.position,Vector3.forward));Check(!player.GetComponent<ConsumableHealing>().Active,"Damage interrupts healing");
            inventory.TryGrantMaterial(MaterialCatalog.Wood,2);inventory.TryGrantMaterial(MaterialCatalog.Stone,3);inventory.TryGrantMaterial(MaterialCatalog.MonsterComponent,1);
            string weaponId=inventory.EquippedId(0);var old=inventory.Item(weaponId);
            Check(inventory.TryCraft(settings.recipes[1],weaponId,station),"Weapon upgrade crafts with exact materials");
            var upgraded=inventory.Item(weaponId);Check(upgraded.tier==2&&upgraded.instanceId==old.instanceId&&upgraded.variant==old.variant,"Upgrade preserves weapon identity and variant");
            Check(!inventory.TryCraft(settings.recipes[1],weaponId,station),"Tier two cannot repeat the initial upgrade");
            player.transform.position=Vector3.right*10;inventory.TryGrantMaterial(MaterialCatalog.Herb,2);
            Check(!inventory.TryCraft(settings.recipes[0],weaponId,station)&&inventory.MaterialCount(MaterialCatalog.Herb)==2,"Out-of-range crafting cannot spend ingredients");
            player.transform.position=Vector3.up*.2f;
            storageField.SetValue(inventory,new FailingStorage());Check(!inventory.TryCraft(settings.recipes[0],weaponId,station)&&inventory.MaterialCount(MaterialCatalog.Herb)==2,"Failed crafting save preserves ingredients");storageField.SetValue(inventory,workingStorage);
            Check(inventory.TryCraft(settings.recipes[0],weaponId,station),"Craft can retry successfully after storage recovers");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.G));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check(gathering.BlocksGameplay,"Workbench opens through G and blocks gameplay controls");
            for(int i=0;i<10;i++)yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"crafting.png"));for(int i=0;i<10;i++)yield return null;
            InputSystem.RemoveDevice(keyboard);
            var worldCatalog=Mismo.Core.ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog");
            foreach(var kind in new[]{WorldAssetKind.Tree,WorldAssetKind.Rock,WorldAssetKind.Deadwood,WorldAssetKind.Bush,WorldAssetKind.Flower,WorldAssetKind.Grass})
            {
                var entry=Array.Find(worldCatalog.assets,e=>e.kind==kind&&e.prefab!=null);
                Check(entry!=null,"World catalog supplies "+kind);
                var placed=GatheringDistribution.PlaceAsset(entry,"test:catalog:"+kind,new Vector3(0,0,1.5f),Quaternion.identity,null);
                yield return null;yield return null;
                var resource=placed.GetComponent<GatheringNode>();
                if(entry.decorativeOnly||kind==WorldAssetKind.Grass)Check(resource==null,"Catalog decoration cannot be harvested: "+kind);
                else
                {
                    Check(resource!=null&&resource.Available&&placed.GetComponentInChildren<MeshFilter>().sharedMesh==entry.prefab.GetComponentInChildren<MeshFilter>().sharedMesh,"Catalog model preserved for "+kind);
                    Check(resource.Complete(inventory)&&!resource.Available,"Catalog resource harvests once: "+kind);
                    yield return null;yield return null;
                    Check(placed.GetComponentsInChildren<Renderer>().Length==0,"Harvested catalog visual disappears: "+kind);
                }
                Object.Destroy(placed);yield return null;yield return null;
            }
        }
    }
}
