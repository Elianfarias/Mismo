using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static class StructureContentChecks
{
    const string Key="StructureContentChecks";
    static IEnumerator routine;static int frame=-1;static double deadline;
    static readonly List<string> report=new List<string>();
    sealed class Memory:IProfileRepository
    {
        public string json;public bool fail;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=json;return json==null?ProfileReadResult.Missing:validate(json)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
        public void Write(string payload){if(fail)throw new IOException("Intentional content test failure");json=payload;}
    }
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);report.Add("PASS "+message);Debug.Log("STRUCTURE_CONTENT_CHECK "+message);}
    public static void RunBatch()
    {
        if(!Application.isBatchMode||!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Isolated validation required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
        Object.DestroyImmediate(player.GetComponent<RegionRespawn>());Object.DestroyImmediate(player.GetComponent<WorldCheckpoint>());player.GetComponent<PlayerController>().enabled=false;
        var camera=new GameObject("Test camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
        if(!Application.isPlaying||Time.frameCount<8||frame==Time.frameCount)return;frame=Time.frameCount;
        try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,"All authored content checks passed; no player save used.");}catch(Exception e){Finish(false,e.ToString());}
    }
    static IEnumerator Run()
    {
        var definition=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+"Temple_Grove.asset");
        var copy=Object.Instantiate(definition);var ids=copy.rooms.SelectMany(r=>r.contents).Select(e=>e.id).OrderBy(i=>i).ToArray();copy.layoutSeed++;copy.GenerateLayout();
        Check(ids.SequenceEqual(copy.rooms.SelectMany(r=>r.contents).Select(e=>e.id).OrderBy(i=>i)),"Regenerating layout preserves slot identities");
        var entry=copy.rooms.SelectMany(r=>r.contents).First();Check(entry.Duplicate().id!=entry.id,"Duplicating an object gives it a new identity");
        var room=copy.rooms.First(r=>r.contents.Count>0);room.contents.Add(entry.Copy());Check(copy.ValidateLayout()!=null,"Duplicate persistent IDs rejected");room.contents.RemoveAt(room.contents.Count-1);
        var oldOffset=entry.offset;entry.offset=Vector2.one*100;Check(copy.ValidateLayout()!=null,"Content outside its room rejected");entry.offset=oldOffset;
        copy.branches=0;bool refused=false;try{copy.GenerateLayout();}catch(InvalidOperationException){refused=true;}Check(refused&&copy.rooms.Any(r=>r.role==StructureRoomRole.Branch),"Reducing populated rooms cannot silently erase content");Object.Destroy(copy);

        var player=Object.FindAnyObjectByType<PlayerController>();var motor=player.GetComponent<PlayerMotor>();motor.AnimationMovementRequested=null;
        var memory=new Memory();var inventory=player.GetComponent<PlayerInventory>()??player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog"),memory);
        var gathering=player.GetComponent<GatheringPlayer>()??player.gameObject.AddComponent<GatheringPlayer>();
        var settings=Object.Instantiate(Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.generationVersion=2;settings.preserveAuthoredCenter=false;settings.loadRadius=2;settings.seed=7319;
        settings.content=Object.Instantiate(settings.content);settings.content.encounters=Array.Empty<WorldEncounterEntry>();settings.content.clearingChance=settings.content.roamingChance=0;
        var field=new ExplorationTerrain(settings);var site=field.WoodlandStructure.Value;var origin=site.position;origin.y=field.Height(origin.x,origin.z)+.05f;
        var start=origin+site.structure.prefab.entrance+new Vector3(0,.2f,-1.5f);motor.ResetPosition(start);
        var region=new GameObject("Structure content streaming test");var geometry=new GameObject("RegionGeometry");geometry.transform.SetParent(region.transform);
        var ground=new GameObject("Terrain_0_0");ground.transform.SetParent(geometry.transform);ground.AddComponent<MeshFilter>().sharedMesh=ExplorationChunks.BuildTerrain(field,ExplorationChunks.Coordinate(start));ground.AddComponent<MeshRenderer>().sharedMaterial=Mismo.Core.ProjectAssets.Load<Material>("TerrainSurface");
        var streaming=region.AddComponent<ExplorationChunks>();streaming.Initialize(settings,player.transform);
        StructureInstance instance=null;
        for(int i=0;i<600;i++){instance=region.GetComponentInChildren<StructureInstance>();if(instance!=null&&instance.GetComponentsInChildren<WorldEnemyIdentity>().Length==2&&instance.GetComponentInChildren<StructureChest>()!=null)break;yield return null;}
        Check(instance!=null,"World streamer loads populated temple");
        var enemies=instance.GetComponentsInChildren<WorldEnemyIdentity>();Check(enemies.Length==2,"Both authored enemies spawn once after navigation is ready");
        for(int i=0;i<5;i++)yield return null;
        Check(enemies.All(e=>e.GetComponent<NavMeshAgent>().isOnNavMesh),"Enemy agents stand on the interior NavMesh");
        Check(enemies.Select(e=>e.Level).OrderBy(l=>l).SequenceEqual(new[]{2,3}),"Per-slot enemy levels applied without changing shared prefabs");
        var chest=instance.GetComponentInChildren<StructureChest>();var node=instance.GetComponentInChildren<GatheringNode>();var reward=instance.GetComponentInChildren<StructureReward>(true);
        Check(chest!=null&&node!=null&&node.Available,"Chest and special ore are instantiated");
        Capture(enemies.Last().transform.position+new Vector3(3,2.7f,-4),enemies.Last().transform.position+Vector3.up*1.1f,"Temple_Grove-guardian");
        Capture(node.transform.position+new Vector3(1.8f,1.6f,-2.4f),node.transform.position+Vector3.up*.65f,"Temple_Grove-ore");
        motor.ResetPosition(instance.transform.TransformPoint(instance.finalRoom)+Vector3.up*.2f);for(int i=0;i<4;i++)yield return null;
        Check(!reward.Consumed&&!chest.Unlocked&&!chest.TryOpen(),"Final reward and chest remain locked while guardians survive");
        motor.ResetPosition(start);
        var family=player.GetComponent<EquipmentLoadout>().ActiveDefinition.MasteryId;
        foreach(var enemy in enemies)
        {
            var path=new NavMeshPath();var agent=enemy.GetComponent<NavMeshAgent>();
            Check(agent.CalculatePath(enemy.transform.position+Vector3.forward*.8f,path)&&path.status==NavMeshPathStatus.PathComplete,"Guardian can navigate inside its chamber");
            enemy.GetComponent<DamageReceiver>().ReceiveDamage(new DamageInfo(100000,player.gameObject,enemy.transform.position,Vector3.forward,area:true,parryable:false,weaponFamilyId:family));
        }
        for(int i=0;i<8;i++)yield return null;
        Check(enemies.All(e=>inventory.IsWorldEnemyDefeated(e.Id)),"Combat rewards persist authored kills through the existing enemy system");
        Check(chest.Unlocked,"Clearing guardians unlocks the chest");
        motor.ResetPosition(chest.transform.position+chest.transform.forward*1.8f+Vector3.up*.2f);Physics.SyncTransforms();
        int pending=inventory.PendingLoot.Count();string before=memory.json;memory.fail=true;
        Check(!chest.TryOpen()&&!chest.Opened&&inventory.PendingLoot.Count()==pending,"Failed save leaves chest closed without partial loot");memory.fail=false;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.F));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check(chest.Opened&&inventory.PendingLoot.Count()==pending+1,"F opens the chest and creates persistent collectible loot");
        for(int i=0;i<12;i++)yield return null;
        Check(Vector3.Dot(chest.transform.forward,Vector3.back)>.99f,"Example chest faces the room entrance");
        Capture(chest.transform.position+chest.transform.forward*3-chest.transform.right*1.5f+Vector3.up*1.8f,chest.transform.position+Vector3.up*.5f,"Temple_Grove-chest");
        Check(!chest.TryOpen()&&inventory.PendingLoot.Count()==pending+1,"Repeated chest interaction cannot duplicate rewards");
        string chestId=chest.GetComponent<StructureContentPoint>().PersistentId;
        Check(inventory.IsWorldEnemyDefeated(chestId),"Opened state and chest loot share one saved transaction");
        var loot=inventory.PendingLoot.Last();motor.ResetPosition(new Vector3(loot.x,origin.y+.2f,loot.z));
        Check(inventory.CollectPending(loot.id),"Chest materials can be picked up through normal inventory storage");

        var resource=Object.Instantiate(node.definition);resource.harvestSeconds=.12f;node.definition=resource;
        motor.ResetPosition(node.transform.position+Vector3.forward*1.8f+Vector3.up*.2f);Physics.SyncTransforms();yield return null;
        string mineral=resource.rewards.entries[0].material.id;int materialBefore=inventory.MaterialCount(mineral);string nodeId=node.persistentId;
        memory.fail=true;Check(!node.Complete(inventory)&&node.Available&&inventory.MaterialCount(mineral)==materialBefore,"Failed save leaves ore available without awarding materials");memory.fail=false;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.F));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        Check(gathering.IsHarvesting,"F starts mining through GatheringPlayer");
        for(int i=0;i<120&&node.Available;i++)yield return null;
        Check(!node.Available&&inventory.MaterialCount(mineral)>materialBefore&&inventory.IsWorldEnemyDefeated(nodeId),"Special ore grants material and persists permanent depletion");
        Check(!node.Complete(inventory),"Depleted ore cannot be harvested twice");
        motor.ResetPosition(instance.transform.TransformPoint(instance.finalRoom)+Vector3.up*.2f);for(int i=0;i<5;i++)yield return null;
        Check(reward.Consumed,"Final discovery can be collected after guardians are cleared");
        var saved=JsonUtility.FromJson<InventoryProfile>(memory.json);Check(saved.defeatedEnemies.Contains(chestId)&&saved.defeatedEnemies.Contains(nodeId)&&enemies.All(e=>saved.defeatedEnemies.Contains(e.Id)),"Serialized profile retains chest, ore and enemy identities");
        motor.ResetPosition(start+Vector3.right*320);for(int i=0;i<350&&instance!=null;i++)yield return null;Check(instance==null,"Populated structure unloads with its owner chunk");
        motor.ResetPosition(start);for(int i=0;i<500;i++){instance=region.GetComponentInChildren<StructureInstance>();if(instance!=null&&instance.GetComponentInChildren<StructureChest>()!=null)break;yield return null;}
        for(int i=0;i<8;i++)yield return null;
        Check(instance!=null&&instance.GetComponentsInChildren<WorldEnemyIdentity>().Length==0,"Defeated guardians stay absent after world streaming reload");
        Check(instance.GetComponentInChildren<StructureChest>().Opened&&!instance.GetComponentInChildren<GatheringNode>().Available,"Opened chest and exhausted ore survive streaming reload");
        Check(instance.GetComponentInChildren<StructureReward>(true).Consumed,"Final reward does not reset on reload");
        var respawn=Object.Instantiate(resource);respawn.regenerationSeconds=.2f;respawn.restoreClearance=0;
        var ordinary=GatheringDistribution.Place(respawn,"content-test-regenerating-ore",start+Vector3.forward*2,region.transform);
        motor.ResetPosition(start+Vector3.up*.1f);yield return null;yield return null;Physics.SyncTransforms();
        Check(ordinary.Complete(inventory)&&!ordinary.Available&&inventory.NodeReadyAt(ordinary.persistentId)>inventory.WorldPlaySeconds,"Optional regenerating ore saves a cooldown instead of a permanent removal");
        for(int i=0;i<150&&!ordinary.Available;i++)yield return null;
        Check(ordinary.Available&&!inventory.IsWorldEnemyDefeated(ordinary.persistentId),"Regenerating ore becomes available again after its configured cooldown");
        Object.Destroy(respawn);
        InputSystem.RemoveDevice(keyboard);Object.Destroy(resource);
    }
    static void Capture(Vector3 position,Vector3 target,string name)
    {var camera=Camera.main;camera.transform.position=position;camera.transform.LookAt(target);camera.nearClipPlane=.08f;camera.fieldOfView=65;StructureWorkshopChecks.Render(camera,name);}
    static void Finish(bool pass,string message)
    {SessionState.SetBool(Key,false);EditorApplication.update-=Tick;report.Add((pass?"PASS ":"FAIL ")+message);File.WriteAllLines(StructureWorkshopSetup.Output+"/content-checks.txt",report);Debug.Log("STRUCTURE_CONTENT_"+(pass?"PASS ":"FAIL ")+message);EditorApplication.Exit(pass?0:1);}
}
