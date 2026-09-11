using System;
using System.Collections;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InitialRegionChecks
    {
        private const string Pending = "Mismo.RegionChecks";
        private static IEnumerator routine;
        private static double deadline;
        private static int frame = -1;
        public static void RunBatch()
        {
            SessionState.SetBool("Mismo.CheckVoxel",false);
            EditorSceneManager.OpenScene(InitialRegionBuilder.ScenePath);
            SessionState.SetBool(Pending,true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunVoxelBatch()
        {
            SessionState.SetBool("Mismo.CheckVoxel",true);
            EditorSceneManager.OpenScene(VoxelRegionGenerator.DefaultScene);
            SessionState.SetBool(Pending,true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunPresentationBatch()
        {
            SessionState.SetBool("Mismo.PresentationChecks",true);
            RunVoxelBatch();
        }
        public static void BuildVoxelPlayer()
        {
            string directory=System.IO.Path.GetFullPath("Builds/VoxelRegion");
            System.IO.Directory.CreateDirectory(directory);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{VoxelRegionGenerator.DefaultScene},
                locationPathName=System.IO.Path.Combine(directory,"Mismo.exe"),
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            Require(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"Windows build: "+report.summary.result);
            Debug.Log("VOXEL_WINDOWS_BUILD_OK bytes="+report.summary.totalSize);
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(Pending,false)) return;
            deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update+=Step;
        }
        private static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"Timeout"); return; }
            if(!Application.isPlaying || Time.frameCount<5 || frame==Time.frameCount) return;
            frame=Time.frameCount;
            try { if(routine==null) routine=Checks(); if(!routine.MoveNext()) Finish(true,"navigation, reward, victory, respawn"); }
            catch(Exception e) { Finish(false,e.ToString()); }
        }
        private static void CaptureWeapon(Transform player, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var go=new GameObject("Weapon validation camera");
            var camera=go.AddComponent<UnityEngine.Camera>();
            camera.transform.position=player.position+new Vector3(3,2.1f,3);
            camera.transform.LookAt(player.position+Vector3.up);
            camera.orthographic=true; camera.orthographicSize=1.65f;
            var rt=new RenderTexture(720,720,24);
            var old=RenderTexture.active;
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var pixels=new Texture2D(720,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,720,720),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            RenderTexture.active=old; camera.targetTexture=null;
            Object.DestroyImmediate(pixels); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }

        private static IEnumerator Checks()
        {
            var player=Object.FindAnyObjectByType<PlayerController>();
            Require(player!=null,"Player exists");
            player.enabled=false;
            Vector3 spawn=player.transform.position;
            bool voxel=SessionState.GetBool("Mismo.CheckVoxel",false);
            if(SessionState.GetBool("Mismo.PresentationChecks",false))
            {
                SessionState.SetBool("Mismo.PresentationChecks",false);
                Screen.SetResolution(1600,900,false);
                for(int i=0;i<15;i++)yield return null;
                var hud=player.GetComponent<Mismo.Gameplay.Player.Presentation.PlayerHUD>();
                var sword=player.GetComponent<Mismo.Gameplay.Player.Presentation.SwordAnimationFeedback>();
                Require(hud!=null && hud.Minimap!=null && hud.Minimap.IsCreated(),"HUD minimap allocated");
                Require(sword!=null && sword.SwordVisual!=null,"Sword resolved");
                Require(Vector3.Distance(sword.SwordVisual.lossyScale,Vector3.one)<.01f,"Sword world scale is one, independent of imported hand");
                foreach(var plate in Object.FindObjectsByType<EnemyNameplate>())
                {
                    var status=plate.transform.Find("Status");
                    Require(status!=null && !status.gameObject.activeInHierarchy,"Legacy enemy status hidden: "+plate.name);
                }
                foreach(var renderer in sword.SwordVisual.GetComponentsInChildren<MeshRenderer>())
                    Require(renderer.bounds.min.y>player.transform.position.y+.1f && renderer.bounds.size.magnitude<1.5f,"Sword idle bounds above feet and normal size");
                player.GetComponent<Mismo.Gameplay.Player.Movement.PlayerMotor>().ResetPosition(new Vector3(-30,6,-34));
                var nearGoblin=Object.FindObjectsByType<GoblinController>();
                foreach(var enemy in nearGoblin)enemy.enabled=false;
                player.GetComponent<Health>().ApplyDamage(new DamageInfo(25,null,Vector3.zero,Vector3.forward));
                player.GetComponent<Mismo.Gameplay.Player.Movement.Stamina>().TrySpend(35);
                for(int i=0;i<15;i++)yield return null;
                CaptureWeapon(player.transform, "sword-rest.png");
                for(int i=0;i<5;i++)yield return null;
                var combo=player.GetComponentInChildren<BasicSwordCombo>();
                var hitbox=player.GetComponentInChildren<AttackHitbox>();
                Require(combo.RequestAttack(),"Combo request");
                float timeout=Time.time+1;
                while(!hitbox.IsWindowOpen && Time.time<timeout){combo.Tick(Time.deltaTime);yield return null;}
                Require(hitbox.IsWindowOpen,"Combo impact window");
                Time.timeScale=0;
                yield return null;
                Require(sword.ImpactVisible,"Sword trail matches active window");
                CaptureWeapon(player.transform, "sword-impact.png");
                foreach(var renderer in sword.SwordVisual.GetComponentsInChildren<MeshRenderer>())
                    Require(renderer.bounds.min.y>player.transform.position.y+.1f && renderer.bounds.size.magnitude<1.5f,"Sword impact above feet and normal size");
                for(int i=0;i<8;i++)yield return null;
                Time.timeScale=1;combo.Cancel();yield return null;
                Require(!sword.ImpactVisible,"Cancel removes trail");
                player.GetComponent<Health>().Revive();
                player.GetComponent<Mismo.Gameplay.Player.Movement.PlayerMotor>().ResetPosition(spawn);
                Debug.Log("PRESENTATION_PASS HUD, minimap, sword scale, active trail and cancellation");
            }
            foreach(var root in player.gameObject.scene.GetRootGameObjects())
                foreach(var item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)==0,"Missing script "+item.name);
            var goblins=Object.FindObjectsByType<GoblinController>();
            Require(goblins.Length==5,"Five goblins including elite");
            foreach(var goblin in goblins)
            {
                Require(goblin.GetComponent<NavMeshAgent>().isOnNavMesh,"Goblin on mesh");
                var route=new NavMeshPath();
                Require(NavMesh.CalculatePath(player.transform.position,goblin.transform.position,NavMesh.AllAreas,route) && route.status==NavMeshPathStatus.PathComplete,"Encounter reachable");
                goblin.enabled=false;
                foreach(var collider in goblin.GetComponentsInChildren<Collider>())collider.enabled=false;
            }
            var boss=Object.FindAnyObjectByType<BossController>();
            Require(boss.GetComponent<NavMeshAgent>().isOnNavMesh,"Boss on mesh");
            boss.enabled=false;
            foreach(var collider in boss.GetComponentsInChildren<Collider>())collider.enabled=false;
            var routeBoss=new NavMeshPath();
            Require(NavMesh.CalculatePath(player.transform.position,boss.transform.position,NavMesh.AllAreas,routeBoss) && routeBoss.status==NavMeshPathStatus.PathComplete,"Boss reachable");
            if(voxel)
            {
                var town=GameObject.Find("Imported Town");
                Require(town!=null && town.transform.childCount==30,"Town has four furnished lots and two exterior props");
                int houses=0;
                foreach(Transform slot in town.transform)
                {
                    if(slot.name=="2-House")houses++;
                    foreach(var renderer in slot.GetComponentsInChildren<MeshRenderer>())
                        foreach(var material in renderer.sharedMaterials)
                            Require(material!=null && material.mainTexture!=null,"Town texture connected");
                    var collider=slot.GetComponent<BoxCollider>();
                    if(collider!=null)Require(!collider.bounds.Intersects(new Bounds(new Vector3(-50,7,-60),new Vector3(5,6,23))),"Village street clear: "+slot.name);
                }
                Require(houses==4,"Four imported houses");
                foreach(var door in new[]{new Vector3(-57,4,-75),new Vector3(-45,4,-78),new Vector3(-58,4,-61),new Vector3(-46,4,-61)})
                {
                    var route=new NavMeshPath();
                    Require(NavMesh.SamplePosition(door,out var target,2,NavMesh.AllAreas) && NavMesh.CalculatePath(spawn,target.position,NavMesh.AllAreas,route) && route.status==NavMeshPathStatus.PathComplete,"House approach reachable");
                }
                Debug.Log("TOWN_RUNTIME_OK four houses, textures, clear street and reachable approaches");
            }
            Debug.Log("REGION PASS navigation and references");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var previewObject = new GameObject("Region preview camera");
                var preview = previewObject.AddComponent<UnityEngine.Camera>();
                preview.transform.position = voxel?new Vector3(170,210,-210):new Vector3(105,145,-25);
                preview.transform.LookAt(voxel?new Vector3(0,5,0):new Vector3(0,0,80));
                preview.orthographic=true; preview.orthographicSize=voxel?160:110;
                preview.farClipPlane=500;
                var texture=new RenderTexture(1200,1200,24);
                preview.targetTexture=texture; preview.Render();
                var previous=RenderTexture.active; RenderTexture.active=texture;
                var pixels=new Texture2D(1200,1200,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1200,1200),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes(voxel?"voxel-overview.png":"region-overview.png",pixels.EncodeToPNG());
                if(voxel)
                {
                    preview.orthographic=false; preview.fieldOfView=65;
                    preview.transform.position=new Vector3(-50,9,-89);preview.transform.LookAt(new Vector3(-38,7,-36));
                    preview.Render();pixels.ReadPixels(new Rect(0,0,1200,1200),0,0);pixels.Apply();
                    System.IO.File.WriteAllBytes("voxel-village.png",pixels.EncodeToPNG());
                }
                RenderTexture.active=previous; preview.targetTexture=null;
                Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(previewObject);
            }
            if(voxel)
            {
                var motor=player.GetComponent<Mismo.Gameplay.Player.Movement.PlayerMotor>();
                foreach(int index in new[]{1,2,3,4,5})
                {
                    var navPath=new NavMeshPath();
                    Require(NavMesh.SamplePosition(VoxelRegionHeightfield.Sites[index],out var goal,3,NavMesh.AllAreas),"Destination sampled");
                    Require(NavMesh.CalculatePath(player.transform.position,goal.position,NavMesh.AllAreas,navPath),"Walking route");
                    foreach(var corner in navPath.corners)
                    {
                        int ticks=0;
                        while(Vector3.ProjectOnPlane(corner-player.transform.position,Vector3.up).magnitude>.65f && ticks++<4000)
                        {
                            motor.Tick(Vector3.ProjectOnPlane(corner-player.transform.position,Vector3.up).normalized,false,false,false,1f/60f);
                            if(ticks%100==0)yield return null;
                        }
                        Require(ticks<4000,"Player walks without jumping to "+corner+" from "+player.transform.position);
                    }
                }
                Debug.Log("VOXEL PASS physical walking through encounters and secret approach");
                Require(Physics.Raycast(new Vector3(40,13,91),Vector3.forward,out var gateHit,5) && gateHit.collider.name=="Boss Passage Gate","Gate physically blocks shrine");
            }
            var pickup=Object.FindAnyObjectByType<SecretRewardPickup>();
            var health=player.GetComponent<Health>();
            var cc=player.GetComponent<CharacterController>();
            cc.enabled=false; player.transform.position=pickup.transform.position; cc.enabled=true;
            for(int i=0;i<10;i++) yield return null;
            Require(!pickup.Consumed,"Full health preserves reward");
            health.ApplyDamage(new DamageInfo(60,null,Vector3.zero,Vector3.forward));
            float collectDeadline=Time.time+1f;
            while(!pickup.Consumed && Time.time<collectDeadline) yield return null;
            Require(pickup.Consumed,"Trigger consumes reward");
            Require(Mathf.Approximately(health.Current,health.Maximum-60+health.Maximum*.4f),"Exactly one heal");
            Debug.Log("REGION PASS reward full health and single consumption");
            var encounter=Object.FindAnyObjectByType<BossEncounter>();
            var gate=GameObject.Find("Boss Passage Gate");
            Require(gate.activeSelf,"Gate initially closed");
            // Re-enable to retain the boss death subscription.
            boss.enabled=true;
            boss.Health.ApplyDamage(new DamageInfo(10000,player.gameObject,boss.transform.position,Vector3.forward));
            yield return null;
            Require(encounter.IsCompleted && !gate.activeSelf,"Victory opens gate");
            Debug.Log("REGION PASS boss victory");
            EntityId oldId=player.GetEntityId();
            health.ApplyDamage(new DamageInfo(10000,null,Vector3.zero,Vector3.forward));
            float until=Time.realtimeSinceStartup+6;
            PlayerController replacement=null;
            while(Time.realtimeSinceStartup<until)
            {
                yield return null;
                replacement=Object.FindAnyObjectByType<PlayerController>();
                if(replacement!=null && replacement.GetEntityId()!=oldId) break;
            }
            Require(replacement!=null && replacement.GetEntityId()!=oldId,"Scene reloaded");
            Require(replacement.enabled && replacement.GetComponent<Health>().Normalized==1,"Player restored");
            Require(Vector3.Distance(replacement.transform.position,spawn)<2,"Village spawn");
            Require(!Object.FindAnyObjectByType<BossEncounter>().IsCompleted,"Boss reset");
            Require(Object.FindAnyObjectByType<SecretRewardPickup>()!=null,"Reward reset");
            Debug.Log("REGION PASS respawn resets attempt");
        }
        private static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
        private static void Finish(bool success,string message)
        {
            SessionState.SetBool(Pending,false); EditorApplication.update-=Step;
            Debug.Log((success?"INITIAL_REGION_CHECKS_OK ":"INITIAL_REGION_CHECKS_FAILED ")+message);
            EditorApplication.Exit(success?0:1);
        }
    }
}

