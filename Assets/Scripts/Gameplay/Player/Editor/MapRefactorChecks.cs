using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class MapRefactorChecks
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        [InitializeOnLoadMethod] static void Watch(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(".validation/map-check.request"))return;
            File.Delete(".validation/map-check.request");Run();
        }
        public static void RunBatch(){Run();EditorApplication.Exit(File.ReadAllText(".validation/map-checks.txt").Contains("FAIL")?1:0);}
        [MenuItem("Mismo/Mapa/Verificar mapa")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Ejecutá la verificación fuera de Play Mode.");return;}
            var notes=new List<string>();GameObject player=null;ExplorationWorldSettings settings=null;
            var scene=EditorSceneManager.NewPreviewScene();WorldMapPanel map=null;
            var current=WorldSession.Current;var currentProperty=typeof(WorldSession).GetProperty("Current");
            var savedLock=Cursor.lockState;bool savedVisible=Cursor.visible;
            try
            {
                currentProperty.SetValue(null,null);
                var catalog=Resources.Load<MapMarkerCatalog>("MapMarkerCatalog");
                Check(catalog!=null&&catalog.ValidationError()==null,"Catálogo válido",notes);
                Check(catalog.types.Count==11&&catalog.types.TrueForAll(t=>t.icon!=null),"Once iconos del pack referenciados",notes);
                Check(catalog.villageIcon!=null,"Pueblo usa home.png",notes);
                foreach(var type in catalog.types)Check(MapIcons.Mask(type.icon)!=null,"Máscara legible: "+type.id,notes);
                settings=Object.Instantiate(Resources.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));settings.preserveAuthoredCenter=false;
                var terrain=new ExplorationTerrain(settings);var village=terrain.Site(Vector2Int.zero);
                player=new GameObject("Map verification");SceneManager.MoveGameObjectToScene(player,scene);player.transform.position=village.position;
                map=player.AddComponent<WorldMapPanel>();map.Initialize(settings);
                var camera=(UnityEngine.Camera)typeof(WorldMapPanel).GetField("mapCamera",Private).GetValue(map);
                var surface=(GameObject)typeof(WorldMapPanel).GetField("surface",Private).GetValue(map);
                SceneManager.MoveGameObjectToScene(camera.gameObject,scene);SceneManager.MoveGameObjectToScene(surface,scene);camera.scene=scene;
                Check(map.Open(),"Abrir mapa",notes);map.SetZoom(1);Check(map.Zoom==40,"Zoom mínimo",notes);map.SetZoom(999999);Check(map.Zoom==2400,"Zoom máximo",notes);
                map.SetZoom(210);map.Pan(new Vector2(40,-20));map.Recenter();Check(map.Center==new Vector2(player.transform.position.x,player.transform.position.z),"Centrar",notes);
                var build=(IEnumerator)typeof(WorldMapPanel).GetMethod("Build",Private).Invoke(map,null);while(build.MoveNext()){}
                typeof(WorldMapPanel).GetMethod("LateUpdate",Private).Invoke(map,null);Check(map.Ready,"Relieve generado",notes);
                Capture(map.Preview,".validation/map-relief.png");
                var pin=new MapPin{id=Guid.NewGuid().ToString("N"),typeId="chest",name="Tesoro propio",x=village.position.x+20,z=village.position.z+30};
                Check(map.SavePin(pin)&&map.Pins.Count==1,"Crear marcador",notes);pin.name="Tesoro editado";pin.typeId="danger";
                Check(map.SavePin(pin)&&map.Pins.Count==1&&map.Pins[0].name==pin.name,"Editar sin duplicar",notes);
                Check(!map.SavePin(new MapPin{id="bad",typeId="flag",name="",x=0,z=0}),"Rechazar nombre vacío",notes);
                var record=new WorldSaveData{id=Guid.NewGuid().ToString("N"),seed=123,settingsJson="{}",mapPins=new List<MapPin>{pin}};
                Check(record.IsValid(),"Partida con marcador válida",notes);
                var repo=new ProtectedProfileRepository(Path.GetFullPath(".validation/map-roundtrip.mismo"));repo.Read(json=>JsonUtility.FromJson<WorldSaveData>(json).IsValid(),out _);repo.Write(JsonUtility.ToJson(record));
                repo.Read(json=>JsonUtility.FromJson<WorldSaveData>(json).IsValid(),out var saved);
                var restored=JsonUtility.FromJson<WorldSaveData>(saved);Check(restored.mapPins[0].name==pin.name&&restored.mapPins[0].x==pin.x,"Persistencia protegida conserva nombre, tipo y coordenadas",notes);
                restored.mapPins=null;Check(restored.IsValid(),"Compatibilidad con partidas anteriores",notes);
                Check(map.DeletePin(pin.id)&&map.Pins.Count==0,"Eliminar marcador",notes);
                Check(!map.TravelTo(new WorldSite{kind=WorldSiteKind.Ruin}),"Rechazar viaje a sitios que no son pueblos",notes);
                // Verify projected point maps back to the same geographic surface location.
                var rect=new Rect(0,0,Screen.width,Screen.height);var point=village.position+new Vector3(8,0,8);
                var projected=(Vector2)typeof(WorldMapPanel).GetMethod("Project",Private).Invoke(map,new object[]{point,rect});
                var args=new object[]{projected,rect,Vector3.zero};bool picked=(bool)typeof(WorldMapPanel).GetMethod("Pick",Private).Invoke(map,args);
                var hit=(Vector3)args[2];Check(picked&&Vector2.Distance(new Vector2(point.x,point.z),new Vector2(hit.x,hit.z))<6,"Picking sobre el relieve",notes);
                map.Close();player.transform.rotation=Quaternion.Euler(0,90,0);
                typeof(WorldMapPanel).GetField("nextRender",Private).SetValue(map,0f);typeof(WorldMapPanel).GetMethod("LateUpdate",Private).Invoke(map,null);
                Check(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y,90))<.1f,"Minimapa sigue el giro del personaje",notes);
                notes.Add("PASS");
            }
            catch(Exception e){notes.Add("FAIL "+e);Debug.LogException(e);}
            finally
            {
                if(map!=null)
                {
                    map.Close();
                    foreach(string name in new[]{"mapCamera","surface","mesh","material","texture"})
                    {var obj=typeof(WorldMapPanel).GetField(name,Private).GetValue(map) as Object;if(obj is Component component)obj=component.gameObject;if(obj!=null)Object.DestroyImmediate(obj);typeof(WorldMapPanel).GetField(name,Private).SetValue(map,null);}
                }
                if(player!=null)Object.DestroyImmediate(player);if(settings!=null)Object.DestroyImmediate(settings);
                EditorSceneManager.ClosePreviewScene(scene);currentProperty.SetValue(null,current);Cursor.lockState=savedLock;Cursor.visible=savedVisible;
                Directory.CreateDirectory(".validation");File.WriteAllLines(".validation/map-checks.txt",notes);
            }
        }
        static void Check(bool value,string text,List<string> notes){if(!value)throw new Exception(text);notes.Add("PASS "+text);}
        static void Capture(RenderTexture source,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=source;var image=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=previous;Object.DestroyImmediate(image);
        }
    }
}


