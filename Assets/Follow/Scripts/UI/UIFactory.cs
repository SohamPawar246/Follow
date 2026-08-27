using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Follow.UI
{
    /// <summary>
    /// Builds the interface from the theme. Every surface is an outlined sticker: a dark
    /// shape behind a lighter fill, with a drawn lip under anything pressable. Two stacked
    /// images per surface is what buys us per-element colour and a crisp outline at once.
    /// </summary>
    public static class UIFactory
    {
        static CozyTheme T => CozyTheme.Active;

        // --- structure ---------------------------------------------------------

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // --- graphics ----------------------------------------------------------

        public static Image Solid(string name, Transform parent, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = T.Flat;
            img.color = color;
            return img;
        }

        public static Image Shape(string name, Transform parent, Sprite sprite, Color color,
            Image.Type type = Image.Type.Sliced)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            return img;
        }

        /// <summary>
        /// An outlined sticker card: dark shape behind, lighter fill inset by the outline
        /// width. Tilt is in degrees and exists so stacked cards never look machine-placed.
        /// </summary>
        public static RectTransform Card(string name, Transform parent, Vector2 size,
            Color? fill = null, float tilt = 0f, bool dropShadow = true)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            if (Mathf.Abs(tilt) > 0.01f) root.localRotation = Quaternion.Euler(0f, 0f, tilt);

            if (dropShadow)
            {
                var shadow = Shape("Shadow", root, T.Card, new Color(0f, 0f, 0f, 0.18f));
                Stretch(shadow.rectTransform);
                shadow.rectTransform.anchoredPosition = new Vector2(0f, -10f);
                shadow.raycastTarget = false;
            }

            var outline = Shape("Outline", root, T.Card, T.outline);
            Stretch(outline.rectTransform);

            var fillImg = Shape("Fill", root, T.Card, fill ?? T.cream);
            Stretch(fillImg.rectTransform, T.outlineWidth);
            fillImg.raycastTarget = false;

            return root;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, int size,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
            bool bold = false, bool handwritten = false)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

            // Baloo is the display face and is unreadable as a paragraph; Nunito carries
            // body text; Patrick Hand is the surveyor's own voice.
            var font = handwritten ? T.handFont
                     : bold ? T.uiFont
                     : (T.bodyFont != null ? T.bodyFont : T.uiFont);
            if (font != null) label.font = font;

            label.text = text;
            label.fontSize = size;
            label.color = color ?? T.ink;
            label.alignment = align;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        // --- buttons -----------------------------------------------------------

        public enum Tone { Primary, Secondary, Quiet, Danger, Leafy }

        public static Color ToneColor(Tone tone) => tone switch
        {
            Tone.Primary => T.honey,
            Tone.Secondary => T.cream,
            Tone.Quiet => T.paperDeep,
            Tone.Danger => T.berry,
            Tone.Leafy => T.leaf,
            _ => T.cream
        };

        /// <summary>
        /// A chunky candy button. A dark base sits a lip below the face; pressing drops the
        /// face onto it, which is the whole tactile trick.
        /// </summary>
        public static CozyButton Button(string name, Transform parent, string text, Action onClick,
            Vector2? size = null, Tone tone = Tone.Secondary, float tilt = 0f)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size ?? new Vector2(420f, 96f);
            if (Mathf.Abs(tilt) > 0.01f) root.localRotation = Quaternion.Euler(0f, 0f, tilt);

            Color fill = ToneColor(tone);

            // The dark base, offset down. Pressing hides it.
            var baseImg = Shape("Base", root, T.Card, T.outline);
            Stretch(baseImg.rectTransform);
            baseImg.rectTransform.anchoredPosition = new Vector2(0f, -T.buttonLip);
            baseImg.raycastTarget = false;

            var face = Rect("Face", root);
            Stretch(face);

            var faceOutline = Shape("Outline", face, T.Card, T.outline);
            Stretch(faceOutline.rectTransform);

            var faceFill = Shape("Fill", face, T.Card, fill);
            Stretch(faceFill.rectTransform, T.outlineWidth);
            faceFill.raycastTarget = false;

            // A thin pill of light near the top edge. A pill, not a card, so it never
            // shows the hard bottom corners that read as a rendering bug.
            var gloss = Shape("Gloss", face, T.Chip, new Color(1f, 1f, 1f, T.glossStrength));
            var gr = gloss.rectTransform;
            gr.anchorMin = new Vector2(0f, 1f);
            gr.anchorMax = new Vector2(1f, 1f);
            gr.pivot = new Vector2(0.5f, 1f);
            gr.offsetMin = new Vector2(T.outlineWidth + 16f, 0f);
            gr.offsetMax = new Vector2(-(T.outlineWidth + 16f), -(T.outlineWidth + 8f));
            gr.sizeDelta = new Vector2(gr.sizeDelta.x, Mathf.Max(10f, root.sizeDelta.y * 0.22f));
            gloss.raycastTarget = false;

            // Text colour follows the fill: dark ink on a light button reads cleanest,
            // cream with a heavy outline is what saturated buttons need.
            float lum = fill.r * 0.299f + fill.g * 0.587f + fill.b * 0.114f;
            bool lightFill = lum > 0.62f;

            var label = Label("Label", face, text, T.buttonSize,
                lightFill ? T.ink : T.cream, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMax = new Vector2(0f, -4f);
            if (lightFill) TextStyles.Soft(label, new Color(0f, 0f, 0f, 0.16f));
            else TextStyles.Chunky(label, T.outline, new Color(0f, 0f, 0f, 0.35f));

            var btn = root.gameObject.AddComponent<CozyButton>();
            btn.Bind(faceFill, face, label, onClick, fill, T.buttonLip);
            return btn;
        }

        /// <summary>A small pill for counters: an icon dot and a number.</summary>
        public static CozyChip Chip(string name, Transform parent, string value, Color dotColor,
            Vector2? size = null)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size ?? new Vector2(150f, 62f);

            var outline = Shape("Outline", root, T.Chip, T.outline);
            Stretch(outline.rectTransform);
            outline.raycastTarget = false;

            var fill = Shape("Fill", root, T.Chip, T.cream);
            Stretch(fill.rectTransform, T.outlineWidth * 0.8f);
            fill.raycastTarget = false;

            var dotOutline = Shape("DotOutline", root, T.Dot, T.outline, Image.Type.Simple);
            Anchor(dotOutline.rectTransform, new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(42f, 42f));
            dotOutline.rectTransform.pivot = new Vector2(0f, 0.5f);
            dotOutline.raycastTarget = false;

            var dot = Shape("Dot", root, T.Dot, dotColor, Image.Type.Simple);
            Anchor(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(32f, 32f));
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);
            dot.raycastTarget = false;

            var label = Label("Value", root, value, T.chipSize, T.ink, TextAlignmentOptions.Center, true);
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(54f, 0f);
            lr.offsetMax = new Vector2(-12f, -2f);

            var chip = root.gameObject.AddComponent<CozyChip>();
            chip.Bind(label, dot);
            return chip;
        }

        /// <summary>
        /// A banner that hangs over the top edge of a card. This single element is what
        /// separates a panel that reads as a game from one that reads as a document, so
        /// it uses Kenney's hand-drawn art where a generated shape would look sterile.
        /// </summary>
        public static RectTransform Banner(string name, Transform parent, string text,
            Vector2 size, Color tint, float tilt = 0f)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            if (Mathf.Abs(tilt) > 0.01f) root.localRotation = Quaternion.Euler(0f, 0f, tilt);

            if (T.HasBanner)
            {
                var art = Shape("Art", root, T.Banner, tint);
                Stretch(art.rectTransform);
                art.raycastTarget = false;
            }
            else
            {
                // Fallback so a missing import never leaves a headless panel.
                var outline = Shape("Outline", root, T.Card, T.outline);
                Stretch(outline.rectTransform);
                var fill = Shape("Fill", root, T.Card, tint);
                Stretch(fill.rectTransform, T.outlineWidth);
                fill.raycastTarget = false;
            }

            var label = Label("Label", root, text, T.headingSize, T.cream,
                TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 24f);
            // The art has a lip at the bottom; nudge the type onto the flat of the cloth.
            label.rectTransform.offsetMax = new Vector2(-24f, -10f);
            TextStyles.Chunky(label, T.outline, new Color(0f, 0f, 0f, 0.4f));

            return root;
        }

        public static CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>A counter pill whose number pops when it changes.</summary>
    public class CozyChip : MonoBehaviour
    {
        TextMeshProUGUI _label;
        Image _dot;
        float _pop;

        public void Bind(TextMeshProUGUI label, Image dot) { _label = label; _dot = dot; }

        public void Set(string value)
        {
            if (_label == null || _label.text == value) return;
            bool first = string.IsNullOrEmpty(_label.text);
            _label.text = value;
            _pop = 1f;
            if (!first) CozySounds.Play(CozySounds.Active?.chipPop, 0.45f);
        }

        void Update()
        {
            if (_pop <= 0f) return;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.unscaledDeltaTime / 0.28f);
            float s = 1f + Mathf.Sin(_pop * Mathf.PI) * 0.16f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// A chunky button that drops onto its own dark base when pressed. This is the single
    /// most important interaction in the game for making the UI feel like objects.
    /// </summary>
    public class CozyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        Image _fill;
        RectTransform _face;
        TextMeshProUGUI _label;
        Action _onClick;

        Color _base;
        float _lip;
        Vector2 _rest;

        bool _down;
        bool _hover;
        float _sink;
        public bool interactable = true;

        public TextMeshProUGUI Label => _label;

        public void Bind(Image fill, RectTransform face, TextMeshProUGUI label, Action onClick,
            Color tint, float lip)
        {
            _fill = fill;
            _face = face;
            _label = label;
            _onClick = onClick;
            _base = tint;
            _lip = lip;
            _rest = face.anchoredPosition;
        }

        void Update()
        {
            if (_fill == null) return;
            float dt = Time.unscaledDeltaTime;

            _sink = Mathf.Lerp(_sink, _down && interactable ? 1f : 0f, 1f - Mathf.Exp(-dt / 0.035f));
            _face.anchoredPosition = _rest + new Vector2(0f, -_lip * _sink);

            Color target = _base;
            if (!interactable) target = Color.Lerp(_base, Color.grey, 0.5f);
            else if (_hover) target = CozyTheme.Active.Lift(_base, 0.22f);
            _fill.color = Color.Lerp(_fill.color, target, 1f - Mathf.Exp(-dt / 0.07f));

            // A small lift on hover reads as the button noticing you.
            float scale = interactable && _hover && !_down ? 1.04f : 1f;
            var s = transform.localScale;
            float next = Mathf.Lerp(s.x, scale, 1f - Mathf.Exp(-dt / 0.08f));
            transform.localScale = new Vector3(next, next, 1f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (interactable) CozySounds.Play(CozySounds.Active?.buttonHover, 0.5f);
        }

        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }

        public void OnPointerDown(PointerEventData e)
        {
            _down = true;
            if (interactable) CozySounds.Play(CozySounds.Active?.buttonPress, 0.8f);
        }

        public void OnPointerUp(PointerEventData e) => _down = false;
        public void OnPointerClick(PointerEventData e) { if (interactable) _onClick?.Invoke(); }
    }
}
