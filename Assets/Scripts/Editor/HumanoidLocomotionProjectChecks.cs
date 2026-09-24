using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Editor;
using UnityEditor;
using UnityEngine;

public static class HumanoidLocomotionProjectChecks
{
    const string Request = "Temp/HumanoidLocomotionChecks.request";

    [InitializeOnLoadMethod]
    static void ResumeRequestedCheck()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        };
    }

    [MenuItem("Mismo/Character/Verificar locomoción Humanoid")]
    public static void Run()
    {
        Directory.CreateDirectory("output/humanoid-locomotion");
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Player necesita un Avatar Humanoid válido.");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(QuaterniusHumanoidLocomotion.ControllerPath);
            string[] names = { "Idle", "Walk", "Run", "Jump", "Fall", "Land" };
            foreach (string name in names)
            {
                var clips = controller.animationClips.Where(c => c.name == "Quaternius_" + name).ToArray();
                if (clips.Length == 0 || clips.Any(c => !c.humanMotion))
                    throw new InvalidOperationException("Locomoción incompatible: " + name);
            }
            File.WriteAllText("output/humanoid-locomotion/project-checks.txt", "PASS Player Avatar and all six controller locomotion clips are Humanoid.\n");
        }
        catch (Exception e)
        {
            File.WriteAllText("output/humanoid-locomotion/project-checks.txt", "FAIL " + e);
            Debug.LogException(e);
        }
        try
        {
            ProjectOrganizationChecks.Run();
            File.WriteAllText("output/humanoid-locomotion/organization-checks.txt", "PASS ProjectOrganizationChecks.Run");
        }
        catch (Exception e)
        {
            File.WriteAllText("output/humanoid-locomotion/organization-checks.txt", e.ToString());
            Debug.LogWarning("La verificación de organización encontró incidencias. Ver output/humanoid-locomotion/organization-checks.txt");
        }
    }
}
