using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponSystemBuilder
    {
        const string Folder = "Assets/Data/Weapons";
        [MenuItem("Mismo/Combat/Create Sword And Bow Loadout")]
        public static void Apply()
        {
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
            var arrow = Visual("arrow_B", .85f, true);
            var bowVisual = Visual("bow_B_withString", 1.15f, false);
            var swordVisual = Visual("sword_E", 1.1f, false);
            var basic = Ability("SwordCombo", "COMBO", 0, .38f, .16f, .45f, 0, AbilityPose.None);
            basic.usesSwordCombo = true;
            var lunge = Ability("SwordLunge", "ESTOCADA", .02f, .24f, .14f, .8f, 12, AbilityPose.Lunge,
                new MoveCasterAction { distance = 3.2f }, new MeleeAction { damage = 18, radius = .6f, forward = .7f });
            var parry = Ability("SwordParry", "PARRY", 0, .16f, .12f, .45f, 0, AbilityPose.Parry, new ParryAction());
            var spin = Ability("SwordSpin", "GIRO", .04f, .55f, .18f, 1.1f, 20, AbilityPose.Spin,
                new MeleeAction { damage = 24, radius = 1.6f, forward = 0 });
            var shot = Ability("BowShot", "FLECHA", .4f, .02f, .35f, .85f, 0, AbilityPose.Bow,
                new ProjectileAction { visual = arrow, damage = 8, speed = 28 });
            var power = Ability("BowPower", "POTENTE", .42f, .02f, .3f, 3, 12, AbilityPose.Bow,
                new ProjectileAction { visual = arrow, damage = 24, postureDamage = 45, speed = 38 });
            var retreat = Ability("BowRetreat", "RETIRADA", .45f, .25f, .3f, 4, 12, AbilityPose.Bow,
                new MoveCasterAction { distance = -2.5f }, new ProjectileAction { visual = arrow, damage = 6, speed = 28 });
            var rain = Ability("BowArea", "LLUVIA", .35f, .02f, .25f, 7, 20, AbilityPose.Bow,
                new GroundAreaAction { radius = 2.5f, duration = 3, interval = .6f, damage = 5, fallingVisual = arrow });
            rain.targetsGround = true; rain.range = 18;
            shot.chargeable=true;shot.maximumCharge=1.2f;shot.focusCost=0;shot.focusGainOnHit=5;
            power.focusCost=20;power.preparation=.65f;
            retreat.preparation=.45f;rain.preparation=.7f;
            foreach(var ranged in new[]{shot,power,retreat,rain})
            { ranged.preparationMobility=.4f;ranged.interruptible=true;ranged.cancelPreparation=true; }
            foreach (var ranged in new[] { shot, power, retreat, rain }) ranged.aimFromCamera = true;
            var sword = Asset<WeaponDefinition>("Sword"); sword.Configure("sword.basic", "Espada", .45f);
            sword.abilities = new[] { basic, lunge, parry, spin }; sword.visualPrefab = swordVisual;
            var bow = Asset<WeaponDefinition>("Bow"); bow.Configure("bow.basic", "Arco", .45f); bow.isBow = true;
            bow.abilities = new[] { shot, power, retreat, rain }; bow.visualPrefab = bowVisual;
            bow.handRotation = Vector3.zero; bow.handOffset = new Vector3(0, 0, -.10612866f); bow.backRotation = new Vector3(0, 0, -25);
            var set = AssetDatabase.LoadAssetAtPath<WeaponSetDefinition>("Assets/Resources/StartingWeapons.asset");
            if (set == null) { set = ScriptableObject.CreateInstance<WeaponSetDefinition>(); AssetDatabase.CreateAsset(set, "Assets/Resources/StartingWeapons.asset"); }
            set.primary = sword; set.secondary = bow;
            foreach (var asset in new Object[] { basic, lunge, parry, spin, shot, power, retreat, rain, sword, bow, set }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("WEAPON_ASSETS_READY");
        }
        static T Asset<T>(string name) where T : ScriptableObject
        {
            string path = Folder + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static AbilityDefinition Ability(string id, string label, float preparation, float active, float recovery, float cooldown, float cost, AbilityPose pose, params AbilityAction[] actions)
        {
            var d = Asset<AbilityDefinition>(id); d.displayName = label; d.preparation = preparation; d.active = active; d.recovery = recovery;
            d.cooldown = cooldown; d.staminaCost = cost; d.pose = pose; d.actions = actions; return d;
        }
        static GameObject Visual(string modelName, float length, bool arrow)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FBX/Weapons/fbx(unity)/" + modelName + ".fbx");
            if (source == null) throw new Exception("Missing weapon model " + modelName);
            var root = new GameObject(modelName + " Visual");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = length / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                Vector3 axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? Vector3.right : bounds.size.y >= bounds.size.z ? Vector3.up : Vector3.forward;
                model.transform.localScale *= scale;
                model.transform.localRotation = Quaternion.FromToRotation(axis, arrow ? Vector3.forward : Vector3.up) * model.transform.localRotation;
                bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.position -= bounds.center;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Weapons.mat");
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/FBX/Weapons/Textures/weapons_bits_texture.png");
                    mat.SetTexture(mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", texture);
                    AssetDatabase.CreateAsset(mat, Folder + "/Weapons.mat");
                }
                foreach (var renderer in renderers) renderer.sharedMaterials = Enumerable.Repeat(mat, renderer.sharedMaterials.Length).ToArray();
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + modelName + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void RunBatch()
        { try { Apply(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
    }
}

