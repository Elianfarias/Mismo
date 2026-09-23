using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class AdventureFeedbackChecks
{
    const string Output = "output/adventure-feedback";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void RunAndPlayBatch()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use an isolated validation project");
        Run();
        if(!File.ReadAllText(Output+"/checks.txt").StartsWith("PASS")){EditorApplication.Exit(1);return;}
        AdventureFeedbackPlayChecks.RunBatch();
    }
    public static void RunInteractionRevisionBatch()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use an isolated validation project");
        Directory.CreateDirectory(Output);
        try { File.WriteAllText(Output+"/checks.txt","PASS: interaction revision\n"); CheckInteraction(); Preview(); AdventureFeedbackPlayChecks.RunBatch(); }
        catch(Exception e) { File.WriteAllText(Output+"/checks.txt","FAIL\n"+e);Debug.LogException(e);EditorApplication.Exit(1); }
    }
    [InitializeOnLoadMethod] static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Temp/AdventureFeedbackChecks.request")) return;
        File.Delete("Temp/AdventureFeedbackChecks.request"); Run();
    }
    [MenuItem("Mismo/Feedback/Verificar avisos de aventura")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            SetupFont();
            var rules = ScriptableObject.CreateInstance<ProgressionRules>();
            try
            {
                var before = new ProgressionData { level=1, experience=50 };
                var after = before.Copy(); rules.Grant(after, 200, null);
                if (AdventureFeedback.ExperienceDelta(before,after,rules)!=200 || after.level!=3) throw new Exception("XP lost across multiple levels");
                if (AdventureFeedback.ExperienceDelta(after,after,rules)!=0) throw new Exception("Unchanged state creates XP");
                if (PlayerInteraction.InteractionKey!=UnityEngine.InputSystem.Key.F) throw new Exception("Interaction must use F");
                var catalog=AssetDatabase.LoadAssetAtPath<Mismo.Gameplay.Player.Quests.QuestCatalog>("Assets/Data/Quests/QuestCatalog.asset");
                if(catalog.interactKey!=PlayerInteraction.InteractionKey)throw new Exception("Quest catalog key disagrees with F");
                var font=ProjectAssets.Load<TMP_FontAsset>("UI/AdventureFont");
                if(font==null||font.material==null||font.atlasTexture==null)throw new Exception("Font or dependencies missing");
                File.WriteAllText(Output+"/checks.txt","PASS: XP across multiple levels, unchanged state, unified F and serialized font dependencies.\n");
            }
            finally { Object.DestroyImmediate(rules); }
            CheckInteraction();
            Preview();
        }
        catch(Exception e) { File.WriteAllText(Output+"/checks.txt","FAIL\n"+e); Debug.LogException(e); }
        try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output+"/organization.txt","PASS"); }
        catch(Exception e) { File.WriteAllText(Output+"/organization.txt",e.ToString()); }
        try
        {
            Directory.CreateDirectory(".validation/AdventureContent");
            var manifest=BuildPipeline.BuildAssetBundles(".validation/AdventureContent",new[]{new AssetBundleBuild
            {assetBundleName="adventure-font",assetNames=new[]{"Assets/Data/UI/Fonts/Adventure Cagliostro SDF.asset","Assets/Data/UI/Adventure Text Settings.asset"}}},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
            if(manifest==null)throw new Exception("Font content build failed");
            var bundle=AssetBundle.LoadFromFile(".validation/AdventureContent/adventure-font");
            if(bundle==null)throw new Exception("Font bundle could not be opened");
            try { var font=bundle.LoadAllAssets<TMP_FontAsset>().FirstOrDefault(); if(font==null||font.material==null||font.atlasTexture==null)throw new Exception("Font missing from built content"); }
            finally { bundle.Unload(true); }
            File.WriteAllText(Output+"/content-build.txt","PASS: built and loaded font, material and atlas for Windows.");
        }
        catch(Exception e) { File.WriteAllText(Output+"/content-build.txt","FAIL\n"+e); }
    }
    [MenuItem("Mismo/Feedback/Preparar tipografía de aventura")]
    public static void SetupFont()
    {
        const string path="Assets/Data/UI/Fonts/Adventure Cagliostro SDF.asset";
        const string settingsPath="Assets/Data/UI/Adventure Text Settings.asset";
        var settings=AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
        if(settings==null){settings=ScriptableObject.CreateInstance<TMP_Settings>();AssetDatabase.CreateAsset(settings,settingsPath);}
        ProjectTextSettings.Use(settings);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        const string spanish="¡¿áéíóúñÁÉÍÓÚÑüÜ·…";
        if(font==null)
        {
            var source=ProjectAssets.Load<Font>("Fonts/Cagliostro-Regular");
            font=TMP_FontAsset.CreateFontAsset(source,64,7,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
            font.name="Adventure Cagliostro SDF";
            font.TryAddCharacters(new string(Enumerable.Range(32,95).Select(c=>(char)c).ToArray())+spanish,out string missing);
            if(!string.IsNullOrEmpty(missing))throw new Exception("Missing font glyphs: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.CreateAsset(font,path);
            foreach(var atlas in font.atlasTextures){atlas.name="Adventure font atlas";AssetDatabase.AddObjectToAsset(atlas,font);}
            font.material.name="Adventure font material";AssetDatabase.AddObjectToAsset(font.material,font);
        }
        foreach(char c in spanish)if(!font.HasCharacter(c))throw new Exception("Missing Spanish character: "+c);
        var catalog=AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(ProjectAssets.CatalogPath);
        var entry=catalog.entries.FirstOrDefault(e=>e.key=="UI/AdventureFont");
        if(entry==null){entry=new RuntimeAssetCatalog.Entry{key="UI/AdventureFont"};catalog.entries=catalog.entries.Concat(new[]{entry}).ToArray();}
        entry.assets=new Object[]{font};catalog.Invalidate();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(font);AssetDatabase.SaveAssetIfDirty(catalog);
        var serialized=new SerializedObject(settings);
        serialized.FindProperty("m_defaultFontAsset").objectReferenceValue=font;
        serialized.FindProperty("assetVersion").stringValue="2";
        serialized.FindProperty("m_defaultFontSize").floatValue=24;
        serialized.FindProperty("m_defaultAutoSizeMinRatio").floatValue=.5f;
        serialized.FindProperty("m_defaultAutoSizeMaxRatio").floatValue=2;
        serialized.FindProperty("m_TextWrappingMode").intValue=1;
        serialized.FindProperty("m_EnableRaycastTarget").boolValue=false;
        serialized.FindProperty("m_ActiveFontFeatures").arraySize=0;
        serialized.FindProperty("m_defaultTextMeshProUITextContainerSize").vector2Value=new Vector2(200,50);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var settingsEntry=catalog.entries.FirstOrDefault(e=>e.key=="UI/TextSettings");
        if(settingsEntry==null){settingsEntry=new RuntimeAssetCatalog.Entry{key="UI/TextSettings"};catalog.entries=catalog.entries.Concat(new[]{settingsEntry}).ToArray();}
        settingsEntry.assets=new Object[]{settings};catalog.Invalidate();EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(settings);AssetDatabase.SaveAssetIfDirty(catalog);
    }
    static void CheckInteraction()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var owner=new GameObject("Interaction test");SceneManager.MoveGameObjectToScene(owner,scene);
            var inventory=owner.AddComponent<PlayerInventory>();
            var loadout=owner.AddComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>();
            var runner=owner.AddComponent<Mismo.Gameplay.Player.Equipment.AbilityRunner>();
            typeof(Mismo.Gameplay.Player.Equipment.EquipmentLoadout).GetField("runner",Private).SetValue(loadout,runner);
            typeof(PlayerInventory).GetField("loadout",Private).SetValue(inventory,loadout);
            typeof(PlayerInventory).GetField("profile",Private).SetValue(inventory,new InventoryProfile());
            var selector=owner.AddComponent<PlayerInteraction>();typeof(PlayerInteraction).GetMethod("Awake",Private).Invoke(selector,null);
            loadout.MarkCombat();
            if(inventory.CanManage||!selector.Available)throw new Exception("Combat grace must block equipment changes, not idle gathering or ground loot");
            int near=0,far=0;
            PlayerInteraction.Offer(inventory,owner,Vector3.right*2,"Lejos","",()=>far++);
            PlayerInteraction.Offer(inventory,selector,Vector3.right,"Cerca","",()=>near++);
            typeof(PlayerInteraction).GetMethod("Resolve",Private).Invoke(selector,new object[]{true});
            PlayerInteraction.Offer(inventory,owner,Vector3.zero,"Repetido","",()=>far++);
            typeof(PlayerInteraction).GetMethod("Resolve",Private).Invoke(selector,new object[]{true});
            if(near!=1||far!=0)throw new Exception("F must execute only the nearest action once per frame");
            File.AppendAllText(Output+"/checks.txt","PASS: interaction selects nearest action and cannot activate two targets in the same frame.\n");
            CheckLoot(inventory);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    sealed class MemoryStorage : IProfileRepository
    {
        public bool fail;
        public string json;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=json;return json==null?ProfileReadResult.Missing:ProfileReadResult.Loaded;}
        public void Write(string payload){if(fail)throw new IOException("Intentional validation failure");json=payload;}
    }
    static void CheckLoot(PlayerInventory inventory)
    {
        var weapon=ScriptableObject.CreateInstance<Mismo.Gameplay.Player.Equipment.WeaponDefinition>();
        var catalog=ScriptableObject.CreateInstance<ItemCatalog>();catalog.weapons=new[]{weapon};
        var storage=new MemoryStorage();
        try
        {
            var profile=new InventoryProfile();
            profile.weapons.Add(new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=weapon.Id});
            profile.weapons.Add(new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=weapon.Id});
            profile.equipped[0]=profile.weapons[0].instanceId;profile.equipped[1]=profile.weapons[1].instanceId;
            typeof(PlayerInventory).GetField("profile",Private).SetValue(inventory,profile);
            typeof(PlayerInventory).GetField("catalog",Private).SetValue(inventory,catalog);
            typeof(PlayerInventory).GetField("repository",Private).SetValue(inventory,storage);
            typeof(PlayerInventory).GetField("writable",Private).SetValue(inventory,true);
            ((System.Collections.Generic.HashSet<string>)typeof(PlayerInventory).GetField("definitions",Private).GetValue(inventory)).Add(weapon.Id);
            int events=0;inventory.Committed+=(a,b,c)=>events++;
            var drop=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=weapon.Id};
            if(!inventory.TryGrantVictory(25,null,drop,"validation:enemy",null,Vector3.zero))throw new Exception("Victory rejected");
            if(inventory.Count!=2||inventory.PendingLoot.Count()!=1||events!=1)throw new Exception("Drop went directly to backpack");
            if(!string.IsNullOrEmpty(inventory.Notice))throw new Exception("Ground drop generated an unsolicited text notice");
            if(!inventory.PendingLootName(inventory.PendingLoot.First()).Contains(weapon.DisplayName))throw new Exception("Ground loot prompt lacks the actual weapon name");
            string id=inventory.PendingLoot.First().id;
            if(JsonUtility.FromJson<InventoryProfile>(storage.json).pendingLoot.Count!=1)throw new Exception("Ground drop was not persisted");
            inventory.TryGrantVictory(25,null,drop,"validation:enemy",null,Vector3.zero);
            if(inventory.PendingLoot.Count()!=1||events!=1)throw new Exception("Victory retry duplicated loot or notification");
            storage.fail=true;
            if(inventory.CollectPending(id)||inventory.Count!=2||inventory.PendingLoot.Count()!=1||events!=1)throw new Exception("Failed save consumed ground loot or announced success");
            storage.fail=false;
            int width=weapon.gridWidth,height=weapon.gridHeight;weapon.gridWidth=weapon.gridHeight=12;
            if(inventory.CollectPending(id)||inventory.PendingLoot.Count()!=1||events!=1)throw new Exception("Full backpack consumed ground loot");
            weapon.gridWidth=width;weapon.gridHeight=height;
            if(!inventory.CollectPending(id)||inventory.Count!=3||inventory.PendingLoot.Any()||events!=2)throw new Exception("Explicit pickup did not transfer ground loot exactly once");
            if(inventory.CollectPending(id)||inventory.Count!=3)throw new Exception("Pickup duplicated loot");
            var loadout=inventory.GetComponent<Mismo.Gameplay.Player.Equipment.EquipmentLoadout>();
            loadout.Configure(inventory.gameObject.AddComponent<Mismo.Gameplay.Player.Equipment.EquippedWeapon>(),null);
            var slots=(Mismo.Gameplay.Player.Equipment.WeaponDefinition[])typeof(Mismo.Gameplay.Player.Equipment.EquipmentLoadout).GetField("slots",Private).GetValue(loadout);
            slots[0]=slots[1]=weapon;
            if(!inventory.TrySwap()||!string.IsNullOrEmpty(inventory.Notice))throw new Exception("Tab swap failed or displayed a text notice");
            File.AppendAllText(Output+"/checks.txt","PASS: Tab swaps silently; ground drop has no notice and exposes its weapon name; pickup works during combat grace.\n");
            File.AppendAllText(Output+"/checks.txt","PASS: enemy drops stay on ground, persist, resist duplicate victories and failed saves, and transfer only on explicit pickup.\n");
        }
        finally { Object.DestroyImmediate(catalog);Object.DestroyImmediate(weapon); }
    }
    static void Preview()
    {
        var scene=EditorSceneManager.NewPreviewScene(); RenderTexture target=null; Texture2D image=null;
        try
        {
            var owner=new GameObject("Adventure preview"); SceneManager.MoveGameObjectToScene(owner,scene);
            owner.AddComponent<PlayerInventory>(); owner.AddComponent<PlayerInteraction>();
            var view=owner.AddComponent<AdventureFeedback>();
            typeof(AdventureFeedback).GetMethod("Awake",Private).Invoke(view,null);
            var canvas=owner.GetComponentInChildren<Canvas>();
            var cameraObject=new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>(); camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.11f,.15f,.16f); camera.transform.position=new Vector3(0,0,-10);
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            foreach(var group in canvas.GetComponentsInChildren<CanvasGroup>())group.alpha=1;
            SetCard(canvas,"Interaction","Recoger","Espada básica · T2");
            SetCard(canvas,"Experience","+125 EXP","Nivel 8 · 125 / 270");
            SetCard(canvas,"Level and mastery","¡Subiste de nivel!","Nivel 8 · +1 punto de atributo");
            SetCard(canvas,"Rewards","","+3 Piedra");
            target=new RenderTexture(1280,800,24); camera.targetTexture=target;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=target;
            try { image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply(); }
            finally { RenderTexture.active=previous; }
            File.WriteAllBytes(Output+"/preview.png",image.EncodeToPNG());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if(target!=null)Object.DestroyImmediate(target);if(image!=null)Object.DestroyImmediate(image);
        }
    }
    static void SetCard(Canvas canvas,string name,string title,string body)
    {
        var texts=canvas.transform.Find(name).GetComponentsInChildren<TextMeshProUGUI>(true);texts[0].text=title;texts[1].text=body;
    }
    [MenuItem("Mismo/Feedback/Probar subida de nivel (Play Mode)")]
    static void Demo()
    {
        var view=Object.FindAnyObjectByType<AdventureFeedback>();
        if(!Application.isPlaying||view==null){Debug.Log("Entrá en Play Mode con un jugador para probar los avisos.");return;}
        var before=new ProgressionData {level=7,experience=200};var after=new ProgressionData {level=8,experience=125};
        typeof(AdventureFeedback).GetMethod("Committed",Private).Invoke(view,new object[]{before,after,"Espada del guardián · Tier 2"});
    }
}
