using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// Gives text a heavy dark outline and a soft drop shadow. This is the single biggest
    /// difference between type that reads as cartoon and type that reads as a form field,
    /// and it works even on a plain default font while a nicer face is still being sourced.
    ///
    /// Materials are cached per style so a screen full of labels does not create a screen
    /// full of material instances.
    /// </summary>
    public static class TextStyles
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void Apply(TextMeshProUGUI label, Color outline, float outlineWidth,
            Color shadow, Vector2 shadowOffset, float shadowSoftness = 0.35f)
        {
            if (label == null || label.font == null) return;

            string key = label.font.GetInstanceID() + "|"
                         + ColorUtility.ToHtmlStringRGBA(outline) + "|" + outlineWidth.ToString("0.000") + "|"
                         + ColorUtility.ToHtmlStringRGBA(shadow) + "|" + shadowOffset + "|" + shadowSoftness;

            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                label.fontSharedMaterial = cached;
                return;
            }

            var mat = new Material(label.font.material)
            {
                name = "TMP_Sticker_" + Cache.Count,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (outlineWidth > 0f)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, outline);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            }

            if (shadow.a > 0f)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, shadow);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset.x);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, shadowOffset.y);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            }

            Cache[key] = mat;
            label.fontSharedMaterial = mat;
        }

        /// <summary>Big display type: fat outline, hard shadow below.</summary>
        public static void Display(TextMeshProUGUI label, Color outline, Color shadow)
            => Apply(label, outline, 0.28f, shadow, new Vector2(0.55f, -0.55f), 0.15f);

        /// <summary>Buttons and headings: outlined, snappy.</summary>
        public static void Chunky(TextMeshProUGUI label, Color outline, Color shadow)
            => Apply(label, outline, 0.20f, shadow, new Vector2(0.35f, -0.35f), 0.2f);

        /// <summary>Body text on light panels: no outline, just enough lift to feel drawn.</summary>
        public static void Soft(TextMeshProUGUI label, Color shadow)
            => Apply(label, Color.clear, 0f, shadow, new Vector2(0.2f, -0.2f), 0.5f);
    }
}
