using System;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class GoblinConceptIntegration
    {
        public const string Model="Assets/Art/FBX/Goblins/Goblin_Concept_Animated.fbx";
        const string Folder="Assets/Art/Animations/GoblinConcept";
        [MenuItem("Mismo/Character/Integrate Concept Goblin")]
        public static void Apply()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;importer.optimizeGameObjects=false;
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips)
            {
                string n=clip.name.Split('|').Last();clip.loopTime=n=="Idle"||n=="Walk"||n=="Run";clip.loopPose=clip.loopTime;
            }
            importer.clipAnimations=clips;importer.SaveAndReimport();
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/Animations","GoblinConcept");
            var all=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Goblin.controller")??AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Goblin.controller");
            var sm=controller.layers[0].stateMachine;
            foreach(var t in sm.anyStateTransitions)sm.RemoveAnyStateTransition(t);
            foreach(var st in sm.states)sm.RemoveState(st.state);
            controller.parameters=Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Motion",AnimatorControllerParameterType.Int);controller.AddParameter("ActionTime",AnimatorControllerParameterType.Float);controller.AddParameter("PlaybackRate",AnimatorControllerParameterType.Float);
            var names=new[]{"Idle","Walk","Run","Attack","Hit"};
            for(int i=0;i<names.Length;i++)
            {
                var state=sm.AddState(names[i]);state.motion=all.Single(c=>c.name.Split('|').Last()==names[i]);
                if(i==0)sm.defaultState=state;
                if(i==1||i==2){state.speedParameter="PlaybackRate";state.speedParameterActive=true;}
                if(i>=3){state.timeParameter="ActionTime";state.timeParameterActive=true;}
                var t=sm.AddAnyStateTransition(state);t.hasExitTime=false;t.hasFixedDuration=true;t.duration=i>=3?.035f:.08f;t.canTransitionToSelf=false;t.AddCondition(AnimatorConditionMode.Equals,i,"Motion");
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            foreach(string path in new[]{GoblinPrototypeTool.PrefabPath,GoblinPrototypeTool.ElitePrefabPath})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual=root.transform.Find("Visual");
                    foreach(Transform child in visual)child.gameObject.SetActive(false);
                    var old=visual.Find("ConceptGoblin");
                    var model=old!=null?old.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),visual);
                    model.SetActive(true);
                    model.name="ConceptGoblin";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one*.8f;
                    foreach(var r in model.GetComponentsInChildren<Renderer>())
                    {
                        var materials=r.sharedMaterials;
                        for(int j=0;j<materials.Length;j++)
                        {
                            var source=materials[j];string safe=System.Text.RegularExpressions.Regex.Replace(source.name,@"[^a-zA-Z0-9_-]","_");string mp=Folder+"/"+safe+".mat";
                            var material=AssetDatabase.LoadAssetAtPath<Material>(mp);
                            if(material==null)
                            {
                                material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));material.name=source.name;
                                var color=source.HasProperty("_Color")?source.color:Color.white;
                                material.SetColor(material.HasProperty("_BaseColor")?"_BaseColor":"_Color",color);
                                material.SetFloat(material.HasProperty("_Smoothness")?"_Smoothness":"_Glossiness",.15f);
                                if(source.name.Contains("Sulfur")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.5f);}
                                AssetDatabase.CreateAsset(material,mp);
                            }
                            materials[j]=material;
                        }
                        r.sharedMaterials=materials;
                    }
                    var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var driver=root.GetComponent<GoblinAnimationDriver>()??root.AddComponent<GoblinAnimationDriver>();driver.Configure(animator);
                    root.GetComponent<GoblinPresentation>().UseAuthoredAnimation();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            // Verify the existing world resolves the updated prefab instances without rebuilding it.
            EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");
            var enemies=Object.FindObjectsByType<GoblinController>();
            if(enemies.Length==0)throw new Exception("No goblins in gameplay scene");
            foreach(var enemy in enemies)
                if(enemy.GetComponent<GoblinAnimationDriver>()==null)throw new Exception("Scene goblin does not inherit updated prefab: "+enemy.name);
            Debug.Log("GOBLIN_CONCEPT_INTEGRATED "+enemies.Length+" scene enemies; base and elite prefabs");
        }
        public static void RunBatch()
        {
            try{Apply();GoblinPlayModeChecks.RunBatch();}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void CaptureBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrototypeTool.PrefabPath));
                var model=root.transform.Find("Visual/ConceptGoblin").gameObject;
                var clip=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview")&&c.name.Split('|').Last()=="Idle");
                clip.SampleAnimation(model,0);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*10;
                floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.22f,.20f,.17f)};
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
                var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);
                var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.transform.position=new Vector3(-2.5f,1.9f,4);camera.transform.LookAt(new Vector3(0,.75f,0));camera.orthographic=true;camera.orthographicSize=.95f;camera.backgroundColor=new Color(.14f,.13f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
                var rt=new RenderTexture(800,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(800,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,900),0,0);image.Apply();
                System.IO.Directory.CreateDirectory("Docs/Validation");System.IO.File.WriteAllBytes("Docs/Validation/GoblinConcept_Unity.png",image.EncodeToPNG());
                Debug.Log("GOBLIN_UNITY_PREVIEW_OK");
                if(Environment.GetCommandLineArgs().Contains("-goblin-checks"))GoblinPlayModeChecks.RunBatch();else EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
