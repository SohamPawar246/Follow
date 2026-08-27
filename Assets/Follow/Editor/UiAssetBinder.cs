using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Follow.UI;

namespace Follow.EditorTools
{
    /// <summary>
    /// Binds the borrowed art, the three fonts, and the sound effects.
    ///
    /// Fonts are picked by exact file rather than by globbing the folder: the Nunito
    /// download alone carries eighteen weights, and generating a TMP atlas for each one
    /// would bloat the project for no benefit.
    /// </summary>
    public static class UiAssetBinder
    {
        const string Adventure = "Assets/kenney_ui-pack-adventure/PNG/Double/";
        const string FontDir = "Assets/Follow/Fonts";
        const string AudioDir = "Assets/Follow/Audio";

        // Display carries the personality, body carries the paragraphs, hand carries the
        // surveyor's own voice. Baloo at body size is unreadable; Nunito as a title is dull.
        static readonly (string path, string role)[] Fonts =
        {
            (FontDir + "/Baloo_2/static/Baloo2-ExtraBold.ttf", "ui"),
            (FontDir + "/Nunito/static/Nunito-SemiBold.ttf",   "body"),
            (FontDir + "/Patrick_Hand/PatrickHand-Regular.ttf", "hand"),
        };

        [MenuItem("Follow/Bind Cozy UI Art", priority = 10)]
        public static void Bind()
        {
            var theme = AssetDatabase.LoadAssetAtPath<CozyTheme>("Assets/Follow/Resources/CozyTheme.asset");
            if (theme == null) { Debug.LogError("CozyTheme not found. Run Follow/Build Everything first."); return; }

            theme.bannerSprite = ImportBanner(Adventure + "banner_hanging.png");
            BindFonts(theme);
            EditorUtility.SetDirty(theme);

            var sounds = BindSounds();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Cozy UI bound."
                      + "\n  banner : " + (theme.bannerSprite != null ? "ok" : "MISSING")
                      + "\n  ui     : " + Name(theme.uiFont)
                      + "\n  body   : " + Name(theme.bodyFont)
                      + "\n  hand   : " + Name(theme.handFont)
                      + "\n  sounds : " + (sounds != null ? CountSounds(sounds) + " clips" : "none"));
        }

        static string Name(TMP_FontAsset f) => f != null ? f.name : "MISSING";

        static int CountSounds(CozySounds s)
        {
            int n = 0;
            if (s.bookOpen) n++;
            if (s.bookClose) n++;
            if (s.scratch) n++;
            if (s.buttonPress) n++;
            if (s.buttonHover) n++;
            if (s.chipPop) n++;
            if (s.shutter) n++;
            n += s.pageFlips != null ? s.pageFlips.Count(c => c != null) : 0;
            n += s.footsteps != null ? s.footsteps.Count(c => c != null) : 0;
            return n;
        }

        static Sprite ImportBanner(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Cozy UI: missing " + path); return null; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(78f, 26f, 78f, 26f);
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- fonts ---------------------------------------------------------------

        static void BindFonts(CozyTheme theme)
        {
            foreach (var (path, role) in Fonts)
            {
                var tmp = MakeFontAsset(path);
                if (tmp == null) { Debug.LogWarning("Font not found: " + path); continue; }
                switch (role)
                {
                    case "ui": theme.uiFont = tmp; break;
                    case "body": theme.bodyFont = tmp; break;
                    case "hand": theme.handFont = tmp; break;
                }
            }
        }

        static TMP_FontAsset MakeFontAsset(string ttfPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) return null;

            string dir = Path.GetDirectoryName(ttfPath).Replace((char)92, '/');
            string assetPath = dir + "/" + Path.GetFileNameWithoutExtension(ttfPath) + " SDF.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var tmp = TMP_FontAsset.CreateFontAsset(font);
            if (tmp == null) return null;
            tmp.name = Path.GetFileNameWithoutExtension(ttfPath) + " SDF";

            AssetDatabase.CreateAsset(tmp, assetPath);
            // Atlas and material are sub-assets and must be stored alongside, or the
            // font renders as blank quads after a domain reload.
            if (tmp.atlasTextures != null && tmp.atlasTextures.Length > 0)
                AssetDatabase.AddObjectToAsset(tmp.atlasTextures[0], tmp);
            if (tmp.material != null) AssetDatabase.AddObjectToAsset(tmp.material, tmp);
            AssetDatabase.SaveAssets();
            return tmp;
        }

        // --- sounds --------------------------------------------------------------

        static CozySounds BindSounds()
        {
            const string path = "Assets/Follow/Resources/CozySounds.asset";
            var sounds = AssetDatabase.LoadAssetAtPath<CozySounds>(path);
            if (sounds == null)
            {
                sounds = ScriptableObject.CreateInstance<CozySounds>();
                AssetDatabase.CreateAsset(sounds, path);
            }

            sounds.bookOpen = Clip("bookOpen");
            sounds.bookClose = Clip("bookClose");
            sounds.pageFlips = new[] { Clip("bookFlip1"), Clip("bookFlip2"), Clip("bookFlip3") }
                .Where(c => c != null).ToArray();
            // A cloth rustle is a better pencil-scratch than anything in the UI pack.
            sounds.scratch = Clip("cloth1") ?? Clip("cloth2");

            sounds.buttonPress = Clip("click1") ?? Clip("switch2") ?? Clip("bookPlace1");
            sounds.buttonHover = Clip("rollover1") ?? Clip("rollover2");
            sounds.chipPop = Clip("click2") ?? Clip("switch1");
            sounds.shutter = Clip("bookPlace2") ?? Clip("doorClose_1");

            sounds.footsteps = Enumerable.Range(0, 10)
                .Select(i => Clip("footstep" + i.ToString("00")))
                .Where(c => c != null).ToArray();

            EditorUtility.SetDirty(sounds);
            CozySounds.Active = sounds;
            return sounds;
        }

        /// <summary>Finds a clip anywhere under the audio folder by exact file name.</summary>
        static AudioClip Clip(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets(fileName + " t:AudioClip", new[] { AudioDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) != fileName) continue;
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }
            return null;
        }
    }
}
