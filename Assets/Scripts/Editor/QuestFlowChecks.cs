using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class QuestFlowChecks
{
    const string Session="Mismo.QuestFlowChecks";
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerator routine,nested;static int frame=-1,count;static double deadline;
    static readonly List<string> errors=new List<string>();
    static Memory storage;
    static string Output=>Path.GetFullPath("output/quest-checks");
    sealed class Memory:IProfileRepository
    {
        public string data;public bool fail;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=data;return data==null?ProfileReadResult.Missing:validate(data)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
        public void Write(string value){if(fail)throw new IOException("Expected quest save failure");data=value;}
    }
    public static void Run()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project.");
        QuestIntegration.Setup();Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));Object.DestroyImmediate(player.GetComponent<RegionRespawn>());
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(60,1,60);
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4,3,-7);camera.transform.LookAt(Vector3.up);camera.gameObject.AddComponent<AudioListener>();
        new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
    }
    public static void RunVillagePreview()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project.");
        Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var settings=Object.Instantiate(ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
        var terrain=new ExplorationTerrain(settings);var origin=terrain.Site(Vector2Int.zero).position;var center=ExplorationChunks.Coordinate(origin);
        var world=new GameObject("Generated village");var material=ProjectAssets.Load<Material>("TerrainSurface");
        for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
        {
            var tile=new GameObject("Terrain");tile.transform.SetParent(world.transform);var mesh=ExplorationChunks.BuildTerrain(terrain,center+new Vector2Int(x,z));
            tile.AddComponent<MeshFilter>().sharedMesh=mesh;tile.AddComponent<MeshRenderer>().sharedMaterial=material;tile.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        new ExplorationContent(settings,terrain).Decorate(center,world.transform,material);
        var mara=Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None).First(g=>g.npc.id=="mara");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));Object.DestroyImmediate(player.GetComponent<RegionRespawn>());
        player.transform.position=mara.transform.position+mara.transform.forward*2;player.transform.rotation=Quaternion.LookRotation(-mara.transform.forward);
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position=mara.transform.position+mara.transform.forward*5+mara.transform.right*1.3f+Vector3.up*2.4f;camera.transform.LookAt(mara.transform.position+Vector3.up*1.3f);
        var cycle=new GameObject("Daylight").AddComponent<DayNightCycle>();cycle.Initialize(ProjectAssets.Load<DayNightSettings>("DayNightSettings"));cycle.SetHour(10);RenderSettings.fog=false;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool("Mismo.VillageNpcPreview",true);SessionState.SetBool(Session,true);EditorApplication.EnterPlaymode();
    }
    public static void RunTrackerPreview(){SessionState.SetBool("Mismo.QuestTrackerPreview",true);RunVillagePreview();}
    public static void RunMenuReview(){SessionState.SetBool("Mismo.MenuReview",true);Run();}
    public static void RunInventoryReview(){SessionState.SetBool("Mismo.InventoryReview",true);Run();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Isolate()
    {
        if(!SessionState.GetBool(Session,false))return;
        storage=new Memory();typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,storage);
        typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Output);
        Application.runInBackground=true;Application.logMessageReceived+=(message,stack,type)=>{if((type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search.SearchInit"))errors.Add(message);};
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void InitializePlayer()
    {
        if(!SessionState.GetBool(Session,false))return;
        var player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;
        var p=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();p.Initialize(ProjectAssets.Load<ItemCatalog>("ItemCatalog"),storage);
        if(player.GetComponent<InventoryPanel>()==null)player.gameObject.AddComponent<InventoryPanel>();
    }
    [InitializeOnLoadMethod] static void Resume(){if(!SessionState.GetBool(Session,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Step;}
    static void Step()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!Application.isPlaying||Time.frameCount<15||frame==Time.frameCount)return;frame=Time.frameCount;
        try{if(nested!=null){if(nested.MoveNext())return;nested=null;}if(routine==null)routine=SessionState.GetBool("Mismo.MenuReview",false)?MenuReview():SessionState.GetBool("Mismo.InventoryReview",false)?InventoryReview():SessionState.GetBool("Mismo.VillageNpcPreview",false)?VillagePreview():Checks();if(!routine.MoveNext())Finish(true,count+" checks passed");else if(routine.Current is IEnumerator child)nested=child;}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string result){bool preview=SessionState.GetBool("Mismo.VillageNpcPreview",false),inventory=SessionState.GetBool("Mismo.InventoryReview",false);SessionState.SetBool("Mismo.MenuReview",false);SessionState.SetBool(Session,false);SessionState.SetBool("Mismo.VillageNpcPreview",false);SessionState.SetBool("Mismo.InventoryReview",false);EditorApplication.update-=Step;File.WriteAllText(Path.Combine(Output,inventory?"InventoryReviewChecks.txt":preview?"VillagePreviewChecks.txt":"Checks.txt"),(ok?"PASS ":"FAIL ")+result);Debug.Log("QUEST_FLOW_"+(ok?"PASS ":"FAIL ")+result);EditorApplication.Exit(ok?0:1);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Debug.Log("QUEST_CHECK "+message);}
    static IEnumerator Capture(string name){for(int i=0;i<8;i++)yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(Output,name+".png"));for(int i=0;i<8;i++)yield return null;}
    static IEnumerator MenuReview()
    {
        var p=Object.FindAnyObjectByType<PlayerInventory>();var panel=p.GetComponent<InventoryPanel>();
        var page=typeof(InventoryPanel).GetField("page",Private);
        Check(p.IsReady,"Inventory initialized");
        Check((bool)typeof(InventoryPanel).GetMethod("OpenPage",Private).Invoke(panel,new[]{Enum.Parse(page.FieldType,"Inventory")}),"Inventory opens");
        yield return Capture("40-inventory");
        Check(panel.OpenRecipes(),"Recipes open");
        yield return Capture("41-recipes");
        InventoryMouse(EventType.MouseDown,new Vector2(325,126));yield return null;
        InventoryMouse(EventType.MouseUp,new Vector2(325,126));yield return null;
        var recipes=(RecipeBookView)typeof(InventoryPanel).GetField("recipeBook",Private).GetValue(panel);
        Check(recipes.Category==RecipeBookView.Filter.Materials,"Third recipe filter selects materials");
        InventoryMouse(EventType.MouseDown,QuietFantasyUI.MenuBackButton.center);yield return null;
        InventoryMouse(EventType.MouseUp,QuietFantasyUI.MenuBackButton.center);yield return null;
        Check(page.GetValue(panel).ToString()=="Menu","Recipe back button opens radial");
        Check(p.TryAcceptQuest(p.Quests.Find("Q-HERBS")),"Accept a quest for tracker review"); Check(panel.OpenQuests(),"Journal opens");
        bool hidden=QuestJournalView.TrackerHidden;
        try
        {
            yield return Capture("42-quests");
            InventoryMouse(EventType.MouseDown,new Vector2(1052,62));yield return null;
            InventoryMouse(EventType.MouseUp,new Vector2(1052,62));yield return null;
            Check(QuestJournalView.TrackerHidden!=hidden,"Journal toggle changes tracker visibility");
            Check(PlayerPrefs.GetInt("Mismo.HUD.Quests.Hidden",0)==(hidden?0:1),"Tracker visibility persists"); Check(p.QuestState(p.Quests.Find("Q-HERBS"))!=null,"Hiding preserves accepted quest");
            InventoryMouse(EventType.MouseDown,QuietFantasyUI.MenuBackButton.center);yield return null;
            InventoryMouse(EventType.MouseUp,QuietFantasyUI.MenuBackButton.center);yield return null;
            Check(page.GetValue(panel).ToString()=="Menu","Journal back button opens radial");
            panel.OpenQuests();
            InventoryMouse(EventType.MouseDown,QuietFantasyUI.MenuCloseButton.center);yield return null;
            InventoryMouse(EventType.MouseUp,QuietFantasyUI.MenuCloseButton.center);yield return null;
            Check(!panel.IsOpen,"Shared close button closes journal");
        }
        finally{QuestJournalView.TrackerHidden=hidden;}
        Check(errors.Count==0,"No runtime or GUI errors: "+string.Join("; ",errors));
    }
    static IEnumerator InventoryReview()
    {
        var p=Object.FindAnyObjectByType<PlayerInventory>();var panel=p.GetComponent<InventoryPanel>();
        var loadout=p.GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>();
        string first=p.EquippedId(0),second=p.EquippedId(1);int countBefore=p.GridItems(false).Count;
        Check(p.IsReady&&p.CanManage,"Inventory ready for equipment changes");
        storage.fail=true;string before=storage.data;
        Check(!p.UnequipToGrid(0,0,0,false)&&p.EquippedId(0)==first&&storage.data==before,"Failed save keeps equipped item and backpack unchanged");storage.fail=false;
        Check(p.UnequipToGrid(0,0,0,false)&&p.EquippedId(0)==null&&p.GridItems(false).Count==countBefore+1,"Main hand moves into backpack at requested cell");
        Check(p.GridPositions(false).Any(v=>v.key==first&&v.x==0&&v.y==0),"Dropped item keeps target grid position");
        Check(!p.UnequipToGrid(2,0,0,false)&&p.EquippedId(1)==second,"Occupied drop target preserves equipped item");
        Check(p.UnequipToGrid(2,3,0,false)&&loadout.ActiveDefinition==null&&loadout.SecondaryDefinition==null&&p.CanManage,"Both sets can be empty without blocking inventory");
        Check((bool)typeof(PlayerInventory).GetMethod("ValidatePayload",Private).Invoke(p,new object[]{storage.data}),"Save with empty hands is valid after Unity JSON round trip");
        Check(!loadout.Runner.TryUse(Mismo.Gameplay.Player.Equipment.AbilitySlot.Basic,Vector3.forward,Vector3.zero,null,true),"Empty active hand cannot attack");
        Check(p.TryEquip(0,first)&&p.TryEquip(1,second),"Weapons re-equip from fully empty loadout");
        Check(p.TrySwap()&&p.TrySwap(),"Weapon switching still works after re-equipping");
        var settings=InventorySettings.Current;int oldColumns=settings.backpackColumns,oldRows=settings.backpackRows;
        settings.backpackColumns=2;settings.backpackRows=1;
        try{Check(!p.UnequipToGrid(0,0,0,false)&&p.EquippedId(0)==first,"Insufficient backpack space rejects unequip without mutation");}
        finally{settings.backpackColumns=oldColumns;settings.backpackRows=oldRows;}
        var page=typeof(InventoryPanel).GetField("page",Private);Check((bool)typeof(InventoryPanel).GetMethod("OpenPage",Private).Invoke(panel,new[]{Enum.Parse(page.FieldType,"Inventory")}),"Inventory panel opens");
        yield return Capture("30-inventory-equipped");
        InventoryMouse(EventType.MouseDown,new Vector2(672,250));yield return null;
        InventoryMouse(EventType.MouseDrag,new Vector2(82,252));yield return null;
        InventoryMouse(EventType.MouseUp,new Vector2(82,252));yield return null;
        Check(p.EquippedId(0)==null&&p.GridItems(false).Any(i=>i.id==first),"Actual mouse drag removes main hand into backpack");
        Check(p.UnequipToGrid(2,3,0,false),"Bow returns to backpack for large item preview");
        yield return Capture("31-inventory-clean-grid");
        Check(p.TryEquip(0,first)&&p.TryEquip(1,second),"Restore equipment after dragging");
        var spare=Guid.NewGuid().ToString("N");var profile=JsonUtility.FromJson<InventoryProfile>(storage.data);
        profile.weapons.Add(new OwnedWeapon{instanceId=spare,definitionId=p.Definition(first).Id,tier=1});
        Check((bool)typeof(PlayerInventory).GetMethod("Commit",Private).Invoke(p,new object[]{profile,"Test spare sword",false,null}),"Add isolated spare for offhand test");
        Check(p.TryEquipOffhand(0,spare)&&p.UnequipToGrid(1,0,0,false)&&p.OffhandId(0)==null,"Secondary hand returns to backpack");
        Check(p.TryEquipOffhand(0,spare)&&p.UnequipToGrid(0,0,0,false)&&p.OffhandId(0)==null&&p.GridItems(false).Any(i=>i.id==spare),"Removing main hand safely returns its paired secondary too");
        Check(errors.Count==0,"Inventory presentation and empty loadout produce no runtime errors: "+string.Join("; ",errors));
    }
    static void InventoryMouse(EventType type,Vector2 position)
    {
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
        // GameView's editor chrome is outside the rendered player's IMGUI canvas.
        var pixel=new Vector2((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2)+position*scale;
        view.SendEvent(new Event{type=type,button=0,mousePosition=pixel+new Vector2(0,47)});
    }
    static IEnumerator VillagePreview()
    {
        var p=Object.FindAnyObjectByType<PlayerInventory>();var npcs=Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None);var mara=npcs.First(n=>n.npc.id=="mara");var q=p.Quests.Find("Q-HERBS");
        Check(npcs.Length==5&&p.Quests.quests.All(quest=>p.QuestState(quest)==null),"Generated village loads five residents without quests");
        Check(mara.Marker(p)=="!"&&mara.CanTalk(p,q),"Mara is physically reachable with a new order");
        yield return Capture("10-village-available");
        Check(typeof(QuestInteraction).GetField("target",Private).GetValue(p.GetComponent<QuestInteraction>())==mara,"Interaction prompt targets Mara in the generated village");
        Check(p.TryAcceptQuest(q)&&mara.Marker(p)=="…","Accepted NPC shows in-progress marker");yield return Capture("11-village-active");
        Check(p.TryGrantMaterial("MAT-01",5)&&mara.Marker(p)=="?","Collected materials update world marker");yield return Capture("12-village-ready");
        Check(mara.Interact(p),"Village NPC opens delivery dialogue");yield return Capture("13-village-dialogue");
        p.GetComponent<InventoryPanel>().Close();p.transform.position=mara.transform.position+mara.transform.right*1.6f;p.transform.rotation=mara.transform.rotation;
        var camera=Camera.main;camera.transform.position=mara.transform.position+mara.transform.forward*7+mara.transform.right*.8f+Vector3.up*2.6f;camera.transform.LookAt(mara.transform.position+mara.transform.right*.8f+Vector3.up*1.3f);
        yield return Capture("14-scale-comparison");
        if(SessionState.GetBool("Mismo.QuestTrackerPreview",false))
        {
            SessionState.SetBool("Mismo.QuestTrackerPreview",false);
            const string key="Mismo.HUD.Quests.Position";
            bool hadX=PlayerPrefs.HasKey(key+".x"),hadY=PlayerPrefs.HasKey(key+".y");float previousX=PlayerPrefs.GetFloat(key+".x"),previousY=PlayerPrefs.GetFloat(key+".y");
            var pause=Object.FindAnyObjectByType<Mismo.Menu.PauseMenu>();
            try
            {
                PlayerPrefs.DeleteKey(key+".x");PlayerPrefs.DeleteKey(key+".y");
                pause.Open();typeof(Mismo.Menu.PauseMenu).GetMethod("BeginUIEditor",Private).Invoke(pause,null);
                Check(p.TrackQuest(null),"Tracker can be positioned without a tracked quest");
                yield return Capture("19-tracker-empty");
                Check(p.TrackQuest(q),"Restore tracked quest after placeholder preview");
                yield return Capture("20-tracker-edit");
                var start=p.Quests.trackerOffset+new Vector2(35,70);var finish=start+new Vector2(110,235);
                TrackerMouse(EventType.MouseDown,start);yield return null;
                TrackerMouse(EventType.MouseDrag,finish);yield return null;
                TrackerMouse(EventType.MouseUp,finish);yield return null;
                Check(PlayerPrefs.HasKey(key+".x")&&Mathf.Abs(PlayerPrefs.GetFloat(key+".x")-(p.Quests.trackerOffset.x+110))<3&&Mathf.Abs(PlayerPrefs.GetFloat(key+".y")-(p.Quests.trackerOffset.y+235))<3,"Tracker drag stores personal position while gameplay is paused");
                yield return Capture("21-tracker-moved");
                typeof(Mismo.Menu.PauseMenu).GetMethod("EndUIEditor",Private).Invoke(pause,null);pause.Resume();
                yield return Capture("21b-tracker-saved");
                pause.Open();typeof(Mismo.Menu.PauseMenu).GetMethod("BeginUIEditor",Private).Invoke(pause,null);
                yield return Capture("21c-tracker-reopened");
                TrackerMouse(EventType.MouseDown,finish,1);yield return null;
                TrackerMouse(EventType.MouseUp,finish,1);yield return null;
                Check(!PlayerPrefs.HasKey(key+".x")&&!PlayerPrefs.HasKey(key+".y"),"Tracker right click restores catalog position");
                typeof(Mismo.Menu.PauseMenu).GetMethod("EndUIEditor",Private).Invoke(pause,null);pause.Resume();
                yield return Capture("22-tracker-left");
            }
            finally
            {
                if(hadX)PlayerPrefs.SetFloat(key+".x",previousX);else PlayerPrefs.DeleteKey(key+".x");
                if(hadY)PlayerPrefs.SetFloat(key+".y",previousY);else PlayerPrefs.DeleteKey(key+".y");
                PlayerPrefs.Save();PlayerHUD.SetUIEditMode(false);pause.Resume();
            }
        }
        Check(errors.Count==0,"No village presentation runtime errors: "+string.Join("; ",errors));
    }
    static void TrackerMouse(EventType type,Vector2 position,int button=0)
    {
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        var field=view.GetType().GetField("m_TargetInView",Private);
        var area=field!=null?(Rect)field.GetValue(view):new Rect(0,22,Screen.width,Screen.height);
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
        var pixel=position*scale;
        var mouse=area.position+new Vector2(pixel.x*area.width/Screen.width,pixel.y*area.height/Screen.height);
        view.SendEvent(new Event{type=type,button=button,mousePosition=mouse});
    }
    static IEnumerator Checks()
    {
        var p=Object.FindAnyObjectByType<PlayerInventory>();var catalog=p.Quests;var panel=p.GetComponent<InventoryPanel>();
        Check(p.IsReady&&p.CanManage,"Inventory initialized with isolated repository");
        Check(catalog.quests.Length==7&&catalog.radialSelected.Length==8,"Seven authored quests and eight radial sectors load");
        foreach(var q in catalog.quests)Check(q.Validate(out _),"Valid quest and NPC dialogue: "+q.id);
        var herbs=catalog.Find("Q-HERBS");var craft=catalog.Find("Q-PREPARE");var hunt=catalog.Find("Q-BOARS");var altar=catalog.Find("Q-ALTAR");var metal=catalog.Find("Q-IRON");var recipe=herbs.recipes[0];
        Check(!catalog.journalContacts&&catalog.quests.All(q=>p.QuestState(q)==null),"New game has no automatically accepted quests");
        var resident=Object.Instantiate(catalog.villageNpcs.residents.First(r=>r.prefab.GetComponent<QuestGiver>().npc==herbs.npc).prefab,p.transform.position+Vector3.forward*2,Quaternion.identity).GetComponent<QuestGiver>();
        Check(resident.Marker(p)=="!"&&resident.OfferedQuest(p)==herbs,"Unaccepted NPC displays available marker");
        Check(resident.Interact(p)&&p.QuestState(herbs)==null,"Talking opens an offer without accepting it");panel.Close();
        var initialView=(QuestJournalView)typeof(InventoryPanel).GetField("questJournal",Private).GetValue(panel);
        panel.OpenQuests();yield return Capture("00-empty-journal");Check(initialView.Selected==null,"Journal hides unaccepted quests");panel.Close();
        resident.transform.position=p.transform.position+Vector3.forward*20;Check(!resident.Interact(p)&&!resident.CanTalk(p,herbs),"Distant NPC cannot open or continue dialogue");resident.transform.position=p.transform.position+Vector3.forward*2;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=p.transform.position+new Vector3(0,1,1);wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();Check(!resident.CanTalk(p,herbs),"Wall blocks NPC dialogue");Object.Destroy(wall);yield return null;
        var discovery=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Quests/FourWindsAltar.prefab"),new Vector3(200,0,200),Quaternion.identity);
        Check(!discovery.GetComponent<QuestSignalTrigger>().Emit(p)&&p.QuestState(altar)==null,"Discovery does not automatically accept a quest");Object.Destroy(discovery);
        Check(!p.CanAcceptQuest(craft),"Prerequisite blocks second tutorial");
        Check(p.KnowsRecipe(ProjectAssets.LoadAll<CraftingRecipe>("Recipes").First(r=>r.id=="REC-01")),"Existing recipes remain available");
        Check(!p.KnowsRecipe(recipe)&&!p.TryCraftInWorld(recipe),"Quest recipe is locked before learning");
        Check(p.TryGrantMaterial("MAT-01",8),"Materials gathered before acceptance are usable");
        storage.fail=true;Check(!p.TryAcceptQuest(herbs)&&p.QuestState(herbs)==null,"Failed acceptance save has no local progress");storage.fail=false;
        Check(p.TryAcceptQuest(herbs)&&p.QuestReady(herbs),"Accepted collection uses current backpack");
        Check(resident.Marker(p)=="?","Ready quest changes NPC marker");
        Check(!p.TryAcceptQuest(herbs),"Cannot accept twice");
        Check(p.OpenJournalForChecks(panel),"Journal opens");
        var view=(QuestJournalView)typeof(InventoryPanel).GetField("questJournal",Private).GetValue(panel);view.Select(herbs);
        yield return Capture("01-journal-material");
        view.Contact(herbs,resident);yield return Capture("02-npc-dialogue");view.Back();panel.Close();
        storage.fail=true;int before=p.MaterialCount("MAT-01");Check(!p.TryDeliverQuest(herbs)&&p.MaterialCount("MAT-01")==before&&p.QuestCoins==0&&!p.KnowsRecipe(recipe),"Failed delivery rolls back materials, coins and recipe together");storage.fail=false;
        Check(p.TryDeliverQuest(herbs)&&p.MaterialCount("MAT-01")==3&&p.MaterialCount("MAT-05")==2&&p.QuestCoins==20&&p.KnowsRecipe(recipe),"Delivery consumes five herbs and grants exact rewards");
        Check(!p.TryDeliverQuest(herbs)&&p.QuestCoins==20,"Completed quest cannot pay twice");
        Check(resident.Marker(p)=="!"&&resident.OfferedQuest(p)==craft,"Follow-up is offered but not automatically accepted");
        Check(p.CanAcceptQuest(craft)&&p.TryAcceptQuest(craft),"Completed first tutorial unlocks crafting tutorial");
        storage.fail=true;Check(!p.TryCraftInWorld(recipe)&&p.QuestCount(craft,craft.objectives[0])==0,"Failed crafting does not advance tutorial");storage.fail=false;
        Check(p.TryCraftInWorld(recipe)&&p.QuestReady(craft)&&p.MaterialCount("MAT-05")==4,"Learning unlocks real crafting and advances objective");
        Check(p.TryDeliverQuest(craft)&&p.QuestCoins==45,"Crafting tutorial pays once");
        Check(resident.Marker(p)==""&&resident.Interact(p),"Completed NPC offers greeting without a stale marker");panel.Close();Object.Destroy(resident.gameObject);
        Check(p.TryGrantVictory(0,null,null,null,null,null,"boar"),"Pre-acceptance victory recorded");
        Check(p.TryAcceptQuest(hunt)&&p.QuestCount(hunt,hunt.objectives[0])==0,"Earlier kills do not count");
        for(int i=0;i<4;i++)Check(p.TryGrantVictory(0,null,null,"quest-check-boar-"+i,null,null,"boar"),"Victory counted "+i);
        Check(p.QuestReady(hunt),"Four new boar victories complete objective");
        p.TryGrantVictory(0,null,null,"quest-check-boar-0",null,null,"boar");Check(p.QuestCount(hunt,hunt.objectives[0])==4,"Duplicate enemy identity does not add another kill");
        Check(p.TryAcceptQuest(metal)&&p.TryGrantMaterial("MAT-06",10)&&p.QuestReady(metal),"Iron count reaches target");
        Check(p.Discard("MAT-06",true,1)&&!p.QuestReady(metal),"Spending/discarding materials makes delivery incomplete again");
        Check(!p.TryDeliverQuest(metal),"Missing material blocks delivery");
        var crowded=Object.Instantiate(metal);crowded.id="Q-CAPACITY-CHECK";crowded.objectives=new[]{new QuestObjective{id="iron",label="Entregá hierro",kind=QuestObjectiveKind.Material,material=p.Material("MAT-06"),quantity=1}};
        crowded.items=new[]{new QuestItemReward{material=p.Material("MAT-03"),quantity=p.BackpackCapacity*Mathf.Max(1,p.Material("MAT-03").stackSize)}};crowded.recipes=Array.Empty<CraftingRecipe>();
        var originalQuests=catalog.quests;catalog.quests=originalQuests.Concat(new[]{crowded}).ToArray();
        Check(p.TryAcceptQuest(crowded),"Capacity check quest accepted");int ironBefore=p.MaterialCount("MAT-06"),coinsBefore=p.QuestCoins;
        Check(!p.TryDeliverQuest(crowded)&&p.MaterialCount("MAT-06")==ironBefore&&p.QuestCoins==coinsBefore&&!p.QuestState(crowded).completed,"Full backpack preserves costs and pending reward");
        catalog.quests=originalQuests;Object.Destroy(crowded);
        Check(p.TryAcceptQuest(altar),"World may start hidden mystery quest");
        Check(!p.TryRecordQuestSignal("altar.solved","forest.altar"),"Puzzle completion before discovery is rejected");
        Check(p.TryRecordQuestSignal("altar.found","forest.altar")&&!p.TryRecordQuestSignal("altar.found","forest.altar"),"Discovery signal is idempotent");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Quests/FourWindsAltar.prefab");var instance=Object.Instantiate(prefab,new Vector3(20,0,20),Quaternion.identity);var puzzle=instance.GetComponent<CompassAltar>();
        Check(!puzzle.Matches(),"Altar starts unsolved");
        for(int i=0;i<puzzle.stones.Length;i++)puzzle.stones[i].SetDirection(puzzle.solution[i]);
        storage.fail=true;Check(!puzzle.TrySolve(p)&&!p.QuestReady(altar),"Failed puzzle save can retry without progress");storage.fail=false;
        Check(puzzle.TrySolve(p)&&puzzle.IsOpened&&p.QuestReady(altar),"Correct cardinal arrangement persists solution");
        Check(!puzzle.TrySolve(p),"Solved altar does not emit duplicate reward progress");
        var restored=Object.Instantiate(prefab,new Vector3(40,0,40),Quaternion.identity).GetComponent<CompassAltar>();restored.Restore(p);Check(restored.IsOpened&&restored.Matches(),"Saved altar restores solved state");
        Check(p.TrackQuest(altar),"Tracking persists");
        storage.fail=true;Check(!p.TrackQuest(hunt)&&p.TrackedQuest==altar,"Failed tracking save preserves previous target");storage.fail=false;
        var saved=JsonUtility.FromJson<InventoryProfile>(storage.data);Check(saved.version==6&&saved.trackedQuest==altar.id&&saved.learnedRecipes.Contains(recipe.id)&&QuestRules.ValidSave(saved),"Quest data round-trips through profile v6");
        var copy=saved.Copy();copy.quests[0].completed=false;Check(saved.quests[0].completed,"Copies isolate quest progress");
        var legacy=JsonUtility.FromJson<InventoryProfile>(storage.data);legacy.version=5;legacy.quests=null;legacy.learnedRecipes=null;legacy.trackedQuest=null;legacy.UpgradeToCurrent();Check(legacy.version==6&&QuestRules.ValidSave(legacy),"Version-five profiles migrate with empty quest state");
        saved.quests.Add(saved.quests[0].Copy());Check(!QuestRules.ValidSave(saved),"Malformed duplicate quest state rejected");
        panel.OpenQuests();view.Select(altar);yield return Capture("03-puzzle-journal");panel.Close();yield return Capture("04-tracker");
        var radial=typeof(InventoryPanel).GetMethod("RadialIndex",BindingFlags.Static|BindingFlags.NonPublic);
        for(int i=0;i<8;i++){float angle=(-90+i*45)*Mathf.Deg2Rad;Check((int)radial.Invoke(null,new object[]{new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*157})==i,"Eight-sector radial hit "+i);}
        var pageType=typeof(InventoryPanel).GetNestedType("Page",BindingFlags.NonPublic);typeof(InventoryPanel).GetMethod("OpenPage",Private).Invoke(panel,new[]{Enum.Parse(pageType,"Menu")});typeof(InventoryPanel).GetField("radialSelected",Private).SetValue(panel,7);yield return Capture("05-radial");typeof(InventoryPanel).GetField("radialSelected",Private).SetValue(panel,7);typeof(InventoryPanel).GetMethod("ConfirmRadialSelection",Private).Invoke(panel,null);Check(panel.IsOpen&&typeof(InventoryPanel).GetField("page",Private).GetValue(panel).ToString()=="Quests","Radial routes to quests");
        Check(errors.Count==0,"No runtime/GUI errors: "+string.Join("; ",errors));
        panel.Close();p.GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>().MarkCombat();
        Check(p.TryStartQuestEvent(catalog.Find("Q-RAID")),"World event can start during combat");
        Check(p.TryRecordQuestSignal("raid.defended","raid:test:first")&&p.QuestReady(catalog.Find("Q-RAID")),"Event controller signal advances its quest");
        Check(!p.TryDeliverQuest(hunt),"Combat still blocks reward delivery");
    }
    static bool OpenJournalForChecks(this PlayerInventory p,InventoryPanel panel)=>panel.OpenQuests();
}
