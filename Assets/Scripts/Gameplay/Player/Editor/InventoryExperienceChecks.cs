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
        static bool PanelAction(InventoryPanel panel,string method,string id,int hand)=>(bool)typeof(InventoryPanel)
            .GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{id,hand});
        static void CheckVisualEquipment(InventoryPanel panel,PlayerInventory inventory)
        {
            string sword=inventory.EquippedId(0),bow=inventory.EquippedId(1);
            long occupied=inventory.UsedSlots(false);
            Check(!PanelAction(panel,"CanDropWeapon",bow,1),"Drag target rejects bow in offhand");
            Check(!PanelAction(panel,"CanDropWeapon",sword,1),"Drag target rejects duplicating a main-hand instance");
            Check(PanelAction(panel,"EquipAtHand",bow,0),"Main-hand drop swaps equipped sets atomically");
            Check(inventory.EquippedId(0)==bow&&inventory.EquippedId(1)==sword,"Set swap retains both weapon instances");
            Check(inventory.UsedSlots(false)==occupied,"Equipment drop does not free occupied backpack cells");
            Check(PanelAction(panel,"EquipAtHand",sword,0),"Main-hand drop restores original sets");
            Field(panel,"showChest",true);
            Check(!PanelAction(panel,"CanDropWeapon",sword,0),"Chest view cannot bypass withdrawal rules");
            Field(panel,"showChest",false);
            var spare=inventory.GridItems(false).Find(item=>!item.material&&!inventory.IsEquipped(item.id)&&inventory.CanUseOffhand(0,item.id));
            if(spare!=null)
            {
                Check(PanelAction(panel,"EquipAtHand",spare.id,1)&&inventory.OffhandId(0)==spare.id,"Compatible offhand drop equips a second object");
                Check(inventory.TryEquipOffhand(0,null)&&inventory.Item(spare.id)!=null,"Removing offhand retains its owned object");
            }
            Field(panel,"draggedGrid",sword);
            typeof(InventoryPanel).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{false});
            Check(typeof(InventoryPanel).GetField("draggedGrid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel)==null,"Focus loss cancels pending inventory drag");
        }
        sealed class RejectQuickSlotSave:IProfileRepository
        {
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}
            public void Write(string payload){throw new IOException("Injected quick slot save failure");}
        }
        static void CheckConsumableSlots(PlayerInventory inventory,string path)
        {
            Check(inventory.TryGrantMaterial("MAT-08",2),"Grant two quick-slot potions");
            long cells=inventory.UsedSlots(false);
            Check(!inventory.AssignConsumable(-1,"MAT-08")&&!inventory.AssignConsumable(4,"MAT-08"),"Quick-slot bounds reject invalid indices");
            Check(!inventory.AssignConsumable(0,MaterialCatalog.Wood),"Quick slots reject materials");
            Check(inventory.AssignConsumable(0,"MAT-08")&&inventory.ConsumableSlot(0)=="MAT-08","Assign owned potion");
            Check(inventory.AssignConsumable(3,"MAT-08")&&inventory.ConsumableSlot(0)==null&&inventory.ConsumableSlot(3)=="MAT-08","Moving assignment avoids duplicate bindings");
            Check(inventory.UsedSlots(false)==cells&&inventory.MaterialCount("MAT-08")==2,"Assignment neither moves nor duplicates inventory stock");
            new ProtectedProfileRepository(path).Read(_=>true,out var json);
            var saved=JsonUtility.FromJson<InventoryProfile>(json);var copy=saved.Copy();copy.consumableSlots[3]=null;
            Check(saved.consumableSlots[3]=="MAT-08","Assignments survive save and profile copies are independent");
            var repository=typeof(PlayerInventory).GetField("repository",BindingFlags.Instance|BindingFlags.NonPublic);
            var original=repository.GetValue(inventory);repository.SetValue(inventory,new RejectQuickSlotSave());
            try{Check(!inventory.AssignConsumable(1,"MAT-08")&&inventory.ConsumableSlot(3)=="MAT-08"&&inventory.ConsumableSlot(1)==null,"Failed save retains previous assignment");}
            finally{repository.SetValue(inventory,original);}
            Check(inventory.AssignConsumable(0,"MAT-08"),"Restore quick slot after failed save");
            var health=inventory.GetComponent<Health>();
            health.Heal(health.Maximum);
            Check(!inventory.TryUseConsumableSlot(0)&&inventory.MaterialCount("MAT-08")==2,"Full health does not consume quick potion");
            health.ApplyDamage(new DamageInfo(60,null,Vector3.zero,Vector3.forward));
            float before=health.Current;
            Check(inventory.TryUseConsumableSlot(0)&&health.Current>before&&inventory.MaterialCount("MAT-08")==1,"Quick slot uses existing healing and spends one item");
            Check(!inventory.TryUseConsumableSlot(0)&&inventory.MaterialCount("MAT-08")==1,"Quick slots respect potion cooldown");
            health.Heal(health.Maximum);
        }
        static IEnumerator Run()
        {
            Application.runInBackground=true;
            var previousLanguage=Localization.GameLanguage.Code;
            Localization.GameLanguage.Set("en",false);
            Check(Localization.GameLanguage.Text("Mochila")=="Backpack","English table resolves interface text");
            Check(Localization.GameLanguage.Format("Recuperación: {0:0.##} s",.85f)=="Cooldown: 0.85 s","English formats decimal cooldowns");
            Check(Localization.GameLanguage.Get("missing.test.key","Respaldo")=="Respaldo","Missing entries retain fallback text");
            Localization.GameLanguage.Set("es",false);
            Check(Localization.GameLanguage.Text("Mochila")=="Mochila","Spanish can be restored without restarting");
            Localization.GameLanguage.Set("en",false);
            var player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;
            var loadout=player.GetComponent<EquipmentLoadout>();loadout.Runner.Cancel();loadout.Belt?.Cancel();
            var inventory=player.gameObject.AddComponent<PlayerInventory>();
            var path=Path.Combine(Output,"test-"+Guid.NewGuid().ToString("N")+".mismo");
            inventory.Initialize(Resources.Load<ItemCatalog>("ItemCatalog"),new ProtectedProfileRepository(path));
            Check(inventory.IsReady,"Test inventory initializes: "+inventory.Notice);
            var uiArt=Resources.Load<InventoryUIIcons>("InventoryUIIcons");
            Check(uiArt!=null&&uiArt.weaponSlotBackground!=null&&uiArt.consumableSlotBackground!=null&&
                uiArt.weaponSlotUV.width>0&&uiArt.weaponSlotUV.height>0&&uiArt.consumableSlotUV.width>0&&uiArt.consumableSlotUV.height>0,
                "Slot artwork loads with nonempty texture regions");
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
            var layout=inventory.GridPositions(false);var moving=layout.Find(p=>p.key==inventory.EquippedId(0));
            Vector2Int FindFree(bool rotated)
            {
                for(int y=inventory.GridRows(false)-1;y>=0;y--)for(int x=inventory.GridColumns(false)-1;x>=0;x--)
                    if((x!=moving.x||y!=moving.y)&&inventory.CanMoveGrid(moving.key,false,x,y,rotated))return new Vector2Int(x,y);
                throw new Exception("No free test destination");
            }
            var destination=FindFree(false);
            Check(inventory.MoveGrid(moving.key,false,destination.x,destination.y,false),"Grid item moves into free cells");
            Check(!inventory.MoveGrid(moving.key,false,8,0,false),"Grid item cannot move outside bag");
            var rotatedDestination=FindFree(true);
            Check(inventory.MoveGrid(moving.key,false,rotatedDestination.x,rotatedDestination.y,true),"Weapon rotates into horizontal footprint");
            var freshStore=new ProtectedProfileRepository(path);freshStore.Read(_=>true,out var gridJson);
            var gridSaved=JsonUtility.FromJson<InventoryProfile>(gridJson);
            Check(gridSaved.gridPlacements.Exists(g=>g.key==moving.key&&g.rotated&&g.x==rotatedDestination.x&&g.y==rotatedDestination.y),"Grid position and rotation survive serialization");
            Check(inventory.OrganizeGrid(false),"Automatic organization fits the current collection");
            Check(panel.TryOpen()&&panel.BlocksGameplay&&Cursor.visible,"Inventory opens and blocks gameplay input");
            CheckVisualEquipment(panel,inventory);
            CheckConsumableSlots(inventory,path);
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-character.png"));
            for(int i=0;i<15;i++)yield return null;
            Field(panel,"selected",inventory.EquippedId(0));Field(panel,"previewDirty",true);
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-weapon.png"));
            for(int i=0;i<15;i++)yield return null;
            Check(uiArt.oliveWeaponSlotBackground!=null&&uiArt.oliveConsumableSlotBackground!=null,"Olive theme artwork is assigned");
            uiArt.useOliveTheme=true;
            for(int i=0;i<15;i++)yield return null;
            var themeField=typeof(InventoryPanel).GetField("inventoryBackdropOlive",BindingFlags.Instance|BindingFlags.NonPublic);
            Check((bool)themeField.GetValue(panel),"Olive theme updates while inventory stays open");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-olive.png"));
            for(int i=0;i<15;i++)yield return null;
            uiArt.useOliveTheme=false;
            for(int i=0;i<15;i++)yield return null;
            Check(!(bool)themeField.GetValue(panel),"Gray theme restores without restarting Play");
            Check(inventory.MoveGrid(moving.key,false,4,4,true),"Horizontal icon fits inside its grid footprint");
            var bowItem=inventory.GridItems(false).Find(item=>item.id==inventory.EquippedId(1));
            Check(bowItem!=null&&inventory.MoveGrid(bowItem.key,false,0,3,true),"Bow displays horizontally in its rotated footprint");
            var preview=(InventoryPreview)typeof(InventoryPanel).GetField("preview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
            var cameraField=typeof(InventoryPreview).GetField("camera",BindingFlags.Instance|BindingFlags.NonPublic);
            var previewCamera=(UnityEngine.Camera)cameraField.GetValue(preview);
            var beforeRotation=previewCamera.transform.position;
            preview.Rotate(new Vector2(100,35));
            Check(Vector3.Distance(beforeRotation,previewCamera.transform.position)>.1f,"Preview camera orbits selected model");
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-rotated.png"));
            for(int i=0;i<15;i++)yield return null;
            var skillsPage=typeof(InventoryPanel).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic);
            skillsPage.SetValue(panel,Enum.Parse(skillsPage.FieldType,"Skills"));
            Field(panel,"skillsWeapon",loadout.GetSlot(1));
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"inventory-skills.png"));
            for(int i=0;i<15;i++)yield return null;
            skillsPage.SetValue(panel,Enum.Parse(skillsPage.FieldType,"Inventory"));
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
            Localization.GameLanguage.Set(previousLanguage,false);
        }
    }
}
