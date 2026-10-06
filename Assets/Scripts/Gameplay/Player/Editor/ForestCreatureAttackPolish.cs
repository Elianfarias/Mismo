using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Only updates the four boar/spider attacks; preserves models, variants and combat stats.</summary>
    public static class ForestCreatureAttackPolish
    {
        const string Animations = "Assets/Art/Animations/ForestCreatures/Combat/";
        const string Data = "Assets/Data/Enemies/ForestCreatures/";
        const string Output = "output/creature-attack-polish/";

        [MenuItem("Mismo/Enemigos/Mejorar anticipación de jabalí y araña")]
        public static void Apply()
        {
            foreach (string species in new[] { "Boar", "Spider" })
            {
                string path = Animations + species + "_Combat.fbx";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = importer.importLights = false;
                importer.optimizeGameObjects = false;
                // A skeleton-only FBX still needs the rig container present in the model FBX.
                importer.preserveHierarchy = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.motionNodeName = "Root";
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.loopTime = clip.loopPose = false;
                    clip.lockRootRotation = clip.lockRootPositionXZ = clip.lockRootHeightY = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var settings = AssetDatabase.LoadAssetAtPath<CreatureSettings>(Data + species + ".asset");
                Configure(settings, species);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            VerifyAndPreview();
        }

        public static void Configure(CreatureSettings settings, string species)
        {
            if (species == "Boar")
            {
                Bind(settings, "Attack", "Boar_TuskStrike", .55f, .16f, .08f, .60f, .88f).damageStartsAt = .40f;
                Bind(settings, "Charge", "Boar_Charge", .72f, .60f, .10f, .60f, .90f);
            }
            else if (species == "Spider")
            {
                Bind(settings, "Attack_Bite", "Spider_Bite", .48f, .14f, .07f, .60f, .88f).damageStartsAt = .45f;
                var jump = Bind(settings, "Jump", "Spider_Pounce", .60f, .48f, .08f, .50f, .92f);
                jump.damageStartsAt = .90f; // The feet land at 90% of the active segment.
            }
        }

        static GoblinAttack Bind(CreatureSettings settings, string label, string clipName,
            float windup, float active, float recovery, float start, float end)
        {
            string species = clipName.Split('_')[0];
            var clip = AssetDatabase.LoadAllAssetsAtPath(Animations + species + "_Combat.fbx")
                .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview") && c.name.Split('|').Last() == clipName);
            var attack = settings.attacks.Single(a => a.label == label);
            attack.windup = windup; attack.active = active; attack.recovery = recovery;
            attack.animation.clip = clip;
            attack.animation.compatibleClip = attack.animation.compatibleSource = null;
            attack.animation.activeStartsAt = start; attack.animation.recoveryStartsAt = end;
            attack.animation.blendSeconds = .025f;
            attack.preparationClip = attack.recoveryClip = null;
            attack.preparationDuration = 0; attack.preparationEndNormalized = 1;
            attack.loopActiveAnimation = false;
            return attack;
        }

        [MenuItem("Mismo/Enemigos/Verificar anticipación de jabalí y araña")]
        public static void VerifyAndPreview()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "animation-checks.txt", "");
            foreach (string species in new[] { "Boar", "Spider" })
            {
                var settings = AssetDatabase.LoadAssetAtPath<CreatureSettings>(Data + species + ".asset");
                foreach (var attack in settings.attacks.Where(a => a.enabled))
                {
                    Check(attack.windup >= .45f && attack.recovery <= .101f, species + " " + attack.label + " readable windup / brief recovery");
                    Check(attack.animation.PlaybackClip != null, "Serialized clip reference resolves");
                    Check(Mathf.Abs(attack.animation.Sample(EnemyAttackPhase.Preparation, 1) -
                        attack.animation.Sample(EnemyAttackPhase.Active, 0)) < .0001f, "Preparation joins execution continuously");
                    Check(Mathf.Abs(attack.animation.Sample(EnemyAttackPhase.Active, 1) -
                        attack.animation.Sample(EnemyAttackPhase.Recovery, 0)) < .0001f, "Execution joins recovery continuously");
                }
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { ForestCreatureIntegration.Prefabs })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p).StartsWith(species + "_")))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(prefab.GetComponent<CreatureController>().Settings == settings, path + " shares updated settings");
                    var instance = Object.Instantiate(prefab);
                    try
                    {
                        var animator = instance.GetComponentInChildren<Animator>();
                        foreach (var attack in settings.attacks.Where(a => a.enabled))
                        {
                            var bindings = AnimationUtility.GetCurveBindings(attack.animation.PlaybackClip);
                            File.WriteAllText(Output + "bone-bindings.txt", string.Join("\n", bindings.Select(b => b.path).Distinct()) +
                                "\nMODEL\n" + string.Join("\n", animator.GetComponentsInChildren<Transform>().Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform))));
                            Check(bindings.Length > 30 && bindings.All(b => string.IsNullOrEmpty(b.path) || animator.transform.Find(b.path) != null),
                                prefab.name + " " + attack.label + " all animated bones resolve");
                            var body = animator.GetComponentsInChildren<Transform>().Single(t => t.name == "Body");
                            var head = animator.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
                            var clip = attack.animation.PlaybackClip;
                            clip.SampleAnimation(animator.gameObject, 0);
                            Vector3 idle = body.position; Quaternion idleHead = head.rotation;
                            clip.SampleAnimation(animator.gameObject, clip.length * attack.animation.activeStartsAt * .80f);
                            Check(Vector3.Distance(body.position, idle) > .06f || Quaternion.Angle(head.rotation, idleHead) > 12,
                                prefab.name + " " + attack.label + " visible anticipation displacement");
                            if (attack.label == "Jump")
                            {
                                clip.SampleAnimation(animator.gameObject, clip.length * attack.animation.Sample(EnemyAttackPhase.Active, .45f));
                                Check(body.position.y - idle.y > .45f, prefab.name + " pounce visibly airborne");
                            }
                            clip.SampleAnimation(animator.gameObject, clip.length);
                            Check(Vector3.Distance(body.position, idle) < .015f && Quaternion.Angle(head.rotation, idleHead) < 1,
                                prefab.name + " " + attack.label + " returns to rest without pose drift");
                        }
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                RenderStrip(species, settings);
            }
            Debug.Log("CREATURE_ATTACK_POLISH_PASS");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Output + "animation-checks.txt", "PASS " + message + "\n");
        }

        static void RenderStrip(string species, CreatureSettings settings)
        {
            const int size = 384, columns = 5;
            var preview = new PreviewRenderUtility();
            var texture = new Texture2D(size * columns, size * 2, TextureFormat.RGB24, false);
            var meshes = new System.Collections.Generic.List<Mesh>();
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ForestCreatureIntegration.Prefabs + "/" + species + "_Standard.prefab"));
                preview.AddSingleGO(root);
                var animator = root.GetComponentInChildren<Animator>();
                animator.enabled = false;
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins)
                {
                    var mesh = new Mesh(); meshes.Add(mesh);
                    var baked = new GameObject("Sampled skin"); baked.transform.SetParent(skin.transform, false);
                    var scale = skin.transform.lossyScale;
                    baked.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                    baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.12f, .15f, .19f);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = species == "Boar" ? 1.8f : 1.6f;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 40;
                preview.camera.transform.position = new Vector3(4, 2.6f, 5);
                preview.camera.transform.LookAt(new Vector3(0, .65f, 0));
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0);
                preview.lights[1].intensity = .65f;
                preview.ambientColor = new Color(.5f, .5f, .5f);
                int row = 0;
                foreach (var attack in settings.attacks.Where(a => a.enabled))
                {
                    float[] moments = { 0, attack.animation.activeStartsAt * .85f, attack.animation.activeStartsAt,
                        attack.animation.Sample(EnemyAttackPhase.Active, .45f), attack.animation.recoveryStartsAt };
                    for (int column = 0; column < columns; column++)
                    {
                        attack.animation.PlaybackClip.SampleAnimation(animator.gameObject, moments[column] * attack.animation.PlaybackClip.length);
                        for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(meshes[i]);
                        preview.BeginStaticPreview(new Rect(0, 0, size, size));
                        preview.Render(true);
                        var frame = preview.EndStaticPreview();
                        texture.SetPixels(column * size, (1-row) * size, size, size, frame.GetPixels());
                        Object.DestroyImmediate(frame);
                    }
                    row++;
                }
                texture.Apply(); File.WriteAllBytes(Output + species + "-poses.png", texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture); preview.Cleanup();
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }
    }
}
