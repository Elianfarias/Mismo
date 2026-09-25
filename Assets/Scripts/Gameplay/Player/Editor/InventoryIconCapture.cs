using System.IO;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class InventoryIconCapture
    {
        public static Sprite Capture(GameObject source,string path,Vector3 rotation)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var preview=new InventoryPreview())
            {
                preview.Show(source,rotation);preview.Render();
                if(!preview.HasModel)return null;
                var old=RenderTexture.active;
                var texture=new Texture2D(preview.Texture.width,preview.Texture.height,TextureFormat.RGBA32,false);
                Texture2D cropped=null;
                try
                {
                RenderTexture.active=(RenderTexture)preview.Texture;
                texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();
                var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>16){minX=Mathf.Min(x,minX);maxX=Mathf.Max(x,maxX);minY=Mathf.Min(y,minY);maxY=Mathf.Max(y,maxY);}
                if(minX>maxX)return null;
                minX=Mathf.Max(0,minX-4);minY=Mathf.Max(0,minY-4);maxX=Mathf.Min(texture.width-1,maxX+4);maxY=Mathf.Min(texture.height-1,maxY+4);
                cropped=new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false);
                cropped.SetPixels(texture.GetPixels(minX,minY,cropped.width,cropped.height));cropped.Apply();
                File.WriteAllBytes(path,cropped.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                finally
                {
                    RenderTexture.active=old;
                    Object.DestroyImmediate(texture);
                    if(cropped!=null)Object.DestroyImmediate(cropped);
                }
            }
        }
    }
}
