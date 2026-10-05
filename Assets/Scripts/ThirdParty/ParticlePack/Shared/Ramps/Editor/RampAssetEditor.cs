using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RampAsset))]
public class RampAssetEditor : Editor
{
    const string OutputFolder = "Assets/Art/Textures/VFX/ParticlePack/Shared/Ramps";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Bake")) Bake();
    }

    void Bake()
    {
        var ramp = (RampAsset)target;
        var texture = new Texture2D(ramp.size, ramp.size, TextureFormat.ARGB32, mipChain: true);
        try
        {
            var pixels = texture.GetPixels();
            for (var x = 0; x < ramp.size; x++)
                for (var y = 0; y < ramp.size; y++)
                    pixels[ramp.up ? y + (ramp.size - x - 1) * ramp.size : x + y * ramp.size] = ramp.gradient.Evaluate(x * 1f / ramp.size);
            texture.SetPixels(pixels);
            texture.Apply();
            EnsureFolder(OutputFolder);
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(ramp));
            string path = OutputFolder + "/" + name + ".png";
            if (!ramp.overwriteExisting) path = AssetDatabase.GenerateUniqueAssetPath(path);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
        }
        finally { DestroyImmediate(texture); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
