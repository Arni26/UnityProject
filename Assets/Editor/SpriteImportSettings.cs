using UnityEditor;
using UnityEngine;

public class SpriteImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        var ti = (TextureImporter)assetImporter;
        if (path.StartsWith("Assets/Resources/Sprites/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 68f;
            ti.mipmapEnabled = false;
        }
        else if (path.StartsWith("Assets/Resources/Story/"))
        {
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
        }
    }
}
