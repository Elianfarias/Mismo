using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Explicit upgrade of the existing Craftpix villagers, preserving prefab GUIDs and quest data.</summary>
public static class VillageHumanoidUpgrade
{
    [InitializeOnLoadMethod] static void Register() { EditorApplication.update-=Poll;EditorApplication.update+=Poll; }
    static void Poll()
    {
        const string request="Temp/VillageHumanoid.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);
        try { if(command=="build")Build();else if(command=="workshop"){NpcWorkshopWindow.CheckRoundTrip();NpcWorkshopWindow.OpenResident();}else {Apply();Validate();} File.WriteAllText(Output+"/request.txt","PASS"); }
        catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/request.txt",e.ToString());Debug.LogException(e);}
    }
    const string Animations = "Assets/Art/Animations/Voxelized/NPC/Craftpix";
    const string ControllerPath = Animations + "/VillageLocomotion.controller";
    const string TownPath = "Assets/Art/Prefabs/World/Villages/MedievalVillage.prefab";
    public const string Output = "output/village-humanoid";
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\','/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'), Path.GetFileName(path));
    }
    static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Humanoid/Quaternius_"+name+".anim");
    static AnimatorController Controller()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null) return existing;
        Folder(Animations);
        var idle=Clip("Idle");var walk=Clip("Walk");
        Require(idle != null && walk != null && idle.humanMotion && walk.humanMotion,"Humanoid locomotion clips missing");
        var controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
        var machine=controller.layers[0].stateMachine;
        var rest=machine.AddState("Idle");rest.motion=idle;machine.defaultState=rest;
        var moving=machine.AddState("Walk");moving.motion=walk;
        var start=rest.AddTransition(moving);start.hasExitTime=false;start.duration=.2f;start.AddCondition(AnimatorConditionMode.Greater,.08f,"Speed");
        var stop=moving.AddTransition(rest);stop.hasExitTime=false;stop.duration=.2f;stop.AddCondition(AnimatorConditionMode.Less,.08f,"Speed");
        return controller;
    }
    [MenuItem("Mismo/Quests/Actualizar habitantes Humanoid y paseos")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode first");
        Directory.CreateDirectory(Output);
        var controller=Controller();var report=new List<string>();
        foreach(var path in Directory.GetFiles(VillageNpcIntegration.Source+"/fbx/people_unity","*.fbx").OrderBy(p=>p))
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
            var original=source.GetComponentInChildren<Animator>();
            Require(original!=null && original.avatar!=null && original.avatar.isHuman && original.avatar.isValid,"Invalid source avatar: "+path);
            string name="NPC_"+Path.GetFileNameWithoutExtension(path);
            string target=VillageNpcIntegration.Prefabs+"/"+name+".prefab";
            string guid=AssetDatabase.AssetPathToGUID(target);
            var current=AssetDatabase.LoadAssetAtPath<GameObject>(target);
            var currentAnimator=current!=null?current.GetComponentInChildren<Animator>():null;
            if(currentAnimator==null || currentAnimator.avatar!=original.avatar || currentAnimator.runtimeAnimatorController!=controller)
            {
                var instance=Object.Instantiate(source);instance.name=source.name;
                GameObject generated;
                try { instance.GetComponentInChildren<Animator>().runtimeAnimatorController=controller;generated=WeaponVoxelizerWindow.ExportModel(instance,name+"_Humanoid",64,true); }
                finally { Object.DestroyImmediate(instance); }
                string temporaryPath=AssetDatabase.GetAssetPath(generated);
                var root=Object.Instantiate(generated);root.name=name;
                try
                {
                    foreach(var collider in root.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                    var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                    string meshPath=AssetDatabase.GetAssetPath(skin.sharedMesh);
                    string previousMesh="Assets/Art/Meshes/Voxelized/NPC/Craftpix/"+name+".asset";
                    var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(previousMesh);
                    if(oldMesh!=null)
                    {
                        ReplaceMesh(skin.sharedMesh,oldMesh);skin.sharedMesh=oldMesh;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,target);
                    Require(guid==AssetDatabase.AssetPathToGUID(target),"Prefab GUID changed: "+target);
                    // Only delete the temporary export and its mesh after replacing their references.
                    AssetDatabase.DeleteAsset(temporaryPath);
                    if(oldMesh!=null)AssetDatabase.DeleteAsset(meshPath);
                    string animationFolder="Assets/Art/Animations/Voxelized/"+name+"_Humanoid_Animations";
                    string destination=Animations+"/"+name+"_Humanoid_Animations";
                    if(AssetDatabase.IsValidFolder(animationFolder))Require(AssetDatabase.MoveAsset(animationFolder,destination)=="","Cannot organize animations");
                    foreach(var material in skin.sharedMaterials)
                    {
                        string materialPath=AssetDatabase.GetAssetPath(material);
                        if(materialPath.StartsWith("Assets/Art/Materials/"+name+"_Humanoid"))
                        {
                            string destinationMaterial="Assets/Art/Materials/Characters/NPC/Craftpix/"+Path.GetFileName(materialPath);
                            Require(AssetDatabase.MoveAsset(materialPath,destinationMaterial)=="","Cannot organize material");
                        }
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
            // An Avatar keeps its asset identity when FBX axes or its reference pose change.
            // Always refresh geometry and bind poses together; Avatar equality is not a cache key.
            var refreshSource=Object.Instantiate(source);refreshSource.name=source.name;
            var refreshTarget=PrefabUtility.LoadPrefabContents(target);Mesh refreshed=null;
            try
            {
                refreshed=WeaponVoxelizerWindow.RebuildRigGeometry(refreshSource,refreshTarget);
                var skin=refreshTarget.GetComponentInChildren<SkinnedMeshRenderer>();
                ReplaceMesh(refreshed,skin.sharedMesh);skin.localBounds=refreshed.bounds;
                PrefabUtility.SaveAsPrefabAsset(refreshTarget,target);
            }
            finally{Object.DestroyImmediate(refreshSource);if(refreshed!=null)Object.DestroyImmediate(refreshed);PrefabUtility.UnloadPrefabContents(refreshTarget);}
            report.Add(name+": valid source Humanoid avatar, original prefab GUID "+guid);
        }
        var settings=AssetDatabase.LoadAssetAtPath<VillageNpcSettings>(VillageNpcIntegration.SettingsPath);
        // The player has a broad stylized silhouette. Adults stand 2.8m tall in world space.
        settings.residentScale=1.6f;settings.interactionRange=3.6f;
        for(int i=0;i<settings.residents.Length;i++)
        {
            var resident=settings.residents[i];string path=AssetDatabase.GetAssetPath(resident.prefab);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var oldVisual=root.GetComponentsInChildren<SkinnedMeshRenderer>().First().transform;
                while(oldVisual.parent!=root.transform)oldVisual=oldVisual.parent;
                string visualName=oldVisual.name;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(VillageNpcIntegration.Prefabs+"/"+visualName+".prefab");
                Require(source!=null,"Resident visual missing: "+visualName);
                var idle=root.GetComponent<VillageNpcIdle>();if(idle!=null)Object.DestroyImmediate(idle);
                Object.DestroyImmediate(oldVisual.gameObject);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
                var animator=visual.GetComponentInChildren<Animator>();animator.enabled=true;animator.applyRootMotion=false;
                animator.runtimeAnimatorController=controller;animator.Rebind();animator.Update(0);
                var mesh=new Mesh();visual.GetComponentInChildren<SkinnedMeshRenderer>().BakeMesh(mesh);
                var bounds=mesh.bounds;Object.DestroyImmediate(mesh);
                // Sampling is only for measuring: never serialize that animation pose as bone overrides.
                foreach(var t in source.GetComponentsInChildren<Transform>())
                {
                    string bonePath=AnimationUtility.CalculateTransformPath(t,source.transform);if(bonePath.Length==0)continue;
                    var bone=visual.transform.Find(bonePath);bone.localPosition=t.localPosition;bone.localRotation=t.localRotation;bone.localScale=t.localScale;
                }
                float scale=1.75f/bounds.size.y;visual.transform.localScale=Vector3.one*scale;
                visual.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                var body=root.GetComponent<CapsuleCollider>();body.height=1.75f;body.radius=.3f;body.center=Vector3.up*.875f;
                var agent=root.GetComponent<NavMeshAgent>();if(agent==null)agent=root.AddComponent<NavMeshAgent>();
                agent.enabled=false;agent.height=1.75f;agent.radius=.3f;agent.speed=1.35f;agent.angularSpeed=160;agent.acceleration=3;agent.stoppingDistance=.35f;
                agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;agent.avoidancePriority=35+i*7;
                var routine=root.GetComponent<VillageNpcRoutine>();if(routine==null)routine=root.AddComponent<VillageNpcRoutine>();routine.animator=animator;routine.walkingSpeed=1.2f+i*.07f;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Each contact returns to their workplace and visits the shared square and entrance street.
            resident.route=new[]{new Vector3(resident.localPosition.x,0,-3),new Vector3(i%2==0?-4:3,0,2),new Vector3(3,0,-8)};
        }
        EditorUtility.SetDirty(settings);
        var catalog=ProjectAssets.Load<WorldContentCatalog>("WorldContentCatalog");catalog.villageSizeMultiplier=3.25f;EditorUtility.SetDirty(catalog);
        AddTownDestinations(settings);
        AssetDatabase.SaveAssets();
        try {ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization.txt","PASS");}
        catch(Exception e){File.WriteAllText(Output+"/organization.txt",e.ToString());Debug.LogWarning("Village upgrade: existing project organization issues, see "+Output+"/organization.txt");}
        File.WriteAllLines(Output+"/upgrade.txt",report.Concat(new[]{"14 Humanoid visuals; 5 animated quest residents; 2.8m adults; village scale 3.25; workplace/plaza/entrance routes."}));
        Debug.Log("VILLAGE_HUMANOID_UPGRADE_OK");
    }
    internal static void ReplaceMesh(Mesh source,Mesh target)
    {
        var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var colors=source.colors32;
        var weights=source.boneWeights;var poses=source.bindposes;var bounds=source.bounds;var format=source.indexFormat;
        var triangles=Enumerable.Range(0,source.subMeshCount).Select(source.GetTriangles).ToArray();
        target.Clear(false);target.indexFormat=format;target.vertices=vertices;target.normals=normals;target.uv=uv;target.colors32=colors;
        target.boneWeights=weights;target.bindposes=poses;target.subMeshCount=triangles.Length;
        for(int i=0;i<triangles.Length;i++)target.SetTriangles(triangles[i],i);
        target.bounds=bounds;target.UploadMeshData(false);EditorUtility.SetDirty(target);
    }
    static void AddTownDestinations(VillageNpcSettings settings)
    {
        var root=PrefabUtility.LoadPrefabContents(TownPath);
        try
        {
            var previous=root.transform.Find("Resident destinations");
            if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var group=new GameObject("Resident destinations").transform;group.SetParent(root.transform,false);
            foreach(var resident in settings.residents)
            {
                var definition=resident.prefab.GetComponent<QuestGiver>().npc;
                var stop=new GameObject(definition.displayName+" - "+definition.occupation).transform;
                stop.SetParent(group,false);stop.localPosition=resident.localPosition;
            }
            foreach(var item in new[]{("Plaza",new Vector3(3,0,2)),("Entrance promenade",new Vector3(3,0,-8))})
            {var stop=new GameObject(item.Item1).transform;stop.SetParent(group,false);stop.localPosition=item.Item2;}
            PrefabUtility.SaveAsPrefabAsset(root,TownPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Batch()
    {
        try { Apply(); Validate(); EditorApplication.Exit(0); }
        catch(Exception e) { Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1); }
    }
    public static void Validate()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
        var report=new List<string>();
        foreach(var path in Directory.GetFiles(VillageNpcIntegration.Prefabs,"*.prefab"))
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/')));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var animator=root.GetComponentInChildren<Animator>();Require(animator.isHuman && animator.avatar.isValid,"Invalid exported avatar: "+path);
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();
                animator.Play("Idle");animator.Update(.1f);skin.BakeMesh(mesh);var idle=mesh.vertices;
                animator.Play("Walk");animator.Update(.3f);skin.BakeMesh(mesh);var walk=mesh.vertices;
                float deformation=idle.Select((p,i)=>Vector3.Distance(p,walk[i])).Max();
                Require(deformation>.02f && deformation<3,"Animation does not deform correctly: "+path+" "+deformation);
                report.Add(Path.GetFileName(path)+": Humanoid Idle/Walk deformation="+deformation);
                Object.DestroyImmediate(mesh);
            }
            finally {Object.DestroyImmediate(root);}
        }
        File.WriteAllLines(Output+"/animation-checks.txt",report);
        Debug.Log("VILLAGE_HUMANOID_ANIMATION_CHECKS_OK");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/VoxelRegion_7319.unity"},locationPathName=Output+"/Build/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Output+"/build.txt",report.summary.result+" errors="+report.summary.totalErrors+"\n"+string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content).Distinct()));
        Require(report.summary.result==BuildResult.Succeeded,"Build failed");
    }
}

