using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerArrivalVfxSetup
{
    public const string Output = "output/player-arrival-vfx";
    public const string ParticlePath = "Assets/Art/Prefabs/Player/VFX/PlayerArrivalParticles.prefab";
    public const string MaterialPath = "Assets/Art/Materials/Player/VFX/PlayerArrival.mat";
    public const string PlayerPath = "Assets/Art/Prefabs/Player/Player.prefab";
    const string Source = "Assets/Art/Prefabs/VFX/ParticlePack/Misc Effects/EllenRespawn.prefab";
    static PlayerArrivalVfxSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/PlayerArrivalVfx.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request); Directory.CreateDirectory(Output);
        try
        {
            if (command == "setup") Apply();
            else if (command == "check") Check();
            else if (command == "play") PlayerArrivalVfxPlayChecks.Run();
            else if (command == "build") Build();
        }
        catch (Exception e) { File.WriteAllText(Output + "/" + command + "-FAILED.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Mismo/Personaje/Configurar efecto de llegada")]
    public static void Apply()
    {
        Directory.CreateDirectory(Output);
        ProjectAssetOrganizer.EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\','/'));
        ProjectAssetOrganizer.EnsureFolder(Path.GetDirectoryName(ParticlePath).Replace('\\','/'));
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/Player/PlayerArrival.shader"));
            material.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/VFX/ParticlePack/Misc Effects/Stripes.png"));
            material.SetColor("_EdgeColor", new Color(0,1.386243f,5.340313f,1));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePath).GetComponentsInChildren<ParticleSystem>(true).Length != 3)
        {
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("PlayerArrivalParticles"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                foreach (var system in reference.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (system.transform.parent.GetComponentInParent<ParticleSystem>(true) != null) continue;
                    Object.Instantiate(system.gameObject, root.transform, false).name = system.name;
                }
                foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = system.main; main.loop = false; main.playOnAwake = false; main.useUnscaledTime = false;
                    main.stopAction = ParticleSystemStopAction.None;
                }
                PrefabUtility.SaveAsPrefabAsset(root, ParticlePath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var effect = player.GetComponent<PlayerArrivalVfx>() ?? player.AddComponent<PlayerArrivalVfx>();
            effect.Configure(material, AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePath));
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        Check();
        File.WriteAllText(Output + "/setup.txt", "PASS: Player prefab references the one-shot effect derived from EllenRespawn.");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    [MenuItem("Mismo/Personaje/Verificar efecto de llegada")]
    public static void Check()
    {
        Directory.CreateDirectory(Output);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        Require(player.GetComponent<PlayerArrivalVfx>() != null, "Player effect missing");
        var dependencies = AssetDatabase.GetDependencies(PlayerPath, true);
        Require(dependencies.Contains(ParticlePath) && dependencies.Contains(MaterialPath), "Effect missing from serialized player dependencies");
        Require(!dependencies.Contains(Source), "Demo model must not enter the player prefab");
        File.WriteAllLines(Output + "/dependencies.txt", AssetDatabase.GetDependencies(ParticlePath, true).Concat(AssetDatabase.GetDependencies(MaterialPath, true)).Distinct());
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePath);
        Require(prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 3, "Expected Ellen rings, embers and smoke");
        Require(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Demo loop must not remain");
        Require(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0, "Demo mesh must not remain");
        Require(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath).shader), "Reveal shader compilation error");
        var lines = new System.Collections.Generic.List<string>();
        foreach (var skin in new[] { "MageSkin", "WarriorSkin", "NinjaFrogSkin" }) PreviewSkin(skin, lines);
        File.WriteAllLines(Output + "/editor-checks.txt", lines);
        try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS"); }
        catch (Exception e) { File.WriteAllText(Output + "/organization.txt", e.Message); }
    }
    static void PreviewSkin(string name, System.Collections.Generic.List<string> lines)
    {
        string path = "Assets/Art/Prefabs/Player/Skins/" + name + ".prefab";
        var skin = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(skin != null, "Missing skin: " + path);
        var scene = EditorSceneManager.NewPreviewScene();
        var owner = new GameObject("Arrival preview"); SceneManager.MoveGameObjectToScene(owner, scene);
        try
        {
            Object.Instantiate(skin, owner.transform, false);
            var renderers = owner.GetComponentsInChildren<Renderer>();
            var originals = renderers.Select(r => r.sharedMaterials).ToArray();
            var effect = owner.AddComponent<PlayerArrivalVfx>();
            effect.Configure(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath), AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePath));
            Require(effect.Play(), name + " must play");
            var tick = typeof(PlayerArrivalVfx).GetMethod("Tick", BindingFlags.Instance|BindingFlags.NonPublic);
            tick.Invoke(effect, new object[] { 1.35f });
            Require(effect.IsPlaying && Mathf.Abs(effect.Progress-.45f)<.01f, name + " reveal progression");
            Render(scene, owner, name);
            tick.Invoke(effect, new object[] { 0f }); Require(Mathf.Abs(effect.Progress-.45f)<.01f, "Pause must freeze reveal");
            Require(effect.Play() && effect.PlayCount==2, "Repeated teleport must restart cleanly");
            tick.Invoke(effect, new object[] { 3.1f }); Require(!effect.IsPlaying, "Reveal must finish");
            for(int i=0;i<renderers.Length;i++) Require(renderers[i].sharedMaterials.SequenceEqual(originals[i]), name + " material restoration");
            Require(effect.Play(), "Replay"); effect.enabled=false;
            // Ordinary MonoBehaviour callbacks do not run in an editor preview scene.
            typeof(PlayerArrivalVfx).GetMethod("OnDisable", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect, null);
            for(int i=0;i<renderers.Length;i++) Require(renderers[i].sharedMaterials.SequenceEqual(originals[i]), name + " disable restoration");
            lines.Add("PASS " + name + ": reveal, restart, pause, completion and disable restore all original material references.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Render(Scene scene, GameObject owner, string name)
    {
        foreach(var system in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ParticleSystem>())) system.Simulate(1.35f, false, true);
        var cameraObject = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraObject,scene);
        var camera = cameraObject.AddComponent<Camera>(); camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.07f,.10f);
        camera.transform.position=new Vector3(3,2.1f,-4.5f);camera.transform.LookAt(Vector3.up*1.1f);camera.fieldOfView=32;
        var lightObject=new GameObject("Preview sun");SceneManager.MoveGameObjectToScene(lightObject,scene);
        var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,-30,0);
        var rt=new RenderTexture(720,720,24);var old=RenderTexture.active;var image=new Texture2D(720,720,TextureFormat.RGB24,false);
        try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
        finally {camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
    }
    static void Build()
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=".validation/PlayerArrivalBuild/Mismo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Output+"/build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\n"+string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content).Distinct()));
    }
}
