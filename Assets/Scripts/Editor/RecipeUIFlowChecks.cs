using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static class RecipeUIFlowChecks
{
    const string Session="Mismo.RecipeUIFlowChecks";
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerator run,nested;static int frame=-1,count;static double deadline;
    static readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
    static Memory storage;
    static string Output=>Path.GetFullPath("output/recipe-ui-checks");
    sealed class Memory:IProfileRepository
    {
        public string data;public bool fail;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=data;return data==null?ProfileReadResult.Missing:ProfileReadResult.Loaded;}
        public void Write(string value){if(fail)throw new IOException("Expected recipe transaction failure");data=value;}
    }
    public static void Run()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use isolated project.");
        RecipeUIIntegration.Register();Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
        Object.DestroyImmediate(player.GetComponent<RegionRespawn>());
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(50,1,50);
        var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4,3,-7);camera.transform.LookAt(Vector3.up);
        camera.gameObject.AddComponent<AudioListener>();
        new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if(!SessionState.GetBool(Session,false))return;
        storage=new Memory();
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Output);
        typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,storage);
        Application.runInBackground=true;
        Application.logMessageReceived+=(message,stack,type)=>
        {
            // Unity's asynchronous editor search index can fail in a batch clone;
            // it is unrelated to runtime rendering and is retained in the editor log.
            if(stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))return;
            if(type==LogType.Exception||type==LogType.Error)errors.Add(message+"\n"+stack);
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializePlayer()
    {
        if(!SessionState.GetBool(Session,false))return;
        var player=Object.FindFirstObjectByType<PlayerController>();player.enabled=false;
        var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();
        inventory.Initialize(ProjectAssets.Load<ItemCatalog>("ItemCatalog"),storage);
        if(player.GetComponent<InventoryPanel>()==null)player.gameObject.AddComponent<InventoryPanel>();
        if(player.GetComponent<GatheringPlayer>()==null)player.gameObject.AddComponent<GatheringPlayer>();
    }
    [InitializeOnLoadMethod]static void Resume()
    {
        if(!SessionState.GetBool(Session,false))return;
        deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Step;
    }
    static void Step()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!Application.isPlaying||Time.frameCount<15||frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(nested!=null){if(nested.MoveNext())return;nested=null;}
            if(run==null)run=Checks();
            if(!run.MoveNext())Finish(true,count+" checks passed");
            else if(run.Current is IEnumerator child)nested=child;
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string text)
    {
        SessionState.SetBool(Session,false);EditorApplication.update-=Step;
        File.WriteAllText(Path.Combine(Output,"Checks.txt"),(ok?"PASS ":"FAIL ")+text);
        Debug.Log("RECIPE_FLOW_"+(ok?"PASS ":"FAIL ")+text);EditorApplication.Exit(ok?0:1);
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Debug.Log("RECIPE_CHECK "+text);}
    static IEnumerator Capture(string name)
    {
        for(int i=0;i<8;i++)yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(Output,name+".png"));
        for(int i=0;i<8;i++)yield return null;
    }
    static IEnumerator Checks()
    {
        var inventory=Object.FindFirstObjectByType<PlayerInventory>();
        Check(inventory!=null&&inventory.IsReady,"Inventory initializes against isolated storage");
        inventory.GetComponent<PlayerController>().enabled=false;
        var panel=inventory.GetComponent<InventoryPanel>();var gathering=inventory.GetComponent<GatheringPlayer>();
        var recipes=ProjectAssets.LoadAll<CraftingRecipe>("Recipes");
        var hand=recipes.First(r=>r.id=="REC-01");var weapon=recipes.First(r=>r.weaponResult!=null);var upgrade=recipes.First(r=>r.upgradeWeapon&&r.fromTier==1);
        Check(hand.craftInWorld,"Herbal salve explicitly supports world crafting");
        foreach(var filter in Enum.GetValues(typeof(RecipeBookView.Filter)).Cast<RecipeBookView.Filter>().Where(f=>f!=RecipeBookView.Filter.All))
            foreach(var recipe in recipes.Where(r=>RecipeBookView.Matches(r,filter)))
                Check(Enum.GetValues(typeof(RecipeBookView.Filter)).Cast<RecipeBookView.Filter>().Count(f=>f!=RecipeBookView.Filter.All&&RecipeBookView.Matches(recipe,f))==1,"Recipe belongs to one specific filter: "+recipe.id);
        Check(RecipeBookView.Available(null,recipes).Length==recipes.Distinct().Count(),"Radial recetario includes all registered recipes");
        Check(!inventory.TryCraftInWorld(hand),"Missing ingredients prevent spending");
        foreach(var material in recipes.SelectMany(r=>r.ingredients).Where(i=>i?.material!=null).Select(i=>i.material).Distinct())
            Check(inventory.TryGrantMaterial(material.id,12),"Grant test ingredient "+material.id);
        int before=inventory.MaterialCount(hand.ingredients[0].material.id),result=inventory.MaterialCount(hand.result.id);
        Check(inventory.TryCraftInWorld(hand)&&inventory.MaterialCount(hand.ingredients[0].material.id)==before-hand.ingredients[0].quantity&&inventory.MaterialCount(hand.result.id)==result+hand.quantity,"World crafting pays once and grants result");
        before=inventory.MaterialCount(hand.ingredients[0].material.id);storage.fail=true;
        Check(!inventory.TryCraftInWorld(hand)&&inventory.MaterialCount(hand.ingredients[0].material.id)==before,"Failed save rolls back world ingredient spending");storage.fail=false;
        Check(!inventory.TryCraftInWorld(weapon)&&!inventory.TryCraftInWorld(upgrade),"World crafting rejects station weapons and upgrades");
        var forged=Object.Instantiate(hand);
        Check(!inventory.TryCraftInWorld(forged),"Unregistered recipe cannot be crafted in world");Object.Destroy(forged);
        var station=new GameObject("Recipe check station").AddComponent<CraftingStation>();station.transform.position=inventory.transform.position;station.recipes=new[]{hand,weapon,upgrade};
        Check(RecipeBookView.Available(station,recipes).Length==3,"Station view uses its own recipe list");
        Check(!inventory.TryCraft(hand,null,null),"Missing station does not silently become handcraft");
        station.transform.position+=Vector3.right*50;
        Check(!inventory.TryCraft(hand,null,station),"Out-of-range station cannot craft");station.transform.position=inventory.transform.position;
        int countBefore=inventory.Count;
        Check(inventory.TryCraft(weapon,null,station)&&inventory.Count==countBefore+1,"Station weapon crafting preserved");
        string equipped=inventory.EquippedId(0);
        Check(inventory.TryCraft(upgrade,equipped,station)&&inventory.Item(equipped).tier==upgrade.toTier,"Station upgrades preserve instance and advance tier");
        Check(!inventory.TryCraft(upgrade,equipped,station),"Upgrade cannot repeat its old tier");
        var radial=typeof(InventoryPanel).GetMethod("RadialIndex",BindingFlags.Static|BindingFlags.NonPublic);
        for(int i=0;i<8;i++)
        {
            float a=(-90+i*360f/8)*Mathf.Deg2Rad;
            Check((int)radial.Invoke(null,new object[]{new Vector2(Mathf.Cos(a),Mathf.Sin(a))*157})==i,"Radial hit matches sector "+i);
        }
        Check((int)radial.Invoke(null,new object[]{Vector2.zero})==-1,"Radial center cancels");
        var pageType=typeof(InventoryPanel).GetNestedType("Page",BindingFlags.NonPublic);
        typeof(InventoryPanel).GetMethod("OpenPage",Private).Invoke(panel,new[]{Enum.Parse(pageType,"Menu")});
        typeof(InventoryPanel).GetField("radialSelected",Private).SetValue(panel,2);
        yield return Capture("01-radial");
        // Real desktop mouse movement during capture must not change this routing assertion.
        typeof(InventoryPanel).GetField("radialSelected",Private).SetValue(panel,2);
        typeof(InventoryPanel).GetMethod("ConfirmRadialSelection",Private).Invoke(panel,null);
        Check(panel.IsOpen&&typeof(InventoryPanel).GetField("page",Private).GetValue(panel).ToString()=="Recipes","Seventh-option radial routes to Recipes");
        yield return Capture("02-world-recipes");
        panel.Close();yield return null;
        Check(gathering.TryOpenStation(station)&&gathering.BlocksGameplay,"Station entry point used by G opens and blocks gameplay");
        Check(!panel.OpenRecipes(),"Station prevents opening another modal");
        yield return Capture("03-station-recipes");
        Object.Destroy(station.gameObject);yield return null;yield return null;
        Check(!gathering.BlocksGameplay,"Destroyed station closes screen and releases gameplay");
        Check(panel.OpenRecipes(),"Recetario opens again after station closes");panel.Close();
        inventory.GetComponent<EquipmentLoadout>().MarkCombat();
        before=inventory.MaterialCount(hand.ingredients[0].material.id);
        Check(!inventory.TryCraftInWorld(hand)&&inventory.MaterialCount(hand.ingredients[0].material.id)==before,"Combat blocks world crafting without spending");
        Check(errors.Count==0,"No runtime or GUI errors: "+string.Join("; ",errors));
    }
}
