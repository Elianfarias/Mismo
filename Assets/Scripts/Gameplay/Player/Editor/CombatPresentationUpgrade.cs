using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class CombatPresentationUpgrade
    {
        [MenuItem("Mismo/Prototype/Combat/Upgrade Enemy Presentation")]
        public static void Apply()
        {
            const string folder="Assets/Art/Materials/CombatPresentation";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Art/Materials","CombatPresentation");
            var leather=Material(folder,"Leather",new Color(.27f,.17f,.10f));
            var metal=Material(folder,"Steel",new Color(.44f,.49f,.53f));
            var dark=Material(folder,"Dark",new Color(.07f,.08f,.06f));
            foreach(string enemy in new[]{"Goblin","FirstBoss"})
            {
                string path="Assets/Prefabs/Enemies/"+enemy+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform visual=root.transform.Find("Visual");
                    if(visual==null || visual.Find("Combat Trim")!=null)continue;
                    var group=new GameObject("Combat Trim").transform;group.SetParent(visual,false);
                    Part("Belt",group,new Vector3(0,.57f,0),new Vector3(.62f,.13f,.37f),leather);
                    Part("Buckle",group,new Vector3(0,.57f,.20f),new Vector3(.14f,.12f,.06f),metal);
                    foreach(float sign in new[]{-1f,1f})
                    {
                        Part("Shoulder",group,new Vector3(sign*.42f,1.03f,0),new Vector3(.30f,.15f,.32f),enemy=="FirstBoss"?metal:leather);
                        Part("Pupil",group,new Vector3(sign*.17f,1.29f,.30f),new Vector3(.055f,.075f,.025f),dark);
                        var brow=Part("Brow",group,new Vector3(sign*.17f,1.365f,.295f),new Vector3(.19f,.045f,.04f),dark);
                        brow.transform.localRotation=Quaternion.Euler(0,0,sign*12);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("COMBAT_PRESENTATION_ASSETS_OK");
        }
        private static GameObject Part(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=size;
            Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;return part;
        }
        private static Material Material(string folder,string name,Color color)
        {
            string path=folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(material,path);return material;
        }
    }
}
