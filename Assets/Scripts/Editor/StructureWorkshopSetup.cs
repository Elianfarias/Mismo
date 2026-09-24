using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class StructureWorkshopSetup
{
    public const string DefRoot="Assets/Data/World/Structures/";
    public const string Output="output/structures";
    public static string ScenePath(StructureDefinition d)=>"Assets/Scenes/Previews/Structures/"+d.name+".unity";
    static GameObject Cave(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/CaveKit/"+name+".prefab");
    static GameObject Grove(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/EnchantedGrove/"+name+".prefab");
    static StructureDefinition Preset(string name,int seed,StructureStyle style)
    {
        var existing=AssetDatabase.LoadAssetAtPath<StructureDefinition>(DefRoot+name+".asset");if(existing!=null)return existing;
        var d=ScriptableObject.CreateInstance<StructureDefinition>();d.name=name;d.style=style;d.layoutSeed=seed;d.decorationSeed=seed+151;
        d.displayName=style==StructureStyle.Cave?"Gruta del manantial olvidado":style==StructureStyle.Ruin?"Ruinas del guardián":"Santuario del bosque";
        d.shellMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/CaveKit/CavePalette.mat");
        d.entranceArch=style==StructureStyle.Cave?Cave("R05_Arco_entrada"):Grove("Ruin_Arch");
        d.finalLandmark=Grove("Ruin_Arch");
        d.rewardVisual=Cave("C01_Cristal_solitario");
        if(name=="Cave_Crystal"){d.displayName="Gruta del cristal azul";d.lightColor=new Color(.28f,.55f,1);d.decorationDensity=.85f;}
        d.floorDetails=new[]{Cave("H04_Colonia"),Cave("V02_Helecho_compacto"),Cave("C02_Racimo_bajo"),Grove("Mushrooms"),Grove("Fern"),Cave("P03_Grupo_grava")}.Where(p=>p!=null).ToArray();
        d.ceilingDetails=new[]{Cave("T01_Aguja_corta"),Cave("T02_Racimo_tres_puntas")}.Where(p=>p!=null).ToArray();
        d.wallDetails=style==StructureStyle.Cave?new[]{Cave("Z02_Cortina_raices"),Grove("Vines_Long")}:new[]{Grove("Ruin_Pillar"),Grove("Ruin_Wall")};
        d.GenerateLayout();AssetDatabase.CreateAsset(d,DefRoot+name+".asset");return d;
    }
    public static void Register(StructureDefinition d,GameObject prefab)
    {
        var catalog=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset");
        if(catalog==null)throw new InvalidOperationException("Falta WorldContentCatalog");
        string id="structure."+d.name;
        Undo.RecordObject(catalog,"Registrar estructura");
        var entries=(catalog.structures??Array.Empty<WorldStructureEntry>()).Where(e=>e!=null&&e.id!=id).ToList();
        entries.Add(new WorldStructureEntry{id=id,prefab=prefab.GetComponent<StructureInstance>(),site=WorldSiteKind.Secret,placement=d.placement,biomes=d.worldBiomes.Length>0?d.worldBiomes:new[]{WorldBiome.Highlands,WorldBiome.Mountains}});
        catalog.structures=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
    public static void CreatePlayground(StructureDefinition d,GameObject prefab)
    {
        string scenePath=ScenePath(d);
        string groundPath=StructureBaker.MeshRoot+d.name+"_PreviewGround.asset";
        StructureBaker.SaveMesh(StructureExterior.Ground(d),groundPath);AssetDatabase.SaveAssets();
        StructureBaker.Folder("Assets/Scenes/Previews/Structures");
        var previous=SceneManager.GetActiveScene();bool emptyBatch=Application.isBatchMode&&string.IsNullOrEmpty(previous.path);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyBatch?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            var groundMesh=AssetDatabase.LoadAssetAtPath<Mesh>(groundPath);var groundMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");
            var instance=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<StructureInstance>();
            StructureContentPreview.Create(instance);
            var floor=new GameObject("Exterior de pruebas");floor.AddComponent<MeshFilter>().sharedMesh=groundMesh;floor.AddComponent<MeshRenderer>().sharedMaterial=groundMaterial;floor.AddComponent<MeshCollider>().sharedMesh=groundMesh;
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"),scene);
            foreach(var component in player.GetComponents<RegionRespawn>())Object.DestroyImmediate(component);
            foreach(var component in player.GetComponents<WorldCheckpoint>())Object.DestroyImmediate(component);
            player.transform.position=instance.entrance+new Vector3(0,.2f,-1.5f);player.transform.rotation=Quaternion.identity;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.1f;camera.farClipPlane=160;camera.fieldOfView=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.46f,.51f);
            camera.gameObject.AddComponent<AudioListener>();var follow=camera.gameObject.AddComponent<ThirdPersonCamera>();follow.Configure(player.transform,player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>());player.GetComponent<PlayerController>().Configure(camera.transform);
            var settings=new SerializedObject(follow);settings.FindProperty("distance").floatValue=5.5f;settings.ApplyModifiedPropertiesWithoutUndo();
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-28,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.3f,.35f,.39f);RenderSettings.ambientEquatorColor=new Color(.2f,.24f,.26f);RenderSettings.ambientGroundColor=new Color(.10f,.12f,.14f);RenderSettings.skybox=null;RenderSettings.fog=false;
            var playground=new GameObject("Structure playground").AddComponent<StructurePlayground>();playground.player=player.GetComponent<PlayerController>();playground.structure=instance;
            EditorSceneManager.SaveScene(scene,scenePath);
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else EditorSceneManager.CloseScene(scene,true);
        }
    }
    public static void RunBatch()
    {
        try
        {
            Directory.CreateDirectory(Output);StructureBaker.Folder(DefRoot);
            // Keep the generated palette's emission enabled after URP material validation.
            foreach(var name in new[]{"CavePalette","CaveWet"})
            {var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/CaveKit/"+name+".mat");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);}
            Preset("Cave_Moss",7319,StructureStyle.Cave);Preset("Cave_Crystal",9281,StructureStyle.Cave);Preset("Ruin_Grove",481,StructureStyle.Ruin);
            bool newTemple=AssetDatabase.LoadAssetAtPath<StructureDefinition>(DefRoot+"Temple_Grove.asset")==null;
            var temple=Preset("Temple_Grove",2451,StructureStyle.Temple);if(newTemple){temple.mainRooms=3;temple.GenerateLayout();}
            foreach(var name in new[]{"Cave_Moss","Cave_Crystal","Ruin_Grove","Temple_Grove"})
            {
                var d=AssetDatabase.LoadAssetAtPath<StructureDefinition>(DefRoot+name+".asset");
                d.worldStoneSurface=true;d.shellMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");d.stoneStyle=AssetDatabase.LoadAssetAtPath<WorldContentCatalog>("Assets/Data/World/WorldContentCatalog.asset").groveStyle;
                d.waterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/EnchantedGrove/PondWater.mat");
                d.worldBiomes=d.style==StructureStyle.Temple?new[]{WorldBiome.Meadow,WorldBiome.Forest}:new[]{WorldBiome.Highlands,WorldBiome.Mountains};
                d.placement=name=="Cave_Crystal"?StructurePlacement.IceEscarpment:StructurePlacement.CompatibleSites;
                d.exteriorDetails=d.style==StructureStyle.Temple?new[]{Grove("Tree_Sage"),Grove("Tree_Cypress"),Grove("Tree_Blossom"),Grove("Shrub_Hydrangea"),Grove("Lily_Pads"),Grove("Reeds")}:new[]{Grove("Rock_Moss_Large"),Grove("Rock_Moss_Small"),Cave("R04_Pilar_rocoso")};
                if(d.style==StructureStyle.Temple){d.displayName="Templo del estanque olvidado";d.lightColor=new Color(.8f,.68f,.35f);d.interiorAmbient=.25f;d.decorationDensity=.85f;d.wallDetails=new[]{Grove("Vines_Long"),Grove("Vines_Short")};}
                if(name=="Cave_Crystal"){d.displayName="Santuario de la escarpa helada";d.mountainHeight=22;}
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
            foreach(var name in new[]{"Cave_Moss","Cave_Crystal","Ruin_Grove","Temple_Grove"})
            {var d=AssetDatabase.LoadAssetAtPath<StructureDefinition>(DefRoot+name+".asset");var prefab=StructureBaker.Bake(d);if(d.style!=StructureStyle.Ruin)Register(d,prefab);CreatePlayground(d,prefab);}
            StructureWorkshopChecks.Run();
            try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
            catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.Message);Debug.LogWarning("Project organization issues: "+Output+"/organization.txt");}
            Debug.Log("STRUCTURE_WORKSHOP_OK");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
