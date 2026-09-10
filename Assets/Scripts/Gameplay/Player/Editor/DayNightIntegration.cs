using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mismo.Gameplay.Player.World;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class DayNightIntegration
    {
        const string Source="Assets/Art/Texture/Skybox/Textures/";
        static Cubemap Sky(string name)
        {
            string path=Source+name+".exr";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.TextureCube;importer.generateCubemap=TextureImporterGenerateCubemap.AutoCubemap;importer.sRGBTexture=false;
            importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        }
        static SkyPhase Phase(string name,float hour,Cubemap sky,float exposure,float intensity,Color light,Color ambient,Color fog)=>new SkyPhase{name=name,hour=hour,sky=sky,exposure=exposure,sunIntensity=intensity,sunColor=light,ambientColor=ambient,fogColor=fog};
        [MenuItem("Mismo/World/Configure day and night")]
        public static void Apply()
        {
            var night=Sky("Deep Midnight");var dawn=Sky("Cotton Candy Morning");var warm=Sky("Warm Sunrise Glow");var day=Sky("Cloudy Bright Day");var noon=Sky("Tropical Noon");
            const string path="Assets/Resources/DayNightSettings.asset";var settings=AssetDatabase.LoadAssetAtPath<DayNightSettings>(path);
            if(settings==null)
            {
                settings=ScriptableObject.CreateInstance<DayNightSettings>();
                var blue=new Color(.55f,.68f,1);var nightAmbient=new Color(.20f,.25f,.36f);var nightFog=new Color(.035f,.05f,.10f);
                settings.phases=new[]{
                    Phase("Noche",0,night,.15f,.16f,blue,nightAmbient,nightFog),
                    Phase("Noche",5,night,.15f,.16f,blue,nightAmbient,nightFog),
                    Phase("Amanecer",6,dawn,.8f,.45f,new Color(1,.65f,.5f),new Color(.34f,.29f,.38f),new Color(.48f,.37f,.44f)),
                    Phase("Mañana",8,warm,.8f,.8f,new Color(1,.82f,.62f),new Color(.48f,.48f,.46f),new Color(.65f,.62f,.57f)),
                    Phase("Día",10,day,.8f,1.1f,new Color(1,.97f,.9f),new Color(.57f,.67f,.77f),new Color(.62f,.72f,.78f)),
                    Phase("Mediodía",12,noon,.8f,1.2f,new Color(1,.98f,.93f),new Color(.6f,.7f,.8f),new Color(.58f,.72f,.81f)),
                    Phase("Tarde",16,day,.8f,.95f,new Color(1,.88f,.7f),new Color(.54f,.57f,.62f),new Color(.65f,.69f,.72f)),
                    Phase("Atardecer",18,warm,.65f,.55f,new Color(1,.52f,.28f),new Color(.35f,.27f,.31f),new Color(.48f,.30f,.24f)),
                    Phase("Noche",20,night,.15f,.16f,blue,nightAmbient,nightFog)};
                AssetDatabase.CreateAsset(settings,path);
            }
            var textures=new[]{night,dawn,warm,day,noon};
            foreach(var phase in settings.phases)
            {
                string name=phase.hour<=5||phase.hour>=20?"Deep Midnight":phase.hour==6?"Cotton Candy Morning":phase.hour==8||phase.hour==18?"Warm Sunrise Glow":phase.hour==12?"Tropical Noon":"Cloudy Bright Day";
                if(phase.sky==null)phase.sky=textures.First(t=>t.name==name);
            }
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Apply();Directory.CreateDirectory("Docs/Validation/DayNight");
                var settings=Object.Instantiate(Resources.Load<DayNightSettings>("DayNightSettings"));
                if(settings.phases.Select(p=>p.sky).Distinct().Count()!=5)throw new Exception("Missing supplied panorama");
                for(float h=0;h<24;h+=.25f){settings.Sample(h,out var a,out var b,out float t);if(a==null||b==null||t<0||t>1)throw new Exception("Invalid phase interpolation at "+h);}
                var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(40,15,-40);camera.transform.LookAt(new Vector3(0,5,0));camera.farClipPlane=500;
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*30;var mat=new Material(Shader.Find("Standard")){color=new Color(.3f,.45f,.2f)};ground.GetComponent<Renderer>().sharedMaterial=mat;
                var village=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Villages/MedievalVillage.prefab");if(village!=null)Object.Instantiate(village);
                var cycle=new GameObject("Day-night cycle").AddComponent<DayNightCycle>();cycle.Initialize(settings);
                cycle.SetHour(23);cycle.Advance(settings.cycleMinutes*60/24*2);if(Mathf.Abs(cycle.hour-1)>.001f)throw new Exception("Clock midnight wrap");
                settings.running=false;cycle.Advance(60);if(Mathf.Abs(cycle.hour-1)>.001f)throw new Exception("Stopped clock advances");
                var record=new WorldSaveData{id=Guid.NewGuid().ToString("N"),seed=1,settingsJson="{}"};if(!record.IsValid())throw new Exception("Old saves incompatible");
                record.hasTimeOfDay=true;record.timeOfDay=18.5f;var restored=JsonUtility.FromJson<WorldSaveData>(JsonUtility.ToJson(record));if(!restored.IsValid()||restored.timeOfDay!=18.5f)throw new Exception("Hour persistence");record.timeOfDay=24;if(record.IsValid())throw new Exception("Invalid clock accepted");
                var world=Resources.Load<WorldContentCatalog>("WorldContentCatalog");
                var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;
                foreach(float hour in new[]{6f,12f,18f,0f})
                {
                    world.ApplyAtmosphere(3);cycle.SetHour(hour);
                    if(RenderSettings.skybox==null||RenderSettings.sun==null)throw new Exception("Environment not applied");
                    camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();File.WriteAllBytes("Docs/Validation/DayNight/Hour_"+hour.ToString("00")+".png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);
                }
                RenderTexture.active=null;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
                File.WriteAllText("Docs/Validation/DayNight/Checks.txt","PASS: all 5 HDR skies, 96 clock samples, midnight wrap, stopped clock, old-save compatibility, saved-hour round trip, invalid-hour rejection, sky/sun assignment, four rendered phases.");
                Debug.Log("DAY_NIGHT_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
