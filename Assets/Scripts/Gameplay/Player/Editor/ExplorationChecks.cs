using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public sealed class ProbeSpecial : SpecialAbilityDefinition
    {
        public int begins,ends,ticks;
        public override float Duration=>1;
        public override float Cooldown=>2;
        public override void Begin(SpecialAbilityExecution e){begins++;e.State=new object();}
        public override void Tick(SpecialAbilityExecution e,float dt){if(e.State==null)throw new Exception("Missing effect state");ticks++;}
        public override void End(SpecialAbilityExecution e,bool cancelled){ends++;e.State=null;}
    }
    public static class ExplorationChecks
    {
        static int count;
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("EXPLORATION_CHECK "+message);}
        public static void RunBatch()
        {
            try
            {
                var settings=Resources.Load<ExplorationWorldSettings>("ExplorationWorldSettings");Check(settings!=null,"Streaming configuration included in Resources");
                var field=new World.VoxelRegionHeightfield(settings.seed,settings.authoredSize,settings.relief,settings.stepHeight);
                Check(ExplorationChunks.Coordinate(new Vector3(-.01f,0,-32.01f))==new Vector2Int(-1,-2),"Negative chunk coordinates use floor");
                var mesh=ExplorationChunks.BuildTerrain(field,new Vector2Int(4,0));var repeat=ExplorationChunks.BuildTerrain(field,new Vector2Int(4,0));
                Check(mesh.vertices.SequenceEqual(repeat.vertices)&&mesh.triangles.SequenceEqual(repeat.triangles),"Chunk regeneration deterministic");
                var editorSettings=AssetDatabase.LoadAssetAtPath<VoxelRegionSettings>("Assets/Data/World/VoxelRegionSettings.asset");
                var oldField=new VoxelRegionHeightfield(editorSettings);
                var old=(Mesh)typeof(VoxelRegionGenerator).GetMethod("Terrain",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{oldField,128,0,32,32,1});
                Check(mesh.vertices.SequenceEqual(old.vertices)&&mesh.triangles.SequenceEqual(old.triangles),"Runtime geometry matches existing editor terrain including seams");
                Object.DestroyImmediate(mesh);Object.DestroyImmediate(repeat);Object.DestroyImmediate(old);
                var owner=new GameObject("Special checks");var slot=owner.AddComponent<BeltDash>();
                var dash=AssetDatabase.LoadAssetAtPath<BasicDashBehaviour>("Assets/Data/Player/BasicBeltDash.asset");slot.Configure(dash);
                Check(slot.TryStart(Vector3.forward,true)&&slot.ControlsMovement,"Initial dash preserved");
                Vector3 distance=Vector3.zero;for(int i=0;i<100;i++)distance+=slot.Step(.01f);
                Check(!slot.IsActive&&distance.z>1,"Dash finishes and displaces");slot.TickCooldown(10);
                var effect=ScriptableObject.CreateInstance<ProbeSpecial>();slot.EquipSpecial(effect);
                Check(slot.TryStart(Vector3.forward,true)&&!slot.ControlsMovement,"Non-movement effect uses same special slot");
                Check(slot.Step(.25f)==Vector3.zero&&effect.ticks==1,"Effect ticks without replacing locomotion");slot.EquipSpecial(dash);
                Check(effect.ends==1&&!slot.IsActive&&slot.CooldownRemaining>0,"Replacing ability cleans effect and preserves cooldown");
                Object.DestroyImmediate(effect);Object.DestroyImmediate(owner);
                Debug.Log("EXPLORATION_CORE_PASS "+count);
                EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");
                EditorApplication.playModeStateChanged+=OnPlay;
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] static void Resume(){EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
        static double began;static int stage;
        static PlayerController player;static ExplorationChunks stream;
        static void OnPlay(PlayModeStateChange state)
        {
            if(!Environment.GetCommandLineArgs().Contains("Mismo.Gameplay.Player.Editor.ExplorationChecks.RunBatch"))return;
            if(state==PlayModeStateChange.EnteredPlayMode){began=EditorApplication.timeSinceStartup;stage=0;EditorApplication.update+=Tick;}
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup-began<4)return;
            try
            {
                if(stage==0)
                {
                    stream=Object.FindFirstObjectByType<ExplorationChunks>();player=Object.FindFirstObjectByType<PlayerController>();
                    Check(stream!=null&&stream.LoadedCount>0,"Scene bootstraps streaming during Play");player.enabled=false;
                    Check(Object.FindObjectsByType<ClimbableTree>(FindObjectsSortMode.None).Length>0,"Existing and generated trees support climbing");
                    player.GetComponent<PlayerMotor>().ResetPosition(new Vector3(160,55,0));stage++;began=EditorApplication.timeSinceStartup;return;
                }
                if(stage==1)
                {
                    Check(Physics.Raycast(new Vector3(160,100,0),Vector3.down,out var hit,150)&&hit.collider.GetComponentInParent<ExplorationChunks>()!=null,"Explored chunk has physical terrain");
                    Check(UnityEngine.AI.NavMesh.SamplePosition(hit.point,out var nav,4,UnityEngine.AI.NavMesh.AllAreas),"Explored terrain has navigable surface");
                    player.GetComponent<PlayerMotor>().ResetPosition(new Vector3(352,55,0));stage++;began=EditorApplication.timeSinceStartup;return;
                }
                Check(stream.LoadedCount<=81,"Distant chunks unload and active count stays bounded");
                var stamina=player.GetComponent<Stamina>();
                var climbing=player.GetComponent<TreeClimbing>();var motor=player.GetComponent<PlayerMotor>();
                var tree=new GameObject("Test trunk");tree.transform.position=new Vector3(350,70,0);var box=tree.AddComponent<BoxCollider>();box.size=new Vector3(1,20,1);tree.AddComponent<ClimbableTree>();
                motor.ResetPosition(new Vector3(350,65,-1));Physics.SyncTransforms();float startStamina=stamina.Current;
                Check(climbing.Step(motor,stamina,Vector3.forward,1,true,.02f),"Holding jump toward a trunk attaches");
                float oldY=player.transform.position.y;motor.Tick(Vector3.zero,false,false,false,.02f);
                Check(player.transform.position.y>oldY&&stamina.Current<startStamina,"Climbing ascends and consumes stamina");
                for(int i=0;i<200&&climbing.IsClimbing;i++)climbing.Step(motor,stamina,Vector3.forward,0,true,.02f);
                Check(!climbing.IsClimbing&&stamina.Current<.01f,"Exhaustion forces release even when stationary");
                Object.Destroy(tree);Debug.Log("EXPLORATION_PLAY_PASS "+count);EditorApplication.update-=Tick;EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
    }
}
