using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class VoxelCharacterIntegration
    {
        public const string ModelPath = "Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx";
        public const string ControllerPath = "Assets/Art/Animations/VoxelLocomotion.controller";
        public const string PrefabPath = "Assets/Prefabs/Player/PlayerVoxelSwordE.prefab";
        [MenuItem("Mismo/Character/Integrate Sword E And Locomotion")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string name=clip.name.Split('|').Last();
                clip.loopTime=name=="Idle"||name=="Walk"||name=="Run";
                clip.loopPose=clip.loopTime;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var all = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm=controller.layers[0].stateMachine;
            foreach(var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach(var state in sm.states)sm.RemoveState(state.state);
            controller.parameters=Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Motion",AnimatorControllerParameterType.Int);
            controller.AddParameter("ActionTime",AnimatorControllerParameterType.Float);
            controller.AddParameter("PlaybackRate",AnimatorControllerParameterType.Float);
            var parameters=controller.parameters;
            parameters.First(p=>p.name=="PlaybackRate").defaultFloat=1;controller.parameters=parameters;
            foreach(CharacterMotion motion in Enum.GetValues(typeof(CharacterMotion)))
            {
                string name=motion.ToString();
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Retargeted/Quaternius_"+name+".anim") ?? all.First(c=>c.name.Split('|').Last()==name);
                var state=sm.AddState(name,new Vector3(260+((int)motion%4)*210,80+((int)motion/4)*100,0));state.motion=clip;
                state.writeDefaultValues=true;
                if(motion==CharacterMotion.Land && clip.name.StartsWith("Quaternius_"))state.speed=clip.length/.18f;
                if(motion==CharacterMotion.Idle)sm.defaultState=state;
                if(motion==CharacterMotion.Walk||motion==CharacterMotion.Run) {state.speedParameter="PlaybackRate";state.speedParameterActive=true;}
                if(motion==CharacterMotion.Jump || motion==CharacterMotion.Attack1 || motion==CharacterMotion.Attack2 || motion==CharacterMotion.Attack3 || motion==CharacterMotion.Parry || motion==CharacterMotion.Lunge || motion==CharacterMotion.Spin)
                {state.timeParameter="ActionTime";state.timeParameterActive=true;}
                var t=sm.AddAnyStateTransition(state);t.hasExitTime=false;t.hasFixedDuration=true;
                t.duration=(int)motion>=(int)CharacterMotion.Attack1?.035f:.09f;
                t.canTransitionToSelf=false;t.AddCondition(AnimatorConditionMode.Equals,(int)motion,"Motion");
            }
            controller.AddParameter("LocomotionSpeed",AnimatorControllerParameterType.Float);
            var locomotion=new BlendTree {name="Continuous Locomotion",blendType=BlendTreeType.Simple1D,blendParameter="LocomotionSpeed",useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(locomotion,controller);
            for(int i=0;i<3;i++)
            {
                string motionName=((CharacterMotion)i).ToString();
                locomotion.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Quaternius/Retargeted/Quaternius_"+motionName+".anim") ?? all.First(c=>c.name.Split('|').Last()==motionName),i);
            }
            var idle=sm.states.First(s=>s.state.name=="Idle").state;
            idle.motion=locomotion;idle.speedParameter="PlaybackRate";idle.speedParameterActive=true;
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player/Player.prefab");
            try { Configure(prefab,controller); PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");
            foreach(var player in Object.FindObjectsByType<PlayerController>()) Configure(player.gameObject,controller);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("VOXEL_INTEGRATION_OK");
        }
        private static void Configure(GameObject player, RuntimeAnimatorController controller)
        {
            var existing=player.transform.Find("VoxelAdventurer");
            if(existing!=null) Object.DestroyImmediate(existing.gameObject);
            foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
                if(!(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer)) renderer.enabled=false;
            foreach(var old in player.GetComponentsInChildren<Animator>(true)) old.enabled=false;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),player.transform);
            model.name="VoxelAdventurer"; model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.layer;
            string matFolder="Assets/Art/Animations/VoxelMaterials";
            if(!AssetDatabase.IsValidFolder(matFolder))AssetDatabase.CreateFolder("Assets/Art/Animations","VoxelMaterials");
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i]; if(source==null)continue;
                    string safe=System.Text.RegularExpressions.Regex.Replace(source.name,@"[^a-zA-Z0-9_-]","_");
                    string path=matFolder+"/"+safe+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null)
                    {
                        var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        material=new Material(shader);material.name=source.name;
                        Color color=source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
                        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
                        if(material.HasProperty("_Color"))material.SetColor("_Color",color);
                        if(source.name.ToLowerInvariant().Contains("weapons"))
                        {
                            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/FBX/Weapons/Textures/weapons_bits_texture.png");
                            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
                            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
                        }
                        AssetDatabase.CreateAsset(material,path);
                    }
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            var animator=model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var motor=player.GetComponent<PlayerMotor>();
            var so=new SerializedObject(motor);var settings=(MovementSettings)so.FindProperty("settings").objectReferenceValue;
            motor.Configure(settings,model.transform);
            var driver=player.GetComponent<PlayerAnimationDriver>() ?? player.AddComponent<PlayerAnimationDriver>();driver.Configure(animator);
            var driverData=new SerializedObject(driver);driverData.FindProperty("referenceWalkSpeed").floatValue=settings.WalkSpeed;driverData.FindProperty("referenceRunSpeed").floatValue=settings.SprintSpeed;driverData.ApplyModifiedPropertiesWithoutUndo();
            var hand=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hand.R");
            var feedback=player.GetComponent<SwordAnimationFeedback>() ?? player.AddComponent<SwordAnimationFeedback>();feedback.ConfigureRig(hand);feedback.UseAuthoredAnimations();
            var movementFeedback=player.GetComponent<MovementFeedback>() ?? player.AddComponent<MovementFeedback>();movementFeedback.UseAuthoredAnimations();
            EditorUtility.SetDirty(movementFeedback);PrefabUtility.RecordPrefabInstancePropertyModifications(movementFeedback);
            foreach(var c in new Component[]{motor,driver,feedback}) { EditorUtility.SetDirty(c); PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        }
        public static void RunBatch()
        {
            try { Apply(); VoxelAnimationChecks.Begin(); }
            catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
        }
        [MenuItem("Mismo/Character/Repair Animation Clip Bindings")]
        public static void RepairAnimationBindings()
        {
            // Model imports also contain editor preview clips, which do not honor runtime looping.
            var clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach(var item in controller.layers[0].stateMachine.states)
                item.state.motion=clips.Single(c=>c.name.Split('|').Last()==item.state.name);
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        public static void RepairAndCheckBatch()
        {
            try { RepairAnimationBindings();VoxelAnimationChecks.Begin(); }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
