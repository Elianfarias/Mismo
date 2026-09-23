using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GroveWorldPlayChecks
{
    const string Key="GroveWorldPlayChecks";
    static IEnumerator routine;static int frame=-1;static double deadline;
    sealed class Memory:IProfileRepository
    {
        string json;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=json;return json==null?ProfileReadResult.Missing:validate(json)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
        public void Write(string payload){json=payload;}
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode||!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Isolated batch project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
        Object.DestroyImmediate(player.GetComponent<RegionRespawn>());player.GetComponent<PlayerController>().enabled=false;player.GetComponent<PlayerMotor>().enabled=false;
        var body=player.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=true;
        new GameObject("Test camera").AddComponent<Camera>().tag="MainCamera";
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!Application.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
        try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,"Live streamer loaded 25 chunks; new tree LODs and wind rendered; harvested a tree; stable resource IDs and depletion survived chunk unload/reload. In-memory inventory only.");}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static IEnumerator Run()
    {
        var player=Object.FindAnyObjectByType<PlayerController>();
        var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),new Memory());Require(inventory.IsReady,"Inventory not initialized");
        var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.generationVersion=2;settings.preserveAuthoredCenter=false;settings.loadRadius=2;
        settings.content=Object.Instantiate(settings.content);settings.content.encounters=Array.Empty<WorldEncounterEntry>();settings.content.clearingChance=0;settings.content.roamingChance=0;
        var field=new ExplorationTerrain(settings);Vector3 start=Vector3.zero;bool found=false;
        for(int z=-8;z<16&&!found;z++)for(int x=-8;x<16&&!found;x++)
        {
            float px=x*32+16,pz=z*32+16;
            if(field.Biome(px,pz)==WorldBiome.Forest&&!field.Reserved(px,pz,12)){start=new Vector3(px,field.Height(px,pz)+1,pz);found=true;}
        }
        Require(found,"No forest location");player.transform.position=start;
        var region=new GameObject("Streaming integration test");var geometry=new GameObject("RegionGeometry");geometry.transform.SetParent(region.transform);
        var seedTerrain=new GameObject("Terrain_0_0");seedTerrain.transform.SetParent(geometry.transform);seedTerrain.AddComponent<MeshFilter>().sharedMesh=ExplorationChunks.BuildTerrain(field,ExplorationChunks.Coordinate(start));seedTerrain.AddComponent<MeshRenderer>().sharedMaterial=Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface");
        var streaming=region.AddComponent<ExplorationChunks>();streaming.Initialize(settings,player.transform);
        for(int i=0;i<150&&streaming.LoadedCount<25;i++)yield return null;
        for(int i=0;i<10;i++)yield return null;
        Require(streaming.LoadedCount>=25,"Streaming failed to fill neighbourhood");
        var tree=region.GetComponentsInChildren<GatheringNode>().FirstOrDefault(n=>n.Available&&n.definition.kind==ResourceNodeKind.Tree&&n.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.name=="Tree_Sage"));
        Require(tree!=null,"New harvestable tree missing");
        Require(tree.GetComponentInChildren<LODGroup>()!=null,"Tree lost its LOD group");
        Require(tree.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial.shader.name=="Mismo/Vegetation Motion"),"Tree lost wind material");
        string id=tree.persistentId;var treePosition=tree.transform.position;
        // Find a clear interaction point around the actual new trunk collider.
        bool harvested=false;
        for(int i=0;i<12&&!harvested;i++)
        {
            float a=i*Mathf.PI/6;player.transform.position=treePosition+new Vector3(Mathf.Cos(a)*1.25f,.5f,Mathf.Sin(a)*1.25f);Physics.SyncTransforms();harvested=tree.Complete(inventory);
        }
        Require(harvested&&!tree.Available,"New tree cannot be harvested");
        player.transform.position=start+new Vector3(256,0,0);player.transform.position=new Vector3(player.transform.position.x,field.Height(player.transform.position.x,player.transform.position.z)+1,player.transform.position.z);
        for(int i=0;i<160&&tree!=null;i++)yield return null;
        Require(tree==null,"Original chunk did not unload");player.transform.position=start;
        GatheringNode reloaded=null;
        for(int i=0;i<180&&reloaded==null;i++){yield return null;reloaded=region.GetComponentsInChildren<GatheringNode>().FirstOrDefault(n=>n.persistentId==id);}
        Require(reloaded!=null,"Resource ID changed after reload");for(int i=0;i<5;i++)yield return null;
        Require(!reloaded.Available,"Harvested tree respawned immediately after streaming");
    }
    static void Finish(bool pass,string message)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Directory.CreateDirectory("output/grove-world");File.WriteAllText("output/grove-world/play-mode.txt",(pass?"PASS: ":"FAIL: ")+message);Debug.Log("GROVE_WORLD_PLAY_"+(pass?"PASS ":"FAIL ")+message);EditorApplication.Exit(pass?0:1);
    }
}
