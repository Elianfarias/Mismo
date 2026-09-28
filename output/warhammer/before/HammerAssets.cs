using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class HammerAssets
    {
        public const string Data="Assets/Data/Weapons/Hammer";
        public const string Art="Assets/Art/FBX/Weapons/Hammer";
        public const string Clips="Assets/Art/Animations/Weapons/Hammer";
        public const string Visual=Data+"/HammerVisual.prefab";
        const string Request="Temp/HammerAssets.request";
        static double next;
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request))return;File.Delete(Request);
            try{Create();}catch(Exception e){Directory.CreateDirectory("HammerChecks");File.WriteAllText("HammerChecks/assets.txt","FAIL\n"+e);Debug.LogException(e);}
        }
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static T Asset<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
            Folder(Path.GetDirectoryName(path).Replace('\\','/'));asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
        }
        [MenuItem("Mismo/Weapons/Create Stone Hammer")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            Folder(Data);Folder(Art);Folder(Clips);
            const string previousVisual="Assets/Prefabs/Weapons/HammerVisual.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Visual)==null&&AssetDatabase.LoadAssetAtPath<GameObject>(previousVisual)!=null)
                AssetDatabase.MoveAsset(previousVisual,Visual);
            var prefab=BuildVisual();
            var basic=Ability("HammerAttack","Golpe","hammer.strike",20,.28f,.12f,.30f,.70f,.18f);
            var heavy=Ability("HammerHeavy","Golpe pesado","hammer.heavy",35,.52f,.12f,.46f,1.30f,.65f);
            var slam=Ability("HammerGroundSlam","Golpe al suelo","hammer.slam",30,.65f,.10f,.40f,2.50f,.6f);
            slam.actions=new AbilityAction[]{new HammerImpactAction()};slam.description="Golpea el suelo: 30 de daño en un radio de 2.5 metros, empuje y breve stagger.";
            var spin=Ability("HammerDoubleSpin","Giro doble","hammer.double-spin",20,.20f,1.20f,.25f,4,.25f);
            spin.actions=new AbilityAction[]{new HammerImpactAction{damage=20,radius=2,forward=0,height=1,pushDistance=.25f,stunSeconds=0,hitTimes=new[]{.30f,.90f}}};
            spin.pose=AbilityPose.Spin;spin.description="Dos vueltas completas. Cada vuelta inflige 20 de daño una vez por enemigo.";
            var abilities=new[]{basic,heavy,slam,spin};
            foreach(var ability in abilities)EditorUtility.SetDirty(ability);
            var family=Asset<WeaponFamilyDefinition>("Assets/Data/WeaponFamilies/Hammer.asset");
            family.progressionId="hammer";family.displayName="Martillo a dos manos";family.abilities=abilities;family.repertoire=new[]{heavy,slam,spin};
            var pose=Asset<WeaponPoseProfile>(Data+"/HammerPose.asset");
            pose.equipped=new WeaponAttachmentPose{anchor=WeaponAnchor.BonePath,bonePath="Armature_Humanoid/Root/Hips/Spine/Chest/Clavicle.L/UpperArm.L/LowerArm.L/Hand.L",scale=1};
            pose.holstered=new WeaponAttachmentPose{anchor=WeaponAnchor.Character,offset=new Vector3(.18f,1.2f,-.27f),rotation=new Vector3(0,0,35),scale=1};
            pose.meleeTrail=true;pose.trailTip=new Vector3(0,1.12f,0);pose.trailWidth=.4f;
            var weapon=Asset<WeaponDefinition>(Data+"/Hammer.asset");weapon.Configure("hammer.stone","Martillo de piedra",.70f);
            weapon.abilities=abilities;weapon.family=family;weapon.isTwoHanded=true;weapon.visualPrefab=prefab;weapon.poseProfile=pose;
            weapon.inventoryDescription="Martillo rudimentario de piedra. Pesado, lento y devastador.";
            weapon.canDiscard=true;weapon.canSell=true;weapon.sellValue=10;weapon.gridWidth=2;weapon.gridHeight=3;
            weapon.inventoryIcon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/Sword/Sword.asset").inventoryIcon;
            weapon.inventoryPreviewRotation=new Vector3(0,25,0);
            var animations=Asset<WeaponAnimationSet>("Assets/Data/WeaponFamilies/HammerAnimations.asset");family.animations=animations;
            CreateAnimations(weapon,animations);
            HammerPolishAssets.Apply(weapon,animations);
            var recipe=Asset<CraftingRecipe>("Assets/Resources/Recipes/StoneHammer.asset");
            recipe.id="REC-HAMMER-STONE";recipe.displayName="Martillo de piedra";recipe.description="Martillo pesado de dos manos. Receta de prueba: Piedra x1.";
            var stone=Resources.LoadAll<MaterialDefinition>("").Single(m=>m.id=="MAT-03");
            recipe.ingredients=new[]{new RecipeIngredient{material=stone,quantity=1}};recipe.weaponResult=weapon;recipe.quantity=1;recipe.upgradeWeapon=false;recipe.category=RecipeCategory.Equipment;
            var catalog=Resources.Load<ItemCatalog>("ItemCatalog");if(!catalog.weapons.Contains(weapon))catalog.weapons=catalog.weapons.Concat(new[]{weapon}).ToArray();
            var settings=Resources.Load<GatheringSettings>("GatheringSettings");if(!settings.recipes.Contains(recipe))settings.recipes=settings.recipes.Concat(new[]{recipe}).ToArray();
            foreach(var obj in new Object[]{weapon,pose,family,animations,recipe,catalog,settings})EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssets();Directory.CreateDirectory("HammerChecks");
            File.WriteAllText("HammerChecks/assets.txt","PASS assets created\nPose offset="+pose.equipped.offset.ToString("F5")+" rotation="+pose.equipped.rotation.ToString("F5")+" scale="+pose.equipped.scale);
        }
        static AbilityDefinition Ability(string name,string display,string id,float damage,float prepare,float active,float recovery,float cooldown,float push)
        {
            var ability=Asset<AbilityDefinition>(Data+"/"+name+".asset");ability.displayName=display;ability.abilityId=id;
            ability.preparation=prepare;ability.active=active;ability.recovery=recovery;ability.cooldown=cooldown;
            ability.preparationMobility=0;ability.activeMobility=0;ability.staminaCost=0;ability.focusCost=0;ability.focusGainOnHit=5;
            ability.pose=AbilityPose.Lunge;ability.range=2;ability.usesSwordCombo=false;
            ability.description=display+": "+damage+" de daño.";
            ability.actions=new AbilityAction[]{new RepeatedStrikeAction{damage=damage,radius=.9f,forward=1.05f,interval=1,pushDistance=push}};
            return ability;
        }
        static GameObject BuildVisual()
        {
            string path=Art+"/Hammer_Double.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new InvalidOperationException("Missing local hammer model: "+path);
            if(importer.importAnimation||importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard)
            {importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();}
            var root=new GameObject("HammerVisual");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
                model.name="Hammer_Double";
                var bounds=new Bounds();bool first=true;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        string name=materials[i]!=null?materials[i].name:"Stone";
                        bool wood=name.IndexOf("Wood",StringComparison.OrdinalIgnoreCase)>=0;
                        string materialPath=Art+"/"+(wood?"HammerWood":"HammerStone")+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(material==null)
                        {
                            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=wood?new Color(.22f,.11f,.055f):new Color(.42f,.43f,.40f);
                            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);AssetDatabase.CreateAsset(material,materialPath);
                        }
                        materials[i]=material;
                    }
                    renderer.sharedMaterials=materials;
                }
                if(first||bounds.size.y<=.001f)throw new InvalidOperationException("Hammer model has invalid bounds.");
                float scale=1.35f/bounds.size.y;model.transform.localScale*=scale;
                model.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale-.16f,-bounds.center.z*scale);
                return PrefabUtility.SaveAsPrefabAsset(root,Visual);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static AnimationClip CopyClip(string source,string name)
        {
            string path=Clips+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(existing!=null)return existing;
            var clip=Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(source));clip.name=name;AnimationUtility.SetAnimationEvents(clip,new AnimationEvent[0]);AssetDatabase.CreateAsset(clip,path);return clip;
        }
        static void CreateAnimations(WeaponDefinition weapon,WeaponAnimationSet animations)
        {
            const string human="Assets/Art/Animations/HumanMelee/";
            var idle=CopyClip(human+"Human_Player_CombatIdle2H01.anim","Hammer_Idle");
            var attack=CopyClip(human+"Human_Player_Attack2H01.anim","Hammer_Attack");
            var heavy=CopyClip(human+"Human_Player_Attack2H01.anim","Hammer_Heavy");
            var slam=CopyClip(human+"Human_Player_Attack2H01.anim","Hammer_GroundSlam");
            var spin=CopyClip(human+"Human_Player_WeaponHold2H01.anim","Hammer_DoubleSpin");
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            var model=character.GetComponentInChildren<Animator>().gameObject;
            try
            {
                idle.SampleAnimation(model,0);
                var right=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.R");
                var left=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.L");
                if(right==null)throw new InvalidOperationException("Hammer hand bone is missing.");
                // In this existing two-hand clip the left hand is the rear grip.
                Vector3 shaft=(right.position-left.position).normalized;
                Quaternion orientation=Quaternion.LookRotation(Vector3.Cross(model.transform.forward,shaft).normalized,shaft);
                weapon.poseProfile.equipped.rotation=(Quaternion.Inverse(left.rotation)*orientation).eulerAngles;
                weapon.poseProfile.equipped.offset=Vector3.zero;
                Directory.CreateDirectory("HammerChecks");File.WriteAllText("HammerChecks/grip.txt","Right="+right.position+" Left="+left.position+" Shaft="+shaft+"\n");
                // Bake an extended two-hand pose and the two turns on the rig root. The motor's facing stays unchanged.
                var root=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Root");
                string rootPath=AnimationUtility.CalculateTransformPath(root,model.transform);
                var hold=idle;
                foreach(var binding in AnimationUtility.GetCurveBindings(spin))AnimationUtility.SetEditorCurve(spin,binding,null);
                foreach(var binding in AnimationUtility.GetCurveBindings(hold))
                {
                    var curve=AnimationUtility.GetEditorCurve(hold,binding);float value=curve.Evaluate(hold.length*.5f);
                    AnimationUtility.SetEditorCurve(spin,binding,AnimationCurve.Constant(0,1.65f,value));
                }
                // This rig's local Y is not world up. Preserve its bind orientation and bake world-space yaw.
                var turns=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
                Quaternion start=root.rotation,parent=root.parent.rotation;
                for(int frame=0;frame<=99;frame++)
                {
                    float time=frame/60f,yaw=720*Mathf.Clamp01((time-.2f)/1.2f);
                    Quaternion q=Quaternion.Inverse(parent)*Quaternion.AngleAxis(yaw,Vector3.up)*start;
                    float[] values={q.x,q.y,q.z,q.w};for(int i=0;i<4;i++)turns[i].AddKey(time,values[i]);
                }
                for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(spin,EditorCurveBinding.FloatCurve(rootPath,typeof(Transform),"m_LocalRotation."+"xyzw"[i]),turns[i]);
                spin.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(spin);settings.loopTime=false;settings.stopTime=1.65f;AnimationUtility.SetAnimationClipSettings(spin,settings);
                EditorUtility.SetDirty(spin);
            }
            finally{Object.DestroyImmediate(character);}
            var actionClips=new[]{attack,heavy,slam,spin};
            animations.actions=weapon.abilities.Select((a,i)=>new AbilityAnimationBinding{ability=a,clip=actionClips[i],maskMode=ActionMaskMode.FullBody,
                activeStartsAt=i==3?.2f/1.65f:.52f,recoveryStartsAt=i==3?1.4f/1.65f:.62f,blendSeconds=.035f}).ToArray();
            var baseController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/VoxelLocomotion.controller");
            string path=Clips+"/HammerAnimations.overrideController";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if(controller==null){controller=new AnimatorOverrideController(baseController);AssetDatabase.CreateAsset(controller,path);}
            var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
            for(int i=0;i<overrides.Count;i++)if(overrides[i].Key.name.IndexOf("Idle",StringComparison.OrdinalIgnoreCase)>=0)overrides[i]=new KeyValuePair<AnimationClip,AnimationClip>(overrides[i].Key,idle);
            controller.ApplyOverrides(overrides);animations.locomotion=controller;EditorUtility.SetDirty(controller);
        }
        public static void RunBatch(){try{Create();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
