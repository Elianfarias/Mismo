using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Presentation;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class FeelProjectChecks
{
    const string Output = "output/feel-integration";
    const string Active = "Mismo.FeelChecks.Active";
    const string ScenePath = "Assets/Scenes/FeelValidation.unity";
    [Serializable] class SavedScenes { public SceneSetup[] scenes; }
    static CombatFeelPlayer player;
    static CombatFeedbackProfile profile;
    static Camera camera;
    static int stage;
    static double next;
    static Quaternion cameraRotation;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState;
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/FeelChecks.request"))
        {
            File.Delete("Temp/FeelChecks.request"); Run(); return;
        }
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || player == null || Time.frameCount < 4 || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .08;
        try { Tick(); }
        catch (Exception e) { Finish("FAIL\n" + e); }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); File.AppendAllText(Output + "/play-checks.txt", "PASS " + message + "\n"); }

    [MenuItem("Mismo/Feedback/Verificar integración Feel")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            CombatFeedbackChecks.RunBatch();
            var p = AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset");
            foreach (var cue in new[] { p.impact, p.heavy, p.postureBreak, p.block, p.parry })
                if (cue.feel == null || !cue.feel.FeedbacksList.OfType<MMF_CameraShake>().Any(s => s.Channel == CombatFeelPlayer.Channel))
                    throw new Exception("Secuencia Feel ausente o canal incorrecto");
            File.WriteAllText(Output + "/editor-checks.txt", "PASS CombatFeedbackChecks.RunBatch y 5 secuencias serializadas\n");
            var dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(p), true);
            File.WriteAllLines(Output + "/combat-dependencies.txt", dependencies);
        }
        catch (Exception e) { File.WriteAllText(Output + "/editor-checks.txt", "FAIL\n" + e); return; }
        try { ProjectOrganizationChecks.Run(); File.WriteAllText(Output + "/organization.txt", "PASS"); }
        catch (Exception e) { File.WriteAllText(Output + "/organization.txt", "FAIL\n" + e); }
        // Never replace an unsaved user scene to run a test.
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
            { File.WriteAllText(Output + "/play-checks.txt", "BLOCKED: escena abierta sin guardar; no se modificó."); return; }
        }
        SessionState.SetString(Active + ".Scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        if (File.Exists(ScenePath)) throw new Exception("La escena de prueba ya existe; no se sobrescribe.");
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(testScene, ScenePath);
        SessionState.SetBool(Active, true);
        File.WriteAllText(Output + "/play-checks.txt", "Running real Play Mode\n");
        EditorApplication.isPlaying = true;
    }
    static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            profile = AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset");
            var target = new GameObject("Feel test target");
            var cameraObject = new GameObject("Feel test camera"); cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<ThirdPersonCamera>().Configure(target.transform, null);
            player = new GameObject("Feel runtime check").AddComponent<CombatFeelPlayer>();
            stage = 0; next = EditorApplication.timeSinceStartup + .15;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Active, false);
            var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Active + ".Scenes", ""));
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
            AssetDatabase.DeleteAsset(ScenePath);
            SessionState.EraseString(Active + ".Scenes");
        }
    }
    static void Tick()
    {
        var cues = new[] { profile.impact, profile.heavy, profile.postureBreak, profile.block, profile.parry };
        if (stage < 5)
        {
            Check(player.Play(cues[stage].feel, Vector3.zero), "Reproducción real MMF_Player " + stage);
            Check(player.ShakeAmplitude > 0, "Evento CameraShake recibido " + stage);
            stage++; return;
        }
        switch (stage++)
        {
            case 5:
                Check(player.CachedSequenceCount == 5, "Cinco secuencias reutilizables");
                Check(!player.Play(profile.impact.feel, Vector3.one * 100), "Combate lejano no sacude cámara");
                Check(player.Play(profile.parry.feel, Vector3.zero), "Reutilización de parry");
                Check(player.CachedSequenceCount == 5, "No se crean instancias por golpe");
                // Sample the render callbacks explicitly, independently of SRP render timing.
                typeof(CombatFeelPlayer).GetField("shakeAge", Private).SetValue(player, .012f);
                cameraRotation = camera.transform.rotation;
                typeof(CombatFeelPlayer).GetMethod("BeforeRender", Private).Invoke(player, new object[] { default(ScriptableRenderContext), camera });
                Check(Quaternion.Angle(cameraRotation, camera.transform.rotation) > .01f, "Rotación visible durante render");
                typeof(CombatFeelPlayer).GetMethod("AfterRender", Private).Invoke(player, new object[] { default(ScriptableRenderContext), camera });
                Check(Quaternion.Angle(cameraRotation, camera.transform.rotation) < .001f, "Órbita y apuntado restaurados tras render");
                GameplayPause.Pause();
                Check(!player.Play(profile.parry.feel, Vector3.zero), "Pausa bloquea nuevo feedback");
                return;
            case 6:
                Check(player.ShakeAmplitude == 0, "Pausa cancela sacudida");
                GameplayPause.Resume(); return;
            case 7:
                Time.timeScale = 0;
                Check(player.Play(profile.heavy.feel, Vector3.zero), "Feedback inicia durante hit stop");
                return;
            case 8:
                Check((float)typeof(CombatFeelPlayer).GetField("shakeAge", Private).GetValue(player) > .01f, "Feedback avanza con tiempo no escalado");
                Time.timeScale = 1; next = EditorApplication.timeSinceStartup + .4; return;
            case 9:
                cameraRotation = camera.transform.rotation;
                typeof(CombatFeelPlayer).GetMethod("BeforeRender", Private).Invoke(player, new object[] { default(ScriptableRenderContext), camera });
                Check(Quaternion.Angle(cameraRotation, camera.transform.rotation) < .001f, "Sacudida termina sin deriva");
                var images = player.GetComponentsInChildren<UnityEngine.UI.Image>();
                Check(images.Length == 1 && images[0].color.a == 0 && !images[0].raycastTarget, "Flash termina y no captura clicks");
                Object.Destroy(player.gameObject);
                Finish("PASS: secuencias reales, distancia, reutilización, cámara, pausa e hit stop."); return;
        }
    }
    static void Finish(string result)
    {
        File.AppendAllText(Output + "/play-checks.txt", result + "\n");
        GameplayPause.Resume(); Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
}
