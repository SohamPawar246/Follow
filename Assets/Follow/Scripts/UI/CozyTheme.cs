using TMPro;
using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// The look of the game in one asset.
    ///
    /// Shapes are generated procedurally (see <see cref="Sticker"/>) rather than taken
    /// from an asset pack, because the heavy outline weight IS the art direction here and
    /// stock UI art would make this look like every other jam entry. The palette is
    /// deliberately saturated: muted browns read as a museum placard, not as a game.
    /// </summary>
    [CreateAssetMenu(menuName = "Follow/Cozy Theme", fileName = "CozyTheme")]
    public class CozyTheme : ScriptableObject
    {
        static CozyTheme _active;
        public static CozyTheme Active
        {
            get
            {
                if (_active == null) _active = Resources.Load<CozyTheme>("CozyTheme");
                if (_active == null) _active = CreateInstance<CozyTheme>();
                return _active;
            }
            set { _active = value; }
        }

        // --- palette ------------------------------------------------------------

        [Header("The outline. Everything gets it.")]
        public Color outline = new Color32(0x3A, 0x2A, 0x1C, 0xFF);
        public Color outlineSoft = new Color32(0x3A, 0x2A, 0x1C, 0x55);

        [Header("Paper")]
        public Color cream = new Color32(0xFF, 0xF6, 0xE0, 0xFF);
        public Color paper = new Color32(0xFF, 0xE9, 0xBC, 0xFF);
        public Color paperDeep = new Color32(0xF0, 0xD2, 0x99, 0xFF);

        [Header("Ink")]
        public Color ink = new Color32(0x4A, 0x35, 0x24, 0xFF);
        public Color inkSoft = new Color32(0x8A, 0x6F, 0x52, 0xFF);

        [Header("Accents - saturated on purpose")]
        public Color honey = new Color32(0xFF, 0xBB, 0x3D, 0xFF);
        public Color amber = new Color32(0xF2, 0x91, 0x3D, 0xFF);
        public Color berry = new Color32(0xE0, 0x60, 0x3C, 0xFF);
        public Color leaf = new Color32(0x6F, 0xB2, 0x55, 0xFF);
        public Color forest = new Color32(0x3E, 0x7A, 0x4E, 0xFF);
        public Color sky = new Color32(0xA8, 0xDC, 0xE8, 0xFF);

        [Header("Overlays")]
        public Color scrim = new Color(0.12f, 0.08f, 0.05f, 0.66f);
        public Color fade = new Color(0.07f, 0.05f, 0.04f, 1f);

        // --- shape language -----------------------------------------------------

        [Header("Shape")]
        [Tooltip("Outline thickness in reference pixels. The personality of the whole UI.")]
        public float outlineWidth = 6f;
        public int cornerRadius = 28;
        [Tooltip("Superellipse exponent. Higher flattens the shoulder and reads friendlier.")]
        public float cornerExponent = 4.2f;
        [Tooltip("How tall the drawn 3D lip under a button is.")]
        public float buttonLip = 12f;
        [Tooltip("Degrees of tilt on stacked cards, so nothing looks machine-placed.")]
        public float cardTilt = 1.2f;
        [Tooltip("Strength of the light band across the top of a button.")]
        [Range(0f, 1f)] public float glossStrength = 0.20f;

        // --- type ---------------------------------------------------------------

        [Header("Borrowed art")]
        [Tooltip("Kenney's hanging banner. Hand-drawn charm that is not worth reproducing procedurally.")]
        public Sprite bannerSprite;

        [Header("Type")]
        [Tooltip("Rounded display face. Baloo 2, Fredoka or Quicksand. Falls back to TMP default.")]
        public TMP_FontAsset uiFont;
        [Tooltip("Handwritten face for field notes. Patrick Hand or Caveat.")]
        public TMP_FontAsset handFont;

        public int titleSize = 132;
        public int headingSize = 46;
        public int bodySize = 26;
        public int buttonSize = 36;
        public int noteSize = 28;
        public int chipSize = 26;

        [Header("Feel")]
        public float easeIn = 0.28f;
        public float buttonPress = 9f;

        // --- resolved shapes ----------------------------------------------------

        public Sprite Card => Sticker.Squircle(cornerRadius, cornerExponent);
        public Sprite Chip => Sticker.Pill(48);
        public Sprite Dot => Sticker.Circle(96);
        public Sprite Flat => Sticker.Solid();
        public Sprite Banner => bannerSprite;
        public bool HasBanner => bannerSprite != null;

        /// <summary>A darker version of a fill, for the lip under a button.</summary>
        public Color Shade(Color c, float amount = 0.28f) => Color.Lerp(c, outline, amount);

        /// <summary>A lighter version, for the gloss band across the top.</summary>
        public Color Lift(Color c, float amount = 0.35f) => Color.Lerp(c, Color.white, amount);
    }
}
