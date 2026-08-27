using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Follow.Core;
using Follow.Game;

namespace Follow.UI
{
    /// <summary>
    /// The clock, in the Stardew register: a sun that walks an arc across the top of a
    /// wooden card, the day underneath, and the time spelled out. Reading light off an
    /// arc is instant in a way a horizontal bar never is - you see how much day is left
    /// by how far round the sun has gone.
    /// </summary>
    public class SundialWidget : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        RectTransform _sunPivot;
        RectTransform _arcArea;
        Image _sunFill;
        TextMeshProUGUI _timeLabel;
        TextMeshProUGUI _dayLabel;

        const float ArcSize = 210f;
        const float DawnDeg = 176f;
        const float DuskDeg = 4f;

        public static SundialWidget Create(Transform parent)
        {
            var card = UIFactory.Card("Sundial", parent, new Vector2(290f, 210f),
                CozyTheme.Active.cream, -1.6f);
            var w = card.gameObject.AddComponent<SundialWidget>();
            w.Build(card);
            return w;
        }

        void Build(RectTransform card)
        {
            // The track the sun walks. Drawn as a ring segment so it reads as a sky path.
            _arcArea = UIFactory.Rect("ArcArea", card);
            UIFactory.Anchor(_arcArea, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(ArcSize, ArcSize));
            _arcArea.pivot = new Vector2(0.5f, 1f);

            var arcSprite = Sticker.Arc(256, 16f, DuskDeg, DawnDeg);

            var arcOutline = UIFactory.Shape("ArcOutline", _arcArea, arcSprite, T.outline, Image.Type.Simple);
            UIFactory.Stretch(arcOutline.rectTransform, -3f);
            arcOutline.raycastTarget = false;

            var arc = UIFactory.Shape("Arc", _arcArea, arcSprite, T.sky, Image.Type.Simple);
            UIFactory.Stretch(arc.rectTransform);
            arc.raycastTarget = false;

            // Dawn and dusk pegs, so the ends of the day are marked.
            Peg(-ArcSize * 0.5f + 8f);
            Peg(ArcSize * 0.5f - 8f);

            // The sun rides on a pivot rotated about the arc centre.
            _sunPivot = UIFactory.Rect("SunPivot", _arcArea);
            _sunPivot.anchorMin = _sunPivot.anchorMax = new Vector2(0.5f, 0.5f);
            _sunPivot.pivot = new Vector2(0.5f, 0.5f);
            _sunPivot.anchoredPosition = Vector2.zero;
            _sunPivot.sizeDelta = new Vector2(ArcSize, ArcSize);

            var sunOutline = UIFactory.Shape("SunOutline", _sunPivot, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Anchor(sunOutline.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, ArcSize * 0.5f - 8f), new Vector2(46f, 46f));
            sunOutline.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            sunOutline.raycastTarget = false;

            _sunFill = UIFactory.Shape("Sun", sunOutline.rectTransform, T.Dot, T.honey, Image.Type.Simple);
            UIFactory.Stretch(_sunFill.rectTransform, T.outlineWidth * 0.8f);
            _sunFill.raycastTarget = false;

            _timeLabel = UIFactory.Label("Time", card, "6:00 am", 34, T.ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_timeLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 58f),
                new Vector2(240f, 44f));
            TextStyles.Soft(_timeLabel, new Color(0f, 0f, 0f, 0.18f));

            _dayLabel = UIFactory.Label("Day", card, "Day 1", 26, T.inkSoft,
                TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Anchor(_dayLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(240f, 36f));
        }

        void Peg(float x)
        {
            var peg = UIFactory.Shape("Peg", _arcArea, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Anchor(peg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(16f, 16f));
            peg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            peg.raycastTarget = false;
        }

        void Update()
        {
            var state = GameState.Instance;
            if (state != null && _dayLabel != null) _dayLabel.text = "Day " + state.day;

            var cycle = DayCycle.Instance;
            if (cycle == null) return;

            // Rotate the pivot so the sun rides the arc from dawn peg to dusk peg.
            float angle = Mathf.Lerp(-90f, 90f, cycle.Time01);
            _sunPivot.localRotation = Quaternion.Euler(0f, 0f, -angle);

            if (_sunFill != null)
                _sunFill.color = cycle.IsDark ? T.sky : cycle.IsDusk ? T.berry : T.honey;

            if (_timeLabel != null) _timeLabel.text = ClockText(cycle.Time01);
        }

        /// <summary>Maps the normalised day onto a readable 6am-9pm clock.</summary>
        public static string ClockText(float t)
        {
            float hours = Mathf.Lerp(6f, 21f, Mathf.Clamp01(t));
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.FloorToInt((hours - h) * 60f / 10f) * 10;
            string suffix = h >= 12 ? "pm" : "am";
            int display = h % 12;
            if (display == 0) display = 12;
            return display + ":" + m.ToString("00") + " " + suffix;
        }
    }

    /// <summary>
    /// Vertical bars for the two things the player can actually spend and refill.
    /// The dog's bond is deliberately not here - that is read off the campfire.
    /// </summary>
    public class VitalsWidget : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        RectTransform _energyFill;
        RectTransform _dogFill;
        Image _energyImg;

        const float BarW = 54f;
        const float BarH = 260f;

        public static VitalsWidget Create(Transform parent)
        {
            var root = UIFactory.Rect("Vitals", parent);
            root.sizeDelta = new Vector2(140f, BarH);
            var w = root.gameObject.AddComponent<VitalsWidget>();
            w.Build(root);
            return w;
        }

        void Build(RectTransform root)
        {
            _energyFill = Bar(root, "Energy", 0f, T.leaf, out _energyImg);
            _dogFill = Bar(root, "DogFed", -68f, T.amber, out _);
        }

        RectTransform Bar(RectTransform parent, string name, float x, Color color, out Image fillImage)
        {
            var holder = UIFactory.Rect(name, parent);
            UIFactory.Anchor(holder, new Vector2(1f, 1f), new Vector2(x, 0f), new Vector2(BarW, BarH));

            var outline = UIFactory.Shape("Outline", holder, T.Chip, T.outline);
            UIFactory.Stretch(outline.rectTransform);
            outline.raycastTarget = false;

            var back = UIFactory.Shape("Back", holder, T.Chip, T.paperDeep);
            UIFactory.Stretch(back.rectTransform, T.outlineWidth * 0.8f);
            back.raycastTarget = false;

            // The fill grows from the bottom, so a draining bar reads instantly.
            var fill = UIFactory.Shape("Fill", holder, T.Chip, color);
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.offsetMin = new Vector2(T.outlineWidth * 0.8f, T.outlineWidth * 0.8f);
            fr.offsetMax = new Vector2(-T.outlineWidth * 0.8f, -T.outlineWidth * 0.8f);
            fill.raycastTarget = false;
            fillImage = fill;

            return fr;
        }

        void Update()
        {
            var state = GameState.Instance;
            if (state == null) return;

            SetFill(_energyFill, state.energy);
            SetFill(_dogFill, 1f - state.dogHunger);

            // Energy flashes toward berry when it is nearly gone.
            if (_energyImg != null)
                _energyImg.color = state.energy < 0.25f
                    ? Color.Lerp(T.leaf, T.berry, Mathf.PingPong(Time.time * 1.6f, 1f))
                    : T.leaf;
        }

        void SetFill(RectTransform fill, float value01)
        {
            if (fill == null) return;
            float target = Mathf.Clamp01(value01);
            var s = fill.localScale;
            float next = Mathf.Lerp(s.y, target, 1f - Mathf.Exp(-Time.deltaTime / 0.15f));
            fill.localScale = new Vector3(1f, next, 1f);
        }
    }

    /// <summary>
    /// The closed journal, pinned in a corner. Clicking it opens the book; the badge shows
    /// how many of today's targets are still waiting to be crossed off.
    /// </summary>
    public class BookButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        CozyTheme T => CozyTheme.Active;

        System.Action _onClick;
        RectTransform _root;
        TextMeshProUGUI _badgeLabel;
        RectTransform _badge;
        float _hover;
        float _nudge;

        public static BookButton Create(Transform parent, System.Action onClick)
        {
            var root = UIFactory.Rect("BookButton", parent);
            root.sizeDelta = new Vector2(150f, 180f);
            var b = root.gameObject.AddComponent<BookButton>();
            b._onClick = onClick;
            b.Build(root);
            return b;
        }

        void Build(RectTransform root)
        {
            _root = root;

            // Pages first, peeking out to the right of the cover.
            var pages = UIFactory.Shape("Pages", root, T.Card, T.cream);
            UIFactory.Stretch(pages.rectTransform);
            pages.rectTransform.offsetMin = new Vector2(18f, 10f);
            pages.rectTransform.offsetMax = new Vector2(6f, -10f);
            pages.raycastTarget = false;

            var coverOutline = UIFactory.Shape("CoverOutline", root, T.Card, T.outline);
            UIFactory.Stretch(coverOutline.rectTransform);
            coverOutline.rectTransform.offsetMax = new Vector2(-14f, 0f);

            var cover = UIFactory.Shape("Cover", root, T.Card, T.forest);
            UIFactory.Stretch(cover.rectTransform, T.outlineWidth);
            cover.rectTransform.offsetMax = new Vector2(-14f - T.outlineWidth, -T.outlineWidth);
            cover.raycastTarget = false;

            // A spine strip and a ribbon read as "book" instantly at this size.
            var spine = UIFactory.Shape("Spine", root, T.Card, T.Shade(T.forest, 0.35f));
            UIFactory.Anchor(spine.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f),
                new Vector2(18f, 140f));
            spine.rectTransform.pivot = new Vector2(0f, 0.5f);
            spine.raycastTarget = false;

            var ribbon = UIFactory.Shape("Ribbon", root, T.Flat, T.berry, Image.Type.Simple);
            UIFactory.Anchor(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(14f, 6f),
                new Vector2(20f, 74f));
            ribbon.rectTransform.pivot = new Vector2(0.5f, 1f);
            ribbon.raycastTarget = false;

            var label = UIFactory.Label("Label", root, "Journal", 22, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-6f, -10f),
                new Vector2(120f, 40f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            TextStyles.Chunky(label, T.outline, new Color(0f, 0f, 0f, 0.35f));

            _badge = UIFactory.Rect("Badge", root);
            UIFactory.Anchor(_badge, new Vector2(1f, 1f), new Vector2(6f, 8f), new Vector2(52f, 52f));
            _badge.pivot = new Vector2(1f, 1f);

            var badgeOutline = UIFactory.Shape("Outline", _badge, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Stretch(badgeOutline.rectTransform);
            badgeOutline.raycastTarget = false;
            var badgeFill = UIFactory.Shape("Fill", _badge, T.Dot, T.berry, Image.Type.Simple);
            UIFactory.Stretch(badgeFill.rectTransform, T.outlineWidth * 0.8f);
            badgeFill.raycastTarget = false;

            _badgeLabel = UIFactory.Label("Count", _badge, "0", 26, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_badgeLabel.rectTransform);
            _badgeLabel.rectTransform.offsetMax = new Vector2(0f, -3f);
        }

        public void SetRemaining(int remaining)
        {
            if (_badgeLabel == null) return;
            _badgeLabel.text = remaining.ToString();
            _badge.gameObject.SetActive(remaining > 0);
        }

        /// <summary>Called when a species is logged, so the book visibly asks to be opened.</summary>
        public void Nudge() => _nudge = 1f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hover = Mathf.Lerp(_hover, gameObject.activeInHierarchy && _hoverState ? 1f : 0f,
                1f - Mathf.Exp(-dt / 0.09f));
            _nudge = Mathf.MoveTowards(_nudge, 0f, dt / 1.1f);

            float wobble = Mathf.Sin(_nudge * Mathf.PI * 6f) * 7f * _nudge;
            float lift = _hover * 8f + Mathf.Abs(Mathf.Sin(_nudge * Mathf.PI * 3f)) * 10f * _nudge;

            _root.localRotation = Quaternion.Euler(0f, 0f, -3f + wobble);
            _root.localScale = Vector3.one * (1f + _hover * 0.06f);
            var p = _root.anchoredPosition;
            _root.anchoredPosition = new Vector2(p.x, _restY + lift);
        }

        float _restY;
        bool _hoverState;

        void Start() { _restY = _root.anchoredPosition.y; }

        public void OnPointerEnter(PointerEventData e) => _hoverState = true;
        public void OnPointerExit(PointerEventData e) => _hoverState = false;
        public void OnPointerClick(PointerEventData e) => _onClick?.Invoke();
    }
}
