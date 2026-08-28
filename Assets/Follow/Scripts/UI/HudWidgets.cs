using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Follow.Core;
using Follow.Game;

namespace Follow.UI
{
    /// <summary>
    /// The clock, built the way Stardew builds one: a wooden plate, a dial with hour ticks,
    /// an arc that fills as the day burns down, a needle, and the time spelled out beneath.
    ///
    /// The filled arc is the part that matters. A dot sliding along a track tells you where
    /// you are; an arc that visibly empties tells you how much you have left, which is the
    /// only question the player actually asks.
    /// </summary>
    public class SundialWidget : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        RectTransform _needle;
        RectTransform _sun;
        Image _arcFill;
        Image _sunFill;
        TextMeshProUGUI _timeLabel;
        TextMeshProUGUI _dayLabel;

        const float CardW = 306f;
        const float CardH = 252f;
        const float Dial = 178f;

        public static SundialWidget Create(Transform parent)
        {
            var card = UIFactory.Card("Sundial", parent, new Vector2(CardW, CardH),
                CozyTheme.Active.cream, -1.4f);
            var w = card.gameObject.AddComponent<SundialWidget>();
            w.Build(card);
            return w;
        }

        void Build(RectTransform card)
        {
            // The arc sprite is a full square with only its top half drawn, so the dial's
            // centre of rotation is the CENTRE of this rect - not its bottom edge. Anchoring
            // the needle anywhere else sends it off through the rest of the card.
            var dial = UIFactory.Rect("Dial", card);
            UIFactory.Anchor(dial, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(Dial, Dial));
            dial.pivot = new Vector2(0.5f, 1f);

            var arcSprite = Sticker.Arc(256, 22f, 2f, 178f);

            var groove = UIFactory.Shape("Groove", dial, arcSprite, T.outline, Image.Type.Simple);
            UIFactory.Stretch(groove.rectTransform, -5f);
            groove.raycastTarget = false;

            var track = UIFactory.Shape("Track", dial, arcSprite, T.paperDeep, Image.Type.Simple);
            UIFactory.Stretch(track.rectTransform);
            track.raycastTarget = false;

            _arcFill = UIFactory.Shape("ArcFill", dial, arcSprite, T.honey, Image.Type.Filled);
            UIFactory.Stretch(_arcFill.rectTransform);
            _arcFill.fillMethod = Image.FillMethod.Radial180;
            _arcFill.fillOrigin = (int)Image.Origin180.Left;
            _arcFill.fillClockwise = true;
            _arcFill.fillAmount = 0f;
            _arcFill.raycastTarget = false;

            for (int i = 0; i <= 6; i++) Tick(dial, i / 6f, i % 3 == 0);

            _needle = CentrePivot("NeedlePivot", dial);
            var needleShape = UIFactory.Shape("Needle", _needle, T.Chip, T.outline);
            UIFactory.Anchor(needleShape.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(8f, Dial * 0.40f));
            needleShape.rectTransform.pivot = new Vector2(0.5f, 0f);
            needleShape.raycastTarget = false;

            _sun = CentrePivot("SunPivot", dial);
            var sunOutline = UIFactory.Shape("SunOutline", _sun, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Anchor(sunOutline.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, Dial * 0.5f - 11f), new Vector2(40f, 40f));
            sunOutline.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            sunOutline.raycastTarget = false;

            _sunFill = UIFactory.Shape("Sun", sunOutline.rectTransform, T.Dot, T.honey, Image.Type.Simple);
            UIFactory.Stretch(_sunFill.rectTransform, T.outlineWidth * 0.75f);
            _sunFill.raycastTarget = false;

            var hub = UIFactory.Shape("Hub", dial, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Anchor(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            hub.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hub.raycastTarget = false;

            // Everything below sits under the dial's centre line, where the arc has ended.
            var plate = UIFactory.Card("TimePlate", card, new Vector2(CardW - 74f, 58f), T.paper, 0.9f);
            UIFactory.Anchor(plate, new Vector2(0.5f, 1f), new Vector2(0f, -Dial * 0.5f - 18f),
                new Vector2(CardW - 74f, 58f));
            plate.pivot = new Vector2(0.5f, 1f);

            _timeLabel = UIFactory.Label("Time", plate, "6:00 am", 34, T.ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_timeLabel.rectTransform, 6f);
            _timeLabel.rectTransform.offsetMax = new Vector2(-6f, -10f);

            _dayLabel = UIFactory.Label("Day", card, "Day 1", 26, T.inkSoft,
                TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Anchor(_dayLabel.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -Dial * 0.5f - 82f), new Vector2(CardW, 34f));
            _dayLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        /// <summary>A full-size child pinned to the dial centre, so rotation orbits correctly.</summary>
        RectTransform CentrePivot(string name, RectTransform dial)
        {
            var rt = UIFactory.Rect(name, dial);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Dial, Dial);
            return rt;
        }

        void Tick(RectTransform dial, float t, bool major)
        {
            var pivot = CentrePivot("TickPivot", dial);
            pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(90f, -90f, t));

            var mark = UIFactory.Shape("Tick", pivot, T.Chip, T.outline);
            float len = major ? 18f : 11f;
            UIFactory.Anchor(mark.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, Dial * 0.5f - 34f), new Vector2(major ? 7f : 5f, len));
            mark.rectTransform.pivot = new Vector2(0.5f, 0f);
            mark.raycastTarget = false;
        }

        void Update()
        {
            var state = GameState.Instance;
            if (state != null && _dayLabel != null) _dayLabel.text = "Day " + state.day;

            var cycle = DayCycle.Instance;
            if (cycle == null) return;

            float t = cycle.Time01;

            // The arc is daylight remaining, not time elapsed. How much light is left is
            // the only question anyone asks a clock in this game.
            if (_arcFill != null)
                _arcFill.fillAmount = t < cycle.duskAt ? 1f - t / cycle.duskAt : 0f;

            // The needle keeps going after dark and comes up the other side at dawn.
            float angle = Mathf.Lerp(90f, -270f, t);
            if (_needle != null) _needle.localRotation = Quaternion.Euler(0f, 0f, angle);

            // The sun token stays on the visible arc. Letting it follow the needle round
            // put it underneath the plate, on top of the day counter, which read as a bug.
            if (_sun != null)
            {
                float token = Mathf.Lerp(90f, -90f, Mathf.Clamp01(t / Mathf.Max(0.01f, cycle.duskAt)));
                _sun.localRotation = Quaternion.Euler(0f, 0f, token);
            }

            if (_sunFill != null)
                _sunFill.color = cycle.IsDark ? T.sky : cycle.IsDusk ? T.berry : T.honey;
            if (_arcFill != null)
                _arcFill.color = cycle.IsDusk ? T.berry : T.honey;

            if (_timeLabel != null) _timeLabel.text = cycle.ClockText;
        }

        /// <summary>Kept only as the fallback when there is no cycle to ask.</summary>
        public static string ClockText(float t)
        {
            float hours = Mathf.Lerp(6f, 21f, Mathf.Clamp01(t));
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.FloorToInt((hours - h) * 6f) * 10;
            string suffix = h >= 12 ? "pm" : "am";
            int display = h % 12;
            if (display == 0) display = 12;
            return display + ":" + m.ToString("00") + " " + suffix;
        }
    }

    /// <summary>
    /// Labelled vitals. An unlabelled coloured bar is a puzzle; every one here carries an
    /// icon badge, a name and a number so nobody has to guess what is draining.
    /// </summary>
    /// <summary>
    /// The four bars: the dog, you, food and water.
    ///
    /// Each one is a labelled column with an icon badge over it, and the dog's badge is a
    /// button - clicking the paw whistles, because the most natural place to reach for the
    /// dog is the thing on screen that represents the dog. Every change floats a number up
    /// off its own bar, so a gain is felt where the player is already looking rather than
    /// announced in a corner they are not.
    /// </summary>
    public class VitalsWidget : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        class Column
        {
            public RectTransform holder;
            public RectTransform fill;
            public Image fillImage;
            public TextMeshProUGUI value;
            public Color tint;
            public float flash;
        }

        readonly System.Collections.Generic.Dictionary<GameState.Track, Column> _bars
            = new System.Collections.Generic.Dictionary<GameState.Track, Column>();

        const float BarW = 54f;
        const float BarH = 168f;
        const float Gap = 74f;
        public const float Width = Gap * 3f + BarW;

        public static VitalsWidget Create(Transform parent)
        {
            var root = UIFactory.Rect("Vitals", parent);
            root.sizeDelta = new Vector2(Width, BarH + 100f);
            var w = root.gameObject.AddComponent<VitalsWidget>();
            w.Build(root);
            return w;
        }

        void Build(RectTransform root)
        {
            // Right to left, so the pair that is yours sits nearest the clock.
            Bar(root, GameState.Track.Hydration, 0f, T.sky, "WATER", IconKind.Drop);
            Bar(root, GameState.Track.Food, -Gap, T.amber, "FOOD", IconKind.Leaf);
            Bar(root, GameState.Track.Energy, -Gap * 2f, T.leaf, "YOU", IconKind.Person);
            Bar(root, GameState.Track.DogFed, -Gap * 3f, T.honey, "DOG", IconKind.Paw);

            var state = GameState.Instance;
            if (state != null) state.Gained += OnGained;
        }

        void OnDestroy()
        {
            var state = GameState.Instance;
            if (state != null) state.Gained -= OnGained;
        }

        enum IconKind { Person, Paw, Drop, Leaf }

        void Bar(RectTransform parent, GameState.Track track, float x, Color color, string label,
            IconKind icon)
        {
            var column = new Column { tint = color };

            var holder = UIFactory.Rect(label, parent);
            UIFactory.Anchor(holder, new Vector2(1f, 1f), new Vector2(x, -46f), new Vector2(BarW, BarH));
            column.holder = holder;

            var outline = UIFactory.Shape("Outline", holder, T.Chip, T.outline);
            UIFactory.Stretch(outline.rectTransform);
            outline.raycastTarget = false;

            var back = UIFactory.Shape("Back", holder, T.Chip, T.paperDeep);
            UIFactory.Stretch(back.rectTransform, T.outlineWidth * 0.8f);
            back.raycastTarget = false;

            var fill = UIFactory.Shape("Fill", holder, T.Chip, color);
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.offsetMin = new Vector2(T.outlineWidth * 0.8f, T.outlineWidth * 0.8f);
            fr.offsetMax = new Vector2(-T.outlineWidth * 0.8f, -T.outlineWidth * 0.8f);
            fill.raycastTarget = false;
            column.fill = fr;
            column.fillImage = fill;

            var badge = UIFactory.Rect("Badge", holder);
            UIFactory.Anchor(badge, new Vector2(0.5f, 1f), new Vector2(0f, 38f), new Vector2(54f, 54f));
            badge.pivot = new Vector2(0.5f, 0.5f);

            var badgeOutline = UIFactory.Shape("Outline", badge, T.Dot, T.outline, Image.Type.Simple);
            UIFactory.Stretch(badgeOutline.rectTransform);
            var badgeFill = UIFactory.Shape("Fill", badge, T.Dot, T.cream, Image.Type.Simple);
            UIFactory.Stretch(badgeFill.rectTransform, T.outlineWidth * 0.8f);
            badgeFill.raycastTarget = false;

            switch (icon)
            {
                case IconKind.Person: DrawPerson(badge, color); break;
                case IconKind.Paw: DrawPaw(badge, color); break;
                case IconKind.Drop: DrawDrop(badge, color); break;
                default: DrawLeaf(badge, color); break;
            }

            // The dog's badge is the whistle. Nothing else on screen is a better place
            // to put "call the dog" than the picture of the dog.
            if (track == GameState.Track.DogFed)
            {
                var whistle = badge.gameObject.AddComponent<WhistleBadge>();
                whistle.Bind(badgeOutline, badge);
            }

            var caption = UIFactory.Label("Caption", holder, label, 18, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -28f),
                new Vector2(110f, 26f));
            caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            caption.characterSpacing = 3f;
            TextStyles.Chunky(caption, T.outline, new Color(0f, 0f, 0f, 0.4f));

            column.value = UIFactory.Label("Value", holder, "100", 20, T.outline,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(column.value.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f),
                new Vector2(BarW, 28f));
            column.value.rectTransform.pivot = new Vector2(0.5f, 0f);

            _bars[track] = column;
        }

        // --- icons ------------------------------------------------------------

        void DrawPerson(RectTransform badge, Color color)
        {
            var head = UIFactory.Shape("Head", badge, T.Dot, color, Image.Type.Simple);
            UIFactory.Anchor(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                new Vector2(15f, 15f));
            head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            head.raycastTarget = false;

            var body = UIFactory.Shape("Body", badge, T.Chip, color);
            UIFactory.Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f),
                new Vector2(24f, 17f));
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.raycastTarget = false;
        }

        void DrawPaw(RectTransform badge, Color color)
        {
            var pad = UIFactory.Shape("Pad", badge, T.Dot, color, Image.Type.Simple);
            UIFactory.Anchor(pad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -7f),
                new Vector2(22f, 18f));
            pad.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pad.raycastTarget = false;

            float[] xs = { -12f, -4f, 4f, 12f };
            float[] ys = { 5f, 10f, 10f, 5f };
            for (int i = 0; i < 4; i++)
            {
                var toe = UIFactory.Shape("Toe", badge, T.Dot, color, Image.Type.Simple);
                UIFactory.Anchor(toe.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(xs[i], ys[i]),
                    new Vector2(8f, 10f));
                toe.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                toe.raycastTarget = false;
            }
        }

        /// <summary>A round belly under a tapered point: unmistakably a drop at this size.</summary>
        void DrawDrop(RectTransform badge, Color color)
        {
            var belly = UIFactory.Shape("Belly", badge, T.Dot, color, Image.Type.Simple);
            UIFactory.Anchor(belly.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f),
                new Vector2(22f, 22f));
            belly.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            belly.raycastTarget = false;

            var tip = UIFactory.Shape("Tip", badge, Sticker.Triangle(48), color, Image.Type.Simple);
            UIFactory.Anchor(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                new Vector2(17f, 17f));
            tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tip.raycastTarget = false;
        }

        /// <summary>Two leaflets off a stem. Food here is what the forest gives you.</summary>
        void DrawLeaf(RectTransform badge, Color color)
        {
            var stem = UIFactory.Shape("Stem", badge, T.Chip, color);
            UIFactory.Anchor(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -9f),
                new Vector2(6f, 16f));
            stem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            stem.raycastTarget = false;

            for (int i = 0; i < 2; i++)
            {
                var leaf = UIFactory.Shape("Leaf", badge, T.Dot, color, Image.Type.Simple);
                UIFactory.Anchor(leaf.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -9f : 9f, 5f), new Vector2(18f, 13f));
                leaf.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                leaf.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 32f : -32f);
                leaf.raycastTarget = false;
            }
        }

        // --- live values --------------------------------------------------------

        void OnGained(GameState.Track track, int amount)
        {
            if (!_bars.TryGetValue(track, out var column)) return;
            FloatingNumber.Pop(column.holder, amount, amount >= 0 ? column.tint : T.berry);
            column.flash = 1f;
        }

        void Update()
        {
            var state = GameState.Instance;
            if (state == null) return;

            Set(GameState.Track.Energy, state.energy);
            Set(GameState.Track.DogFed, 1f - state.dogHunger);
            Set(GameState.Track.Food, state.nourishment);
            Set(GameState.Track.Hydration, state.hydration);

            // The food bar shows what is in your bag rather than a percentage: rations are
            // the thing you actually decide about.
            if (_bars.TryGetValue(GameState.Track.Food, out var food) && food.value != null)
                food.value.text = state.food.ToString();

            float dt = Time.unscaledDeltaTime;
            foreach (var column in _bars.Values)
            {
                if (column.flash <= 0f) continue;
                column.flash = Mathf.MoveTowards(column.flash, 0f, dt / 0.45f);
                float pulse = Mathf.Sin(column.flash * Mathf.PI);
                column.fillImage.color = Color.Lerp(column.tint, Color.white, pulse * 0.55f);
                column.holder.localScale = Vector3.one * (1f + pulse * 0.06f);
            }
        }

        void Set(GameState.Track track, float value01)
        {
            if (!_bars.TryGetValue(track, out var column)) return;

            value01 = Mathf.Clamp01(value01);
            var s = column.fill.localScale;
            float next = Mathf.Lerp(s.y, value01, 1f - Mathf.Exp(-Time.deltaTime / 0.15f));
            column.fill.localScale = new Vector3(1f, next, 1f);

            if (column.value != null && track != GameState.Track.Food)
                column.value.text = Mathf.RoundToInt(value01 * 100f).ToString();

            // A bar in trouble pulses red, so you notice it without reading it.
            if (column.flash <= 0f)
                column.fillImage.color = value01 < 0.22f
                    ? Color.Lerp(column.tint, T.berry, Mathf.PingPong(Time.time * 1.7f, 1f))
                    : column.tint;
        }
    }

    /// <summary>
    /// The paw badge, which whistles when you click it. Separate from the widget so the
    /// pointer handling does not have to live inside a class that draws four bars.
    /// </summary>
    public class WhistleBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        Image _ring;
        RectTransform _root;
        float _hover, _ping;
        bool _hoverState;

        public void Bind(Image ring, RectTransform root) { _ring = ring; _root = root; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hover = Mathf.Lerp(_hover, _hoverState ? 1f : 0f, 1f - Mathf.Exp(-dt / 0.08f));
            _ping = Mathf.MoveTowards(_ping, 0f, dt / 0.5f);

            if (_root != null)
                _root.localScale = Vector3.one *
                    (1f + _hover * 0.12f + Mathf.Sin(_ping * Mathf.PI) * 0.22f);
            if (_ring != null)
                _ring.color = Color.Lerp(CozyTheme.Active.outline, CozyTheme.Active.honey, _hover);

            // A key as well as the badge. Calling your dog is a verb you use constantly
            // and constantly reaching for the mouse to do it breaks the walk.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame && !UIModal.Any) Blow();
        }

        /// <summary>
        /// Whistle, from either the badge or the key.
        ///
        /// She always answers something now. The old version could return a flat refusal,
        /// and since a fresh run starts below the bond it required, the very first whistle
        /// a player ever blows was guaranteed to do nothing at all.
        /// </summary>
        void Blow()
        {
            _ping = 1f;
            Follow.Game.Soundscape.Instance?.Whistle();

            var dog = Follow.Dog.DogBrain.Instance;
            if (dog == null) return;

            bool pointing = dog.State == Follow.Dog.DogState.Point;
            dog.Whistle();

            GameHud.Instance?.Say(pointing
                ? "she barks back - she is standing over something"
                : dog.DistanceToPlayer > 30f ? "she is on her way" : "she trots over");
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hoverState = true;
            CozySounds.Play(CozySounds.Active?.buttonHover, 0.5f);
        }

        public void OnPointerExit(PointerEventData e) => _hoverState = false;

        public void OnPointerClick(PointerEventData e) => Blow();
    }

    /// <summary>
    /// A number that lifts off a bar and fades. Built on demand and destroyed after; there
    /// are never more than a handful, and pooling them would be machinery for nothing.
    /// </summary>
    public class FloatingNumber : MonoBehaviour
    {
        public static void Pop(RectTransform anchorTo, int amount, Color tint)
        {
            if (anchorTo == null || anchorTo.parent == null) return;

            var label = UIFactory.Label("Pop", anchorTo.parent,
                (amount > 0 ? "+" : "") + amount, 30, tint, TextAlignmentOptions.Center, true);
            var rt = label.rectTransform;
            rt.anchorMin = anchorTo.anchorMin;
            rt.anchorMax = anchorTo.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 44f);
            rt.anchoredPosition = anchorTo.anchoredPosition + new Vector2(0f, 26f);
            TextStyles.Chunky(label, CozyTheme.Active.outline, new Color(0f, 0f, 0f, 0.4f));

            var pop = label.gameObject.AddComponent<FloatingNumber>();
            pop._label = label;
            pop._from = rt.anchoredPosition;
        }

        TextMeshProUGUI _label;
        Vector2 _from;
        float _t;

        void Update()
        {
            _t += Time.unscaledDeltaTime / 1.1f;
            if (_t >= 1f) { Destroy(gameObject); return; }

            var rt = _label.rectTransform;
            // Out and up, easing off, so it reads as weightless rather than launched.
            rt.anchoredPosition = _from + new Vector2(-34f, 62f) * Mathf.Sqrt(_t);

            var c = _label.color;
            c.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, _t));
            _label.color = c;

            float scale = 1f + Mathf.Sin(Mathf.Clamp01(_t * 4f) * Mathf.PI) * 0.25f;
            rt.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// One line in the survey list. It carries its own hover, because a name printed on
    /// paper gives no sign at all that it can be clicked, and the list is the only way to
    /// reach the right-hand page.
    /// </summary>
    public class JournalRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image _plate;
        Image _chevron;
        TMPro.TextMeshProUGUI _label;
        bool _hover;
        float _lit;
        float _rest;

        public void Bind(Image plate, Image chevron, TMPro.TextMeshProUGUI label, bool selected)
        {
            _plate = plate;
            _chevron = chevron;
            _label = label;
            _rest = selected ? 0.35f : 0f;
            Apply(_rest);
        }

        void Update()
        {
            float want = _hover ? 1f : _rest;
            if (Mathf.Abs(_lit - want) < 0.001f) return;
            _lit = Mathf.MoveTowards(_lit, want, Time.unscaledDeltaTime / 0.12f);
            Apply(_lit);
        }

        void Apply(float amount)
        {
            var theme = CozyTheme.Active;
            if (_plate != null)
            {
                var c = theme.paperDeep;
                c.a = amount * 0.75f;
                _plate.color = c;
            }
            if (_chevron != null)
            {
                var c = theme.berry;
                c.a = 0.35f + amount * 0.65f;
                _chevron.color = c;
                _chevron.rectTransform.anchoredPosition = new Vector2(6f + amount * 6f, 0f);
            }
            if (_label != null)
                _label.rectTransform.anchoredPosition = new Vector2(amount * 8f, 0f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            CozySounds.Play(CozySounds.Active?.buttonHover, 0.35f);
        }

        public void OnPointerExit(PointerEventData e) => _hover = false;
    }

    /// <summary>
    /// The closed journal, pinned in a corner. The badge counts what is still outstanding,
    /// and a hint strip names the key that opens it.
    /// </summary>
    public class BookButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        CozyTheme T => CozyTheme.Active;

        System.Action _onClick;
        RectTransform _root;
        TextMeshProUGUI _badgeLabel;
        RectTransform _badge;
        float _hover, _nudge, _restY;
        bool _hoverState;

        public static BookButton Create(Transform parent, System.Action onClick)
        {
            var root = UIFactory.Rect("BookButton", parent);
            root.sizeDelta = new Vector2(160f, 190f);
            var b = root.gameObject.AddComponent<BookButton>();
            b._onClick = onClick;
            b.Build(root);
            return b;
        }

        void Build(RectTransform root)
        {
            _root = root;

            var pages = UIFactory.Shape("Pages", root, T.Card, T.cream);
            UIFactory.Stretch(pages.rectTransform);
            pages.rectTransform.offsetMin = new Vector2(20f, 10f);
            pages.rectTransform.offsetMax = new Vector2(6f, -10f);
            pages.raycastTarget = false;

            var coverOutline = UIFactory.Shape("CoverOutline", root, T.Card, T.outline);
            UIFactory.Stretch(coverOutline.rectTransform);
            coverOutline.rectTransform.offsetMax = new Vector2(-14f, 0f);

            var cover = UIFactory.Shape("Cover", root, T.Card, T.forest);
            UIFactory.Stretch(cover.rectTransform, T.outlineWidth);
            cover.rectTransform.offsetMax = new Vector2(-14f - T.outlineWidth, -T.outlineWidth);
            cover.raycastTarget = false;

            var spine = UIFactory.Shape("Spine", root, T.Card, T.Shade(T.forest, 0.35f));
            UIFactory.Anchor(spine.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f),
                new Vector2(18f, 150f));
            spine.rectTransform.pivot = new Vector2(0f, 0.5f);
            spine.raycastTarget = false;

            var ribbon = UIFactory.Shape("Ribbon", root, T.Flat, T.berry, Image.Type.Simple);
            UIFactory.Anchor(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(14f, 6f),
                new Vector2(20f, 78f));
            ribbon.rectTransform.pivot = new Vector2(0.5f, 1f);
            ribbon.raycastTarget = false;

            var label = UIFactory.Label("Label", root, "Journal", 24, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-6f, -4f),
                new Vector2(130f, 40f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            TextStyles.Chunky(label, T.outline, new Color(0f, 0f, 0f, 0.35f));

            // Name the key. An unlabelled clickable is a guess.
            var hint = UIFactory.Card("Hint", root, new Vector2(76f, 40f), T.paper, -2f);
            UIFactory.Anchor(hint, new Vector2(0.5f, 0f), new Vector2(-4f, 6f), new Vector2(76f, 40f));
            hint.pivot = new Vector2(0.5f, 0.5f);
            var hintLabel = UIFactory.Label("Key", hint, "J", 24, T.ink, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(hintLabel.rectTransform);
            hintLabel.rectTransform.offsetMax = new Vector2(0f, -4f);

            _badge = UIFactory.Rect("Badge", root);
            UIFactory.Anchor(_badge, new Vector2(1f, 1f), new Vector2(8f, 10f), new Vector2(54f, 54f));
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

        public void Nudge() => _nudge = 1f;

        void Start() { _restY = _root.anchoredPosition.y; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hover = Mathf.Lerp(_hover, _hoverState ? 1f : 0f, 1f - Mathf.Exp(-dt / 0.09f));
            _nudge = Mathf.MoveTowards(_nudge, 0f, dt / 1.1f);

            float wobble = Mathf.Sin(_nudge * Mathf.PI * 6f) * 7f * _nudge;
            float lift = _hover * 8f + Mathf.Abs(Mathf.Sin(_nudge * Mathf.PI * 3f)) * 10f * _nudge;

            _root.localRotation = Quaternion.Euler(0f, 0f, -3f + wobble);
            _root.localScale = Vector3.one * (1f + _hover * 0.06f);
            var p = _root.anchoredPosition;
            _root.anchoredPosition = new Vector2(p.x, _restY + lift);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hoverState = true;
            CozySounds.Play(CozySounds.Active?.buttonHover, 0.5f);
        }

        public void OnPointerExit(PointerEventData e) => _hoverState = false;
        public void OnPointerClick(PointerEventData e) => _onClick?.Invoke();
    }
}
