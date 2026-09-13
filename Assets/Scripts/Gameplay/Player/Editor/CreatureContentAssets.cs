using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
namespace Mismo.Gameplay.Player.Editor
{
    public static class CreatureContentAssets
    {
        static T Asset<T>(string path)where T:ScriptableObject {var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value!=null)return value;value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;}
        [MenuItem("Mismo/Criaturas/Preparar bestiario y efectos")]
        public static void Create()
        {
            Directory.CreateDirectory("Assets/Resources/Bestiary");Directory.CreateDirectory("Assets/Data/RiderPoses");AssetDatabase.Refresh();
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Enemies"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var entries=new List<CreatureSpecies>();
            string[] ids={"boar","spider","goblin","golem","boss"};string[] names={"Jabalí","Araña","Goblin","Gólem","Guardián"};string[] match={"Boar_","Spider_","Goblin","Golem","Boss"};
            for(int i=0;i<ids.Length;i++)
            {
                var models=paths.Where(p=>Path.GetFileNameWithoutExtension(p).IndexOf(match[i],StringComparison.OrdinalIgnoreCase)>=0).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();if(models.Length==0)continue;
                var entry=Asset<CreatureSpecies>("Assets/Resources/Bestiary/"+ids[i]+".asset");
                if(string.IsNullOrEmpty(entry.id)){entry.id=ids[i];entry.displayName=names[i];entry.description=i==0?"Habitante del bosque. Puede reconocerte como su amo después de vencerlo.":"Una criatura descubierta durante tus viajes.";entry.domesticable=entry.mountable=i==0;entry.recognitionChance=.1f;}
                entry.prefabs=models;
                if(entry.mountable&&entry.riderPose==null)entry.riderPose=Asset<RiderPose>("Assets/Data/RiderPoses/"+ids[i]+".asset");
                if(entry.riderPose!=null&&(entry.riderPose.bones==null||entry.riderPose.bones.Length==0))DefaultPose(entry);
                EditorUtility.SetDirty(entry);entries.Add(entry);
            }
            var book=Asset<BestiaryBook>("Assets/Resources/BestiaryBook.asset");book.pages=entries.ToArray();EditorUtility.SetDirty(book);
            var catalog=Resources.Load<WorldContentCatalog>("WorldContentCatalog");var gathering=Resources.Load<GatheringSettings>("GatheringSettings");
            var resources=new HashSet<GameObject>(catalog.assets.Where(a=>a.prefab!=null&&a.kind!=WorldAssetKind.Grass).Select(a=>a.prefab));
            foreach(var node in Resources.LoadAll<ResourceNodeDefinition>("Gathering"))if(node.availablePrefab!=null)resources.Add(node.availablePrefab);
            foreach(var prefab in resources)BakePalette(prefab);
            AssetDatabase.SaveAssets();
        }
        static void BakePalette(GameObject prefab)
        {
            string path=AssetDatabase.GetAssetPath(prefab);if(!path.EndsWith(".prefab",StringComparison.Ordinal))return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var colors=new Dictionary<Color32,int>();
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
                    foreach(var material in renderer.sharedMaterials)
                    {
                        if(material==null)continue;var texture=material.mainTexture;Texture2D pixels=null;
                        if(texture!=null){var rt=RenderTexture.GetTemporary(256,256,0);Graphics.Blit(texture,rt);var old=RenderTexture.active;RenderTexture.active=rt;pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
                        var uv=mesh.uv;
                        foreach(var coordinate in uv.Take(3000))
                        {Color32 c=pixels!=null?pixels.GetPixelBilinear(coordinate.x,coordinate.y):material.color;if(c.a<128)continue;c.r=(byte)(c.r/24*24);c.g=(byte)(c.g/24*24);c.b=(byte)(c.b/24*24);colors.TryGetValue(c,out int count);colors[c]=count+1;}
                        if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
                    }
                }
                var palette=root.GetComponent<ResourceImpactPalette>()??root.AddComponent<ResourceImpactPalette>();
                palette.colors=colors.OrderByDescending(p=>p.Value).Take(8).Select(p=>(Color)p.Key).ToArray();if(palette.colors.Length==0)palette.colors=new[]{Color.gray};
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void DefaultPose(CreatureSpecies species)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");var player=CompanionPlayer.CloneVisual(source);var animator=player.GetComponentInChildren<Animator>();
            var bones=new List<RiderPose.Bone>();
            var hips=animator.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Hips");
            if(hips!=null)species.riderPose.position=new Vector3(0,1.05f-(hips.position.y-player.transform.position.y),0);
            foreach(string side in new[]{"L","R"})
            {
                float direction=side=="L"?-1:1;
                Orient("UpperLeg."+side,"LowerLeg."+side,new Vector3(direction*.28f,-.12f,.55f));
                Orient("LowerLeg."+side,"Foot."+side,new Vector3(direction*.05f,-.55f,-.12f));
                Orient("UpperArm."+side,"LowerArm."+side,new Vector3(direction*.22f,-.25f,.25f));
                Orient("LowerArm."+side,"Hand."+side,new Vector3(direction*-.05f,-.05f,.4f));
            }
            species.riderPose.bones=bones.ToArray();EditorUtility.SetDirty(species.riderPose);UnityEngine.Object.DestroyImmediate(player);
            void Orient(string name,string childName,Vector3 direction)
            {
                var transforms=animator.GetComponentsInChildren<Transform>();var bone=transforms.FirstOrDefault(t=>t.name==name);var child=transforms.FirstOrDefault(t=>t.name==childName);if(bone==null||child==null)return;
                bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation;
                bones.Add(new RiderPose.Bone{path=AnimationUtility.CalculateTransformPath(bone,animator.transform),rotation=bone.localEulerAngles});
            }
        }
        public static void RunBatch()
        {
            if(!Directory.GetCurrentDirectory().Replace("\\","/").Contains("/.validation/"))throw new InvalidOperationException("Use an isolated project.");
            Create();TranslationTables.Import();
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");var animator=player.GetComponentInChildren<Animator>();
            File.WriteAllText("../../.validation/rider-bones.txt",string.Join("\n",animator.GetComponentsInChildren<Transform>().Select(t=>AnimationUtility.CalculateTransformPath(t,animator.transform))));
            CreatureFeatureChecks.RunBatch();
        }
    }
}
