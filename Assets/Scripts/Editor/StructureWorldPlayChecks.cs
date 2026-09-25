using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StructureWorldPlayChecks
{
    const string Key="StructureWorldPlayChecks";
    static IEnumerator routine;static int frame=-1;static double deadline;
    sealed class Memory:IProfileRepository
    {
        public string json;public int writes;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=json;return json==null?ProfileReadResult.Missing:validate(json)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
        public void Write(string payload){json=payload;writes++;}
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode||!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Isolated batch project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
        Object.DestroyImmediate(player.GetComponent<RegionRespawn>());Object.DestroyImmediate(player.GetComponent<WorldCheckpoint>());player.GetComponent<PlayerController>().enabled=false;
        var camera=new GameObject("Test camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+360;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!Application.isPlaying||Time.frameCount<5||frame==Time.frameCount)return;frame=Time.frameCount;
        try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,"World streamer loaded both the woodland temple and ice cave; the real character crossed the temple bridge and walked from outside to each final room; both rewards persisted once in memory and stayed consumed after chunk unload/reload. No player save used.");}
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static IEnumerator Run()
    {
        var player=Object.FindAnyObjectByType<PlayerController>();var motor=player.GetComponent<PlayerMotor>();motor.AnimationMovementRequested=null;
        var memory=new Memory();var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),memory);Require(inventory.IsReady,"Inventory not initialized");
        var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.generationVersion=2;settings.preserveAuthoredCenter=false;settings.loadRadius=2;settings.seed=7319;
        settings.content=Object.Instantiate(settings.content);settings.content.encounters=Array.Empty<WorldEncounterEntry>();settings.content.clearingChance=0;settings.content.roamingChance=0;
        var field=new ExplorationTerrain(settings);
        Require(field.WoodlandStructure.HasValue&&field.IceStructure.HasValue,"Missing temple or ice cave landmark");
        foreach(var site in new[]{field.WoodlandStructure.Value,field.IceStructure.Value})
        {
            var exercise=Exercise(player,motor,inventory,memory,settings,field,site);
            while(exercise.MoveNext())yield return exercise.Current;
        }
    }
    static IEnumerator Exercise(PlayerController player,PlayerMotor motor,PlayerInventory inventory,Memory memory,ExplorationWorldSettings settings,ExplorationTerrain field,WorldSite site)
    {
        var origin=site.position;origin.y=field.Height(origin.x,origin.z)+.05f;
        var start=origin+site.structure.prefab.entrance+new Vector3(0,.2f,-1.5f);motor.ResetPosition(start);
        var region=new GameObject("Streaming structure test");var geometry=new GameObject("RegionGeometry");geometry.transform.SetParent(region.transform);
        var seedTerrain=new GameObject("Terrain_0_0");seedTerrain.transform.SetParent(geometry.transform);seedTerrain.AddComponent<MeshFilter>().sharedMesh=ExplorationChunks.BuildTerrain(field,ExplorationChunks.Coordinate(start));seedTerrain.AddComponent<MeshRenderer>().sharedMaterial=Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface");
        var streaming=region.AddComponent<ExplorationChunks>();streaming.Initialize(settings,player.transform);
        for(int i=0;i<200&&streaming.LoadedCount<25;i++)yield return null;
        var instance=region.GetComponentInChildren<StructureInstance>();Require(instance!=null,"Structure not instantiated by streamer");
        var points=instance.GetComponentsInChildren<StructureContentPoint>().Where(p=>p.data.kind==StructureContentKind.Enemy).ToArray();
        for(int i=0;i<400&&points.Any(p=>p.Spawned==null);i++)yield return null;
        foreach(var point in points)
        {
            Require(point.Spawned!=null,"Authored guardian did not spawn");
            point.Spawned.GetComponent<Mismo.Gameplay.Combat.Health>().ApplyDamage(new Mismo.Gameplay.Combat.DamageInfo(100000,null,point.transform.position,Vector3.forward));
        }
        for(int i=0;i<5;i++)yield return null;
        Require(Vector3.Distance(instance.transform.position,origin)<.1f,"Structure elevation changed");
        var definition=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+site.structure.id.Substring("structure.".Length)+".asset");
        var reward=instance.GetComponentInChildren<StructureReward>();Require(reward!=null&&!reward.Consumed,"Reward missing");
        foreach(var room in definition.rooms.Where(r=>r.role!=StructureRoomRole.Branch))
        {
            Vector3 destination=origin+new Vector3(room.center.x,0,room.center.y);float began=Time.time;
            while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(destination.x,destination.z))>.55f)
            {
                var delta=destination-player.transform.position;delta.y=0;motor.Tick(delta.normalized,false,false,false,Mathf.Min(Time.deltaTime,.05f));
                Require(Time.time-began<40,"Character stuck reaching "+room.name+" at "+player.transform.position);
                Require(Mathf.Abs(player.transform.position.y-origin.y)<1,"Character left the cave floor");
                yield return null;
            }
        }
        for(int i=0;i<8;i++){motor.Tick(Vector3.zero,false,false,false,.02f);yield return null;}
        Require(reward.Consumed,"Final trigger did not collect");string id="world-v1:"+settings.seed+":"+site.cell.x+":"+site.cell.y+":secret";
        Require(inventory.IsWorldEnemyDefeated(id),"Final discovery not persisted");string saved=memory.json;
        Require(inventory.TryGrantVictory(100,null,null,id)&&memory.json==saved,"Reward can be granted twice");
        motor.ResetPosition(start+Vector3.right*300);
        for(int i=0;i<250&&instance!=null;i++)yield return null;
        Require(instance==null,"Structure owner chunk did not unload");motor.ResetPosition(start);
        for(int i=0;i<250&&instance==null;i++){yield return null;instance=region.GetComponentInChildren<StructureInstance>();}
        Require(instance!=null,"Structure did not reload");for(int i=0;i<5;i++)yield return null;
        Require(instance.GetComponentInChildren<StructureReward>(true).Consumed,"Final reward respawned after streaming");
        Debug.Log("STRUCTURE_WALK_OK "+site.structure.id+" at "+site.position);
        Object.Destroy(region);yield return null;yield return null;
    }
    static void Finish(bool pass,string message)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Directory.CreateDirectory(StructureWorkshopSetup.Output);File.WriteAllText(StructureWorkshopSetup.Output+"/play-mode.txt",(pass?"PASS: ":"FAIL: ")+message);Debug.Log("STRUCTURE_PLAY_"+(pass?"PASS ":"FAIL ")+message);EditorApplication.Exit(pass?0:1);
    }
}
