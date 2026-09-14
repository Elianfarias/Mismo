using System;
using System.Collections;
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
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class CraftingExpansionChecks
    {
        const string Key="Mismo.CraftingExpansionChecks";
        static IEnumerator routine;static int frame=-1,count;static double deadline;
        static string Output=>Path.GetFullPath("../../output/crafting");
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use isolated project.");
            CraftingExpansionAssets.Create();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(80,1,80);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));Object.DestroyImmediate(player.GetComponent<RegionRespawn>());
            var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4,3,-7);camera.transform.LookAt(Vector3.up);
            new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
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
        static void Finish(bool pass,string text){SessionState.SetBool(Key,false);EditorApplication.update-=Step;File.WriteAllText(Path.Combine(Output,"checks.txt"),(pass?"PASS ":"FAIL ")+text);Debug.Log("EXPANSION_"+(pass?"PASS ":"FAIL ")+text);EditorApplication.Exit(pass?0:1);}
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);count++;Debug.Log("EXPANSION_CHECK "+text);}
        sealed class Memory:IProfileRepository
        {
            public string payload;public bool fail;
            public ProfileReadResult Read(Func<string,bool> validate,out string p){p=payload;return p==null?ProfileReadResult.Missing:ProfileReadResult.Loaded;}
            public void Write(string p){if(fail)throw new IOException("Expected test failure");payload=p;}
        }
        static IEnumerator Run()
        {
            var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
            var storage=new Memory();var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Resources.Load<ItemCatalog>("ItemCatalog"),storage);
            var health=player.GetComponent<Health>();var loadout=player.GetComponent<EquipmentLoadout>();
            var settings=Resources.Load<GatheringSettings>("GatheringSettings");
            Check(settings.recipes.Length==8,"Eight recipes load");
            Check(settings.recipes.Count(r=>r.Category==RecipeCategory.Preparation)==3&&settings.recipes.Count(r=>r.Category==RecipeCategory.Upgrades)==3&&settings.recipes.Count(r=>r.Category==RecipeCategory.Equipment)==2,"Three complete recipe categories");
            Check(settings.minerals.Any(n=>n.rewards.entries[0].material.id=="MAT-06")&&settings.minerals.Any(n=>n.rewards.entries[0].material.id=="MAT-07"),"Minerals yield iron and arcane crystal");
            Check(settings.stone.rewards.entries[0].material.id=="MAT-03","Ordinary rocks retain stone");
            foreach(string id in new[]{"MAT-01","MAT-02","MAT-03","MAT-04","MAT-06","MAT-07"})Check(inventory.TryGrantMaterial(id,10),"Grant ingredient "+id);
            var station=new GameObject("Workbench").AddComponent<CraftingStation>();station.recipes=settings.recipes;station.transform.position=player.transform.position;
            var potionRecipe=settings.recipes.First(r=>r.id=="REC-03");
            Check(inventory.TryCraft(potionRecipe,null,station)&&inventory.MaterialCount("MAT-08")==1,"Craft potion from rose and crystal");
            Check(inventory.TryCraft(settings.recipes.First(r=>r.id=="REC-04"),null,station)&&inventory.MaterialCount("MAT-09")==1,"Craft whetstone from stone and iron");
            var sword=settings.recipes.First(r=>r.id=="REC-07");int before=inventory.Count;
            Check(inventory.TryCraft(sword,null,station)&&inventory.Count==before+1,"Craft fresh basic weapon");
            Check(inventory.MaterialCount("MAT-06")==5,"Weapon pays its ingredients");
            var bowRecipe=settings.recipes.First(r=>r.id=="REC-08");
            int wood=inventory.MaterialCount("MAT-02");before=inventory.Count;storage.fail=true;
            Check(!inventory.TryCraft(bowRecipe,null,station)&&inventory.Count==before&&inventory.MaterialCount("MAT-02")==wood,"Failed weapon save preserves materials and inventory");storage.fail=false;
            var upgrade=settings.recipes.First(r=>r.id=="REC-02");var equipped=inventory.EquippedId(0);
            Check(inventory.TryCraft(upgrade,equipped,station)&&inventory.Item(equipped).tier==2,"Existing upgrade stays compatible");
            Check(!inventory.TryCraft(upgrade,equipped,station),"Cannot repeat same tier upgrade");
            Check(inventory.TryCraft(settings.recipes.First(r=>r.id=="REC-05"),equipped,station)&&inventory.Item(equipped).tier==3,"Iron upgrade advances same instance");
            inventory.TryGrantMaterial("MAT-08",2);inventory.TryGrantMaterial("MAT-09",1);
            Check(!inventory.TryUseConsumable("MAT-08")&&inventory.MaterialCount("MAT-08")==3,"Full health does not spend potion");
            health.ApplyDamage(new DamageInfo(60,null,Vector3.zero,Vector3.forward));
            loadout.MarkCombat();float hp=health.Current;Check(inventory.TryUseConsumable("MAT-08")&&Mathf.Approximately(health.Current,hp+35),"Potion heals instantly after damage in combat");
            Check(!inventory.TryUseConsumable("MAT-08")&&inventory.MaterialCount("MAT-08")==2,"Cooldown prevents repeated potion use");
            var saved=JsonUtility.FromJson<InventoryProfile>(storage.payload);Check(saved.potionReadyAt>saved.worldPlaySeconds&&saved.Copy().potionReadyAt==saved.potionReadyAt,"Potion cooldown persists and copies");
            float outOfCombatAt=Time.time+7;while(Time.time<outOfCombatAt)yield return null;
            Check(inventory.CanManage,"Combat grace ends before preparation");
            float damage=inventory.DamageMultiplier(loadout.ActiveDefinition);
            storage.fail=true;Check(!inventory.TryUseConsumable("MAT-09")&&inventory.MaterialCount("MAT-09")==2&&inventory.WeaponBuffRemaining==0,"Save failure grants no buff and spends nothing");storage.fail=false;
            Check(inventory.TryUseConsumable("MAT-09")&&inventory.DamageMultiplier(loadout.ActiveDefinition)>damage,"Whetstone buffs active weapon");
            Check(!inventory.TryUseConsumable("MAT-09")&&inventory.MaterialCount("MAT-09")==1,"Whetstone cannot stack");
            saved=JsonUtility.FromJson<InventoryProfile>(storage.payload);Check(saved.buffedWeaponId==inventory.EquippedId(loadout.ActiveSlot)&&saved.Copy().weaponBuffDamage==.15f,"Buff persists with weapon identity");
            var other=loadout.GetSlot(1-loadout.ActiveSlot);Check(inventory.WeaponConsumableBonus(other)==0,"Other weapon receives no bonus");
            typeof(PlayerInventory).GetField("worldClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(inventory,saved.weaponBuffUntil+1);
            Check(inventory.WeaponBuffRemaining==0&&inventory.WeaponConsumableBonus(loadout.ActiveDefinition)==0,"Buff expires by world play time");
            Check(inventory.PotionCooldownRemaining==0,"Potion cooldown expires");
            var gathering=player.GetComponent<GatheringPlayer>()??player.gameObject.AddComponent<GatheringPlayer>();
            typeof(GatheringPlayer).GetField("station",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gathering,station);
            for(int tab=0;tab<3;tab++)
            {
                typeof(GatheringPlayer).GetField("category",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gathering,tab);
                for(int f=0;f<5;f++)yield return null;
                string screenshot=Path.Combine(Output,new[]{"preparation.png","upgrades.png","equipment.png"}[tab]);
                ScreenCapture.CaptureScreenshot(screenshot);
                for(int f=0;f<30;f++)yield return null;
                Check(File.Exists(screenshot),"Crafting category screenshot "+tab);
            }
        }
    }
}
