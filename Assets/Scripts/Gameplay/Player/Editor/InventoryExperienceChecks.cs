using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InventoryExperienceChecks
    {
        const string Key="Mismo.InventoryExperienceChecks";
        static IEnumerator routine;
        static double deadline;
        static int frame=-1,count;
        static string Output=>Path.GetFullPath("../../output/inventory");
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Run these checks only in an isolated .validation project.");
            InventoryPresentationAssets.GenerateGridIcons();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(80,1,80);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            var respawn=player.GetComponent<World.RegionRespawn>();if(respawn!=null)Object.DestroyImmediate(respawn);
            player.transform.position=Vector3.up*.2f;
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(4,3,-7);camera.transform.LookAt(Vector3.up);
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-30,0);
            var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Resume()
        {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;
        }
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<15||frame==Time.frameCount)return;frame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks passed");}
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Debug.Log("INVENTORY_EXPERIENCE "+message);}
        static void Finish(bool pass,string message)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Step;
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"checks.txt"),(pass?"PASS ":"FAIL ")+message);
            Debug.Log("INVENTORY_EXPERIENCE_"+(pass?"PASS ":"FAIL ")+message);EditorApplication.Exit(pass?0:1);
        }
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static IEnumerator Run()
        {
            Application.runInBackground=true;
            var player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;
            var loadout=player.GetComponent<EquipmentLoadout>();loadout.Runner.Cancel();loadout.Belt?.Cancel();
            var inventory=player.gameObject.AddComponent<PlayerInventory>();
            var path=Path.Combine(Output,"test-"+Guid.NewGuid().ToString("N")+".mismo");
            inventory.Initialize(Resources.Load<ItemCatalog>("ItemCatalog"),new ProtectedProfileRepository(path));
            Check(inventory.Material(MaterialCatalog.Wood).icon!=null&&inventory.Definition(inventory.EquippedId(0)).inventoryIcon!=null,"Grid icons are imported as usable sprites");
            var panel=player.gameObject.AddComponent<InventoryPanel>();
            var settings=InventorySettings.Current;settings.backpackColumns=3;settings.backpackRows=1;
            var itemCatalog=Resources.Load<ItemCatalog>("ItemCatalog");
            foreach(var weapon in itemCatalog.weapons){weapon.gridWidth=1;weapon.gridHeight=1;}
            foreach(var material in inventory.Materials){material.gridWidth=1;material.gridHeight=1;}
            Check(inventory.IsReady&&inventory.UsedSlots(false)==2,"Two equipped weapons count toward capacity");
            Check(inventory.TryGrantMaterial(MaterialCatalog.Wood,20)&&inventory.UsedSlots(false)==3,"Material fills last slot");
            Check(!inventory.TryGrantMaterial(MaterialCatalog.Wood,1)&&inventory.MaterialCount(MaterialCatalog.Wood)==20,"Full backpack rejects reward without mutation");
            Check(inventory.TryGrantVictory(25,null,null,"inventory-test-enemy",new System.Collections.Generic.Dictionary<string,int>{{MaterialCatalog.Stone,2}},player.transform.position),"Victory saves EXP and overflow loot");
            string pending=null;foreach(var loot in inventory.PendingLoot)pending=loot.id;
            Check(pending!=null,"Full backpack preserves collectible loot");
            var defs=new System.Collections.Generic.HashSet<string>();foreach(var w in Resources.Load<ItemCatalog>("ItemCatalog").weapons)defs.Add(w.Id);
            var rewardDefs=new System.Collections.Generic.Dictionary<string,string>{{ItemCatalog.BossRewardId,inventory.BossReward.Id}};
            var reopen=new ProtectedProfileRepository(path);reopen.Read(_=>true,out var pendingJson);
            var pendingSaved=JsonUtility.FromJson<InventoryProfile>(pendingJson);
            Check(pendingSaved.IsValid(defs,rewardDefs)&&pendingSaved.pendingLoot.Count==1,"Unity serialization preserves valid uncollected loot");
            Check(inventory.TryGrantVictory(25,null,null,"inventory-test-enemy"),"Retry of defeated enemy is idempotent");
            int pendingCount=0;foreach(var loot in inventory.PendingLoot)pendingCount++;
            Check(pendingCount==1,"Reward retry does not duplicate overflow");
            for(int i=0;i<70;i++)yield return null;
            var chest=GameObject.Find("Cofre personal");Check(chest!=null,"Personal chest appears on valid ground near arrival");
            player.transform.position=chest.transform.position+Vector3.back;
            Check(inventory.AtChest,"Chest requires proximity");
            Check(inventory.Transfer(MaterialCatalog.Wood,true,20,true)&&inventory.MaterialCount(MaterialCatalog.Wood)==0&&inventory.StoredMaterialCount(MaterialCatalog.Wood)==20,"Deposit saves both containers atomically");
            player.transform.position=new Vector3(0,.2f,0);
            Check(inventory.CollectPending(pending)&&inventory.MaterialCount(MaterialCatalog.Stone)==2,"Loot remains collectible after making room");
            Check(!inventory.CollectPending(pending),"Collected loot cannot duplicate");
            Check(inventory.ToggleFavorite(MaterialCatalog.Stone,true)&&!inventory.Discard(MaterialCatalog.Stone,true,1),"Favorite protects against discard");
            inventory.ToggleFavorite(MaterialCatalog.Stone,true);
            Check(inventory.Discard(MaterialCatalog.Stone,true,1)&&inventory.MaterialCount(MaterialCatalog.Stone)==1,"Discard uses exact quantity");
            Check(!inventory.Discard(inventory.EquippedId(0),false,1),"Equipped weapon is protected");
            Check(inventory.TryClaimReward(ItemCatalog.BossRewardId)&&inventory.HasClaimed(ItemCatalog.BossRewardId),"Full bag reserves unique boss reward persistently");
            Check(!inventory.TryClaimReward(ItemCatalog.BossRewardId),"Reserved unique reward cannot duplicate");
            string bossLoot=null;foreach(var loot in inventory.PendingLoot)bossLoot=loot.id;
            Check(bossLoot!=null,"Unique reward remains collectible on the ground");
            inventory.Discard(MaterialCatalog.Stone,true,1);
            Check(inventory.CollectPending(bossLoot),"Unique reward can be collected after making space");
            player.transform.position=new Vector3(20,1,20);
            Check(!inventory.Transfer(MaterialCatalog.Wood,true,1,false),"Remote chest withdrawals are blocked");
            settings.backpackColumns=8;settings.backpackRows=6;
            foreach(var weapon in itemCatalog.weapons){weapon.gridWidth=weapon.isBow?2:1;weapon.gridHeight=3;}
            foreach(var material in inventory.Materials){material.gridWidth=material.id==MaterialCatalog.Wood?2:1;material.gridHeight=1;}
            inventory.TryGrantMaterial(MaterialCatalog.Herb,8);inventory.TryGrantMaterial(MaterialCatalog.Wood,14);
            var layout=inventory.GridPositions(false);var moving=layout[0];
            Check(inventory.MoveGrid(moving.key,false,6,3,false),"Grid item moves into free cells");
            Check(!inventory.MoveGrid(moving.key,false,8,0,false),"Grid item cannot move outside bag");
            Check(inventory.MoveGrid(moving.key,false,4,4,true),"Weapon rotates into horizontal footprint");
            var freshStore=new ProtectedProfileRepository(path);freshStore.Read(_=>true,out var gridJson);
            var gridSaved=JsonUtility.FromJson<InventoryProfile>(gridJson);
            Check(gridSaved.gridPlacements.Exists(g=>g.key==moving.key&&g.rotated&&g.x==4&&g.y==4),"Grid position and rotation survive serialization");
            Check(inventory.OrganizeGrid(false),"Automatic organization fits the current collection");
            Check(panel.TryOpen()&&panel.BlocksGameplay&&Cursor.visible,"Inventory opens and blocks gameplay input");
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-character.png"));
            for(int i=0;i<15;i++)yield return null;
            Field(panel,"selected",inventory.EquippedId(0));Field(panel,"previewDirty",true);
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-weapon.png"));
            for(int i=0;i<15;i++)yield return null;
            Field(panel,"selected",MaterialCatalog.Wood);Field(panel,"selectedMaterial",true);Field(panel,"previewDirty",true);
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-material.png"));
            for(int i=0;i<15;i++)yield return null;
            var pageField=typeof(InventoryPanel).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic);
            pageField.SetValue(panel,Enum.Parse(pageField.FieldType,"Menu"));
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-menu.png"));
            for(int i=0;i<15;i++)yield return null;
            player.transform.position=chest.transform.position+Vector3.back;
            Check(panel.OpenChest(),"Chest opens from proximity");
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-chest.png"));
            for(int i=0;i<15;i++)yield return null;
            panel.Close();
            Check(!panel.IsOpen&&panel.BlocksGameplay,"Close consumes its frame");
            yield return null;
            Check(!panel.BlocksGameplay,"Gameplay resumes next frame");
            var repository=new ProtectedProfileRepository(path);
            Check(repository.Read(_=>true,out var json)==ProfileReadResult.Loaded,"New repository reopens protected save");
            var restored=JsonUtility.FromJson<InventoryProfile>(json);
            Check(restored.chestMaterials.Find(s=>s.id==MaterialCatalog.Wood).quantity==20&&restored.pendingLoot.Count==0,"Chest and consumed loot persist across reload");
        }
    }
}
