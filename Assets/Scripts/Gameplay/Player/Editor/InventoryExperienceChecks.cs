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
            Check(inventory.UsedSlots(false)==occupied,"Swapping equipped sets leaves backpack capacity unchanged");
            Check(PanelAction(panel,"EquipAtHand",sword,0),"Main-hand drop restores original sets");
            Field(panel,"showChest",true);
            Check(!PanelAction(panel,"CanDropWeapon",sword,0),"Chest view cannot bypass withdrawal rules");
            Field(panel,"showChest",false);
            var spare=inventory.GridItems(false).Find(item=>!item.material&&!inventory.IsEquipped(item.id)&&inventory.CanUseOffhand(0,item.id));
            if(spare!=null)
            {
                Check(PanelAction(panel,"EquipAtHand",spare.id,1)&&inventory.OffhandId(0)==spare.id,"Compatible offhand drop equips a second object");
                Check(!inventory.GridItems(false).Exists(item=>item.id==spare.id)&&inventory.UsedSlots(false)<occupied,"Equipped offhand leaves the backpack and frees its cells");
                Check(inventory.TryEquipOffhand(0,null)&&inventory.Item(spare.id)!=null,"Removing offhand retains its owned object");
                Check(inventory.GridItems(false).Exists(item=>item.id==spare.id)&&inventory.UsedSlots(false)==occupied,"Unequipped offhand returns to backpack cells");
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
            Check(uiArt.VitalsFrame!=null,"Vitals frame loads without manual assignment");
            Check(uiArt.abilityIcons!=null&&uiArt.abilityIcons.Length==21,"Shared HUD icon references are populated");
            foreach(var icon in uiArt.abilityIcons)
                Check(icon!=null&&Mismo.Gameplay.Player.Presentation.QuietFantasyUI.Icon(icon.name)==icon,"HUD resolves referenced icon: "+(icon!=null?icon.name:"missing"));
            Check(inventory.Material(MaterialCatalog.Wood).icon!=null&&inventory.Definition(inventory.EquippedId(0)).inventoryIcon!=null,"Grid icons are imported as usable sprites");
            var panel=player.gameObject.AddComponent<InventoryPanel>();
            var settings=InventorySettings.Current;settings.backpackColumns=3;settings.backpackRows=1;
            var itemCatalog=Resources.Load<ItemCatalog>("ItemCatalog");
            foreach(var weapon in itemCatalog.weapons){weapon.gridWidth=1;weapon.gridHeight=1;}
            foreach(var material in inventory.Materials){material.gridWidth=1;material.gridHeight=1;}
            Check(inventory.IsReady&&inventory.UsedSlots(false)==0&&inventory.GridItems(false).Count==0,"Both equipped sets are outside backpack capacity");
            Check(inventory.TryGrantMaterial("MAT-06",40),"Fill two backpack cells with material stacks");
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
            var replacement=inventory.GridItems(false).Find(item=>!item.material);
            var originalMain=inventory.EquippedId(0);
            var originalDefinition=inventory.Definition(originalMain);
            originalDefinition.gridHeight=2;
            Check(!inventory.TryEquip(0,replacement.id)&&inventory.EquippedId(0)==originalMain&&inventory.GridItems(false).Exists(item=>item.id==replacement.id),"Oversized displaced weapon rejects equip without losing either item");
            originalDefinition.gridHeight=1;
            Check(inventory.TryEquip(0,replacement.id)&&inventory.UsedSlots(false)==3,"Full backpack swaps weapons using the incoming weapon's freed cell");
            Check(inventory.TryEquip(0,originalMain),"Restore original main weapon after full-bag swap");
            if(inventory.CanUseOffhand(0,replacement.id))
            {
                Check(inventory.TryEquipOffhand(0,replacement.id)&&inventory.UsedSlots(false)==2,"Offhand equipment frees one cell");
                Check(inventory.TryGrantMaterial(MaterialCatalog.Wood,20),"Fill the cell freed by the offhand");
                Check(!inventory.TryEquipOffhand(0,null)&&inventory.OffhandId(0)==replacement.id,"Full backpack rejects offhand removal without losing equipment");
                Check(inventory.Discard(MaterialCatalog.Wood,true,20)&&inventory.TryEquipOffhand(0,null),"Offhand removal succeeds after making room");
            }
            player.transform.position=new Vector3(20,1,20);
            Check(!inventory.Transfer(MaterialCatalog.Wood,true,1,false),"Remote chest withdrawals are blocked");
            settings.backpackColumns=8;settings.backpackRows=6;
            foreach(var weapon in itemCatalog.weapons){weapon.gridWidth=weapon.isBow?2:1;weapon.gridHeight=3;}
            foreach(var material in inventory.Materials){material.gridWidth=material.id==MaterialCatalog.Wood?2:1;material.gridHeight=1;}
            inventory.TryGrantMaterial(MaterialCatalog.Herb,8);inventory.TryGrantMaterial(MaterialCatalog.Wood,14);
            var spareWeapon=inventory.GridItems(false).Find(item=>!item.material);
            var layout=inventory.GridPositions(false);var moving=layout.Find(p=>p.key==spareWeapon.key);
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
            Check(!gridSaved.gridPlacements.Exists(g=>gridSaved.IsEquipped(g.key)),"Saved grid excludes all equipped weapons");
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
            Check(!inventory.GridItems(false).Exists(item=>inventory.IsEquipped(item.id)),"Equipped weapons never appear in the bag grid");
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
            var radialIndex=typeof(InventoryPanel).GetMethod("RadialIndex",BindingFlags.Static|BindingFlags.NonPublic);
            Check((int)radialIndex.Invoke(null,new object[]{Vector2.zero})==-1,"Radial center cancels");
            for(int i=0;i<6;i++)
            {
                float angle=(-90+i*60)*Mathf.Deg2Rad;
                Check((int)radialIndex.Invoke(null,new object[]{new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*180})==i,"Radial direction selects sector "+i);
            }
            Check(Mathf.Approximately(Time.timeScale,1),"Radial menu keeps world running");
            Field(panel,"radialSelected",1);
            typeof(InventoryPanel).GetMethod("ConfirmRadialSelection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
            Check(panel.IsOpen&&pageField.GetValue(panel).ToString()=="Inventory","Radial inventory selection opens its page");
            pageField.SetValue(panel,Enum.Parse(pageField.FieldType,"Menu"));Field(panel,"radialSelected",-1);
            typeof(InventoryPanel).GetMethod("ConfirmRadialSelection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
            Check(!panel.IsOpen,"Radial center closes without opening a page");
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
            Check(inventory.TryGrantVictory(300,null,null,"attribute-draft-test"),"Grant attribute points for allocation checks");
            var before=inventory.Progression;
            Check(!inventory.TrySpendAttributes(-1,1,0)&&!inventory.TrySpendAttributes(int.MaxValue,1,0),"Invalid attribute batches are rejected");
            var storageField=typeof(PlayerInventory).GetField("repository",BindingFlags.Instance|BindingFlags.NonPublic);
            var originalStorage=storageField.GetValue(inventory);
            storageField.SetValue(inventory,new RejectQuickSlotSave());
            Check(!inventory.TrySpendAttributes(1,1,0)&&inventory.Progression.lifePoints==before.lifePoints&&inventory.Progression.attackPoints==before.attackPoints,"Failed attribute save applies no partial allocation");
            storageField.SetValue(inventory,originalStorage);
            Check(inventory.TrySpendAttributes(1,1,0)&&inventory.Progression.Available==before.Available-2,"Apply commits the attribute allocation together");
            var openPage=typeof(InventoryPanel).GetMethod("OpenPage",BindingFlags.Instance|BindingFlags.NonPublic);
            openPage.Invoke(panel,new object[]{Enum.Parse(pageField.FieldType,"Character")});
            var draft=(int[])typeof(InventoryPanel).GetField("pendingAttributes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
            draft[2]=1;
            for(int i=0;i<15;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"character-refactor.png"));
            for(int i=0;i<15;i++)yield return null;
            panel.Close();Check(draft[2]==0,"Closing the character sheet discards unconfirmed points");
            var masteryWeapon=loadout.GetSlot(0);
            Check(inventory.TryGrantVictory(0,new System.Collections.Generic.Dictionary<string,int>{{masteryWeapon.MasteryId,300}},null,"mastery-draft-test"),"Grant points for mastery allocation");
            var masteryBefore=inventory.Mastery(masteryWeapon);
            Check(!inventory.TrySpendMasteryPoints(masteryWeapon,-1,1)&&!inventory.TrySpendMasteryPoints(masteryWeapon,int.MaxValue,1),"Mastery rejects invalid and excessive batches");
            storageField.SetValue(inventory,new RejectQuickSlotSave());
            Check(!inventory.TrySpendMasteryPoints(masteryWeapon,1,1)&&inventory.Mastery(masteryWeapon).Available==masteryBefore.Available,"Failed mastery save spends no points");
            storageField.SetValue(inventory,originalStorage);
            Check(inventory.TrySpendMasteryPoints(masteryWeapon,1,1)&&inventory.Mastery(masteryWeapon).Available==masteryBefore.Available-2,"Mastery applies damage and speed atomically");
            openPage.Invoke(panel,new object[]{Enum.Parse(pageField.FieldType,"Weapons")});Field(panel,"skillsWeapon",masteryWeapon);
            for(int i=0;i<5;i++)yield return null;
            var masteryDraft=(int[])typeof(InventoryPanel).GetField("pendingMastery",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
            masteryDraft[0]=1;
            for(int i=0;i<5;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"mastery-allocation.png"));
            for(int i=0;i<5;i++)yield return null;
            Field(panel,"skillsWeapon",loadout.GetSlot(1));
            for(int i=0;i<5;i++)yield return null;
            Check(masteryDraft[0]==0&&masteryDraft[1]==0,"Changing weapons discards unconfirmed mastery points");
            masteryDraft[0]=1;panel.Close();Check(masteryDraft[0]==0,"Closing discards mastery draft");
            var book=Resources.Load<World.BestiaryBook>("BestiaryBook");
            var modelFixture=GameObject.CreatePrimitive(PrimitiveType.Cube);
            modelFixture.name="Bestiary model regression fixture";
            var textFixture=new GameObject("State Label",typeof(TextMesh));textFixture.transform.SetParent(modelFixture.transform,false);
            var fixturePreview=new InventoryPreview();fixturePreview.Show(modelFixture);
            Check(fixturePreview.HasModel,"Model preview ignores text renderers without MeshFilter");
            fixturePreview.Dispose();
            fixturePreview.Show(textFixture);Check(!fixturePreview.HasModel,"Text-only prefab safely has no model");fixturePreview.Dispose();
            Object.Destroy(modelFixture);
            if(book!=null&&book.pages!=null)foreach(var species in book.pages)if(species!=null)inventory.DiscoverSpecies(species.id);
            uiArt.panelOpacity=.6f;uiArt.navigationIconOpacity=.8f;
            openPage.Invoke(panel,new object[]{Enum.Parse(pageField.FieldType,"Bestiary")});
            for(int i=0;i<15;i++)yield return null;
            var bestiaryView=(BestiaryView)typeof(InventoryPanel).GetField("bestiary",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
            var bestiaryPage=typeof(BestiaryView).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic);
            var bestiaryLast=typeof(BestiaryView).GetField("last",BindingFlags.Instance|BindingFlags.NonPublic);
            int speciesCount=book==null||book.pages==null?0:Array.FindAll(book.pages,s=>s!=null).Length;
            for(int index=0;index<speciesCount;index++)
            {
                bestiaryPage.SetValue(bestiaryView,index);
                for(int i=0;i<4;i++)yield return null;
                Check((int)bestiaryLast.GetValue(bestiaryView)==index,"Bestiary page loads or falls back once: "+index);
                if(preview.HasModel)
                {
                    var modelCamera=(UnityEngine.Camera)cameraField.GetValue(preview);
                    var startPosition=modelCamera.transform.position;preview.Rotate(new Vector2(-40,20));
                    Check(Vector3.Distance(startPosition,modelCamera.transform.position)>.01f,"Bestiary model can orbit: "+index);
                }
            }
            bestiaryPage.SetValue(bestiaryView,0);
            for(int i=0;i<4;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"bestiary-opacity.png"));
            for(int i=0;i<15;i++)yield return null;
            panel.Close();uiArt.panelOpacity=1;uiArt.navigationIconOpacity=1;
            Type pauseType=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){pauseType=assembly.GetType("Mismo.Menu.PauseMenu");if(pauseType!=null)break;}
            Check(pauseType!=null,"Pause menu assembly is available");
            var pause=Object.FindAnyObjectByType(pauseType) as Component;
            if(pause==null)pause=new GameObject("Pause test").AddComponent(pauseType);
            pauseType.GetMethod("Open").Invoke(pause,null);
            Check(Presentation.GameplayPause.IsPaused&&Mathf.Approximately(Time.timeScale,0),"Pause freezes gameplay");
            for(int i=0;i<8;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"pause-home.png"));
            for(int i=0;i<4;i++)yield return null;
            Field(pause,"options",true);
            for(int category=0;category<4;category++)
            {
                Field(pause,"tab",category);
                for(int i=0;i<5;i++)yield return null;
                Check(Presentation.GameplayPause.IsPaused,"Options keeps game paused: "+category);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"pause-options-"+category+".png"));
                for(int i=0;i<4;i++)yield return null;
            }
            pauseType.GetMethod("Resume").Invoke(pause,null);
            Check(!Presentation.GameplayPause.IsPaused&&Mathf.Approximately(Time.timeScale,1),"Resume restores gameplay time");
            for(int i=0;i<8;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"hud-refactor.png"));
            for(int i=0;i<4;i++)yield return null;
            var menuRoot=new GameObject("Credits validation");menuRoot.SetActive(false);
            Type menuType=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){menuType=assembly.GetType("Mismo.Menu.MainMenuView");if(menuType!=null)break;}
            Check(menuType!=null,"Main menu assembly is available");
            var menu=menuRoot.AddComponent(menuType);menuType.GetMethod("CreateLayout").Invoke(menu,null);menuRoot.SetActive(true);
            for(int i=0;i<5;i++)yield return null;
            menuType.GetMethod("ShowCredits",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
            var credits=(GameObject)menuType.GetField("credits",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            Check(credits.activeSelf&&!(bool)menuType.GetProperty("OptionsVisible").GetValue(menu),"Main menu opens a separate credits page");
            for(int i=0;i<10;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"main-menu-credits.png"));
            for(int i=0;i<10;i++)yield return null;
            menuType.GetMethod("ShowOptions").Invoke(menu,new object[]{false});Check(!credits.activeSelf,"Credits returns to the main menu");
            Object.Destroy(menuRoot);
            Localization.GameLanguage.Set(previousLanguage,false);
        }
    }
}
