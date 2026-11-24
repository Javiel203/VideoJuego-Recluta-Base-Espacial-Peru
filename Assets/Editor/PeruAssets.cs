using System.IO;
using UnityEditor;
using UnityEngine;

// Guarda como PNG (en Assets/Resources/Peru) la bandera, el emblema y el sprite redondeado de los botones,
// para que las escenas de menú puedan referenciarlos y el juego los cargue con Resources.
public static class PeruAssets
{
    const string Folder = "Assets/Resources/" + PeruTheme.ResourcesFolder;
    const string FlagPath = Folder + "/Flag.png";
    const string EmblemPath = Folder + "/Emblem.png";
    const string RoundedPath = Folder + "/Rounded.png";

    public static void Ensure(bool force = false)
    {
        Directory.CreateDirectory(Folder);
        if (force || !File.Exists(FlagPath))
            Save(FlagPath, PeruTheme.GenerateFlag(300, 200), sprite: false, FilterMode.Point);
        if (force || !File.Exists(EmblemPath))
            Save(EmblemPath, PeruTheme.GenerateEmblem(512), sprite: false, FilterMode.Bilinear);
        if (force || !File.Exists(RoundedPath))
            Save(RoundedPath, PeruTheme.GenerateRounded(64, 22), sprite: true, FilterMode.Bilinear);
        PeruTheme.ClearCache();
    }

    static void Save(string path, Texture2D tex, bool sprite, FilterMode filter)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = !sprite;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = filter;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (sprite)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * PeruTheme.RoundedBorder;
            importer.spritePixelsPerUnit = 100f;
        }
        importer.SaveAndReimport();
    }
}
