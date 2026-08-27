using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Follow.Core;

namespace Follow.UI
{
    /// <summary>
    /// The options card, built once and used by both the main menu and the pause menu.
    ///
    /// Two copies of a settings screen always drift apart, and the version you reach from
    /// inside the game is the one that matters - it is where you go when the camera feels
    /// wrong, and being sent back to the title screen to fix it would be absurd.
    /// </summary>
    public static class SettingsPanel
    {
        static CozyTheme T => CozyTheme.Active;

        /// <summary>
        /// Builds a full-screen dimmed layer with the options card on it, starting hidden.
        /// Returns the layer so the caller can show and hide it.
        /// </summary>
        public static RectTransform Build(RectTransform parent, Action onBack)
        {
            GameSettings.Load();

            var layer = UIFactory.Stretch(UIFactory.Rect("OptionsLayer", parent));

            var dim = UIFactory.Solid("Dim", layer, T.scrim);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Card("Panel", layer, new Vector2(780f, 640f), T.cream, -0.8f);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var tab = UIFactory.Card("Tab", panel, new Vector2(320f, 84f), T.honey, 2f);
            UIFactory.Anchor(tab, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(320f, 84f));
            var heading = UIFactory.Label("Heading", tab, "Options", T.headingSize - 6, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(heading.rectTransform);
            TextStyles.Chunky(heading, T.outline, new Color(0f, 0f, 0f, 0.35f));

            float y = -140f;
            Slider(panel, "Sound", y, GameSettings.Volume, v => GameSettings.Volume = v);
            y -= 104f;
            Slider(panel, "Camera", y, GameSettings.Zoom01, v => GameSettings.Zoom01 = v,
                "close", "far");
            y -= 112f;
            Check(panel, "Fullscreen", y, GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
            y -= 88f;
            Check(panel, "Reduced motion", y, GameSettings.ReducedMotion,
                v => GameSettings.ReducedMotion = v);

            var back = UIFactory.Button("Back", panel, "Back", onBack,
                new Vector2(320f, 88f), UIFactory.Tone.Primary);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 46f), new Vector2(320f, 88f));

            layer.gameObject.SetActive(false);
            return layer;
        }

        /// <summary>Plays the card's entrance. Skipped entirely when motion is reduced.</summary>
        public static void Show(MonoBehaviour host, RectTransform layer)
        {
            layer.gameObject.SetActive(true);
            var panel = (RectTransform)layer.Find("Panel");
            if (panel == null) return;

            if (GameSettings.ReducedMotion)
            {
                UIFactory.Group(panel).alpha = 1f;
                return;
            }
            host.StartCoroutine(UITween.RiseIn(panel, UIFactory.Group(panel), T.easeIn, 40f));
        }

        // --- rows --------------------------------------------------------------------

        static void Slider(RectTransform parent, string label, float y, float value,
            UnityEngine.Events.UnityAction<float> onChange,
            string lowCaption = null, string highCaption = null)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 64f));
            row.pivot = new Vector2(0.5f, 1f);

            var text = UIFactory.Label("Label", row, label, T.bodySize, T.ink, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.34f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            var track = UIFactory.Rect("Slider", row);
            track.anchorMin = new Vector2(0.34f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(0f, -20f);
            track.offsetMax = new Vector2(0f, 20f);

            var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();

            var outline = UIFactory.Shape("Outline", track, T.Chip, T.outline);
            UIFactory.Stretch(outline.rectTransform);
            var back = UIFactory.Shape("Track", track, T.Chip, T.paperDeep);
            UIFactory.Stretch(back.rectTransform, T.outlineWidth * 0.7f);
            back.raycastTarget = false;

            var fillArea = UIFactory.Stretch(UIFactory.Rect("FillArea", track), T.outlineWidth * 0.7f);
            var fill = UIFactory.Shape("Fill", fillArea, T.Chip, T.honey);
            UIFactory.Stretch(fill.rectTransform);
            fill.raycastTarget = false;

            var handleArea = UIFactory.Stretch(UIFactory.Rect("HandleArea", track), 4f);
            var handleOutline = UIFactory.Shape("HandleOutline", handleArea, T.Dot, T.outline, Image.Type.Simple);
            handleOutline.rectTransform.sizeDelta = new Vector2(52f, 52f);
            var handle = UIFactory.Shape("Handle", handleOutline.rectTransform, T.Dot, T.cream, Image.Type.Simple);
            UIFactory.Stretch(handle.rectTransform, T.outlineWidth * 0.8f);
            handle.raycastTarget = false;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleOutline.rectTransform;
            slider.targetGraphic = handleOutline;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChange);

            // Ends labelled in the surveyor's hand, so a bare bar is never a guess.
            if (lowCaption == null && highCaption == null) return;
            Caption(track, lowCaption, new Vector2(0f, 0f), TextAlignmentOptions.Left);
            Caption(track, highCaption, new Vector2(1f, 0f), TextAlignmentOptions.Right);
        }

        static void Caption(RectTransform parent, string text, Vector2 anchor,
            TextAlignmentOptions align)
        {
            if (string.IsNullOrEmpty(text)) return;
            var label = UIFactory.Label("Caption", parent, text, 20, T.inkSoft, align, handwritten: true);
            UIFactory.Anchor(label.rectTransform, anchor, new Vector2(0f, -6f), new Vector2(120f, 28f));
            label.rectTransform.pivot = new Vector2(anchor.x, 1f);
        }

        static void Check(RectTransform parent, string label, float y, bool value,
            UnityEngine.Events.UnityAction<bool> onChange)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 62f));
            row.pivot = new Vector2(0.5f, 1f);

            var text = UIFactory.Label("Label", row, label, T.bodySize, T.ink, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            var box = UIFactory.Rect("Box", row);
            UIFactory.Anchor(box, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            box.pivot = new Vector2(1f, 0.5f);

            var toggle = box.gameObject.AddComponent<Toggle>();
            var outline = UIFactory.Shape("Outline", box, T.Card, T.outline);
            UIFactory.Stretch(outline.rectTransform);
            var back = UIFactory.Shape("Back", box, T.Card, T.paperDeep);
            UIFactory.Stretch(back.rectTransform, T.outlineWidth * 0.8f);
            back.raycastTarget = false;
            var check = UIFactory.Shape("Check", box, T.Card, T.leaf);
            UIFactory.Stretch(check.rectTransform, T.outlineWidth * 0.8f + 8f);
            check.raycastTarget = false;

            toggle.targetGraphic = outline;
            toggle.graphic = check;
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(onChange);
        }
    }
}
