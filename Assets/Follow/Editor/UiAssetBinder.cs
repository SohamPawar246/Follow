using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Follow.UI;

namespace Follow.EditorTools
{
    /// <summary>
    /// Binds the small amount of borrowed art we still use, plus fonts.
    ///
    /// Shapes are generated in code (see Sticker) because the outline weight is the art
    /// direction. The one exception is Kenney's hanging banner: it is hand-drawn, it is
    /// the element that makes a panel look like a game rather than a document, and it is
    /// not worth reproducing procedurally.
    /// </summary>
    public static class UiAssetBinder
    {
        const string Adventure = "Assets/kenney_ui-pack-adventure/PNG/Double/";
        const string FontDir = "Assets/Follow/Fonts";

        [MenuItem("Follow/Bind Cozy UI Art", priority = 10)]
        public static void Bind()
        {
            var theme = AssetDatabase.LoadAssetAtPath<CozyTheme>("Assets/Follow/Resources/CozyTheme.asset");
            if (theme == null) { Debug.LogError("CozyTheme not found. Run Follow/Build Everything first."); return; }

            theme.bannerSprite = ImportBanner(Adventure + "banner_hanging.png");
            BindFonts(theme);

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Cozy UI: banner=" + (theme.bannerSprite != null ? "ok" : "MISSING")
                      + "  uiFont=" + (theme.uiFont != null ? theme.uiFont.name : "TMP default")
                      + "  handFont=" + (theme.handFont != null ? theme.handFont.name : "none"));
        }

        static Sprite ImportBanner(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Cozy UI: missing " + path); return null; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Wide ends stay intact, the middle stretches to whatever the title needs.
            importer.spriteBorder = new Vector4(78f, 26f, 78f, 26f);
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Turns any TTF dropped into Assets/Follow/Fonts into a TMP font asset.</summary>
        static void BindFonts(CozyTheme theme)
        {
            if (!AssetDatabase.IsValidFolder(FontDir))
            {
                AssetDatabase.CreateFolder("Assets/Follow", "Fonts");
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null) continue;

                string assetPath = FontDir + "/" + Path.GetFileNameWithoutExtension(path) + " SDF.asset";
                var tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (tmp == null)
                {
                    tmp = TMP_FontAsset.CreateFontAsset(font);
                    if (tmp == null) continue;
                    tmp.name = Path.GetFileNameWithoutExtension(path) + " SDF";
                    AssetDatabase.CreateAsset(tmp, assetPath);
                    if (tmp.atlasTextures != null && tmp.atlasTextures.Length > 0)
                        AssetDatabase.AddObjectToAsset(tmp.atlasTextures[0], tmp);
                    if (tmp.material != null) AssetDatabase.AddObjectToAsset(tmp.material, tmp);
                    AssetDatabase.SaveAssets();
                }

                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool handwritten = lower.Contains("patrick") || lower.Contains("caveat")
                                   || lower.Contains("hand") || lower.Contains("script");
                if (handwritten) theme.handFont = tmp;
                else if (theme.uiFont == null) theme.uiFont = tmp;
            }
        }
    }
}
