using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Follow.Game;

namespace Follow.UI
{
    /// <summary>
    /// The row of arrows that appears in the air over whatever you are pointing the lens at.
    ///
    /// The whole row stays on screen for the whole shot, under one shared timer. Nothing
    /// vanishes while you are still reading it: you take in the order, you play it, and
    /// when the bar runs out the shutter goes with whatever you managed. Scoring is by how
    /// many you got right in order, so a half-remembered sequence makes a half-decent
    /// photograph rather than a ruined one.
    ///
    /// Every press answers itself immediately in colour - a green border for a hit, a red
    /// one for a miss, thrown off as an expanding halo so it registers even out of the
    /// corner of your eye. Without that the row was silent and there was no way to tell
    /// whether the game had heard you at all.
    /// </summary>
    public class ShotSequenceUI : MonoBehaviour
    {
        static CozyTheme T => CozyTheme.Active;

        [Tooltip("Seconds on the clock before the shutter goes, whatever you have managed.")]
        public float baseSeconds = 1.5f;
        public float secondsPerArrow = 0.8f;

        RectTransform _root;
        RectTransform _frame;
        RectTransform _row;
        RectTransform _timerFill;
        Image _timerImage;
        TextMeshProUGUI _name;
        TextMeshProUGUI _tally;
        CanvasGroup _group;
        Camera _camera;

        readonly List<Chip> _chips = new List<Chip>();
        readonly List<Key> _expected = new List<Key>();

        /// <summary>The row that has been drawn, in order. Read by the editor's test probe.</summary>
        public IReadOnlyList<Key> Expected => _expected;

        /// <summary>Which arrow is next, or -1 when no sequence is running.</summary>
        public int Step { get; private set; } = -1;

        enum Verdict { Waiting, Right, Wrong, Missed }

        class Chip
        {
            public RectTransform root;
            public Image ring;      // the halo thrown off by a press
            public Image border;    // the permanent record of how it went
            public Image fill;
            public RectTransform glyph;
            public Image arrow;
            public Key key;
            public Verdict verdict = Verdict.Waiting;
            public float flash;     // 1 at the moment of the press, decaying to 0
            public float pop;
        }

        public static ShotSequenceUI Create(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas("ShotCanvas", 300);
            canvas.transform.SetParent(parent, false);

            var root = UIFactory.Stretch(UIFactory.Rect("Shot", canvas.transform));
            var ui = root.gameObject.AddComponent<ShotSequenceUI>();
            ui.Build(root);
            return ui;
        }

        void Build(RectTransform root)
        {
            _root = root;
            _group = UIFactory.Group(root);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            BuildViewfinder();
            BuildRow();
        }

        /// <summary>Four corner brackets. A closed box reads as a dialog; corners read as a lens.</summary>
        void BuildViewfinder()
        {
            _frame = UIFactory.Rect("Viewfinder", _root);
            _frame.sizeDelta = new Vector2(360f, 260f);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                float sy = i < 2 ? 1f : -1f;
                var corner = new Vector2(0.5f + sx * 0.5f, 0.5f + sy * 0.5f);
                var offset = new Vector2(-sx * 8f, -sy * 8f);

                // A dark plate under each arm; cream brackets alone vanish on a sunlit meadow.
                Bracket("Edge" + i, corner, offset, new Vector2(64f, 16f), T.outline);
                Bracket("EdgeV" + i, corner, offset, new Vector2(16f, 64f), T.outline);
                Bracket("Arm" + i, corner, offset, new Vector2(56f, 8f), T.cream);
                Bracket("Post" + i, corner, offset, new Vector2(8f, 56f), T.cream);
            }

            _name = UIFactory.Label("Subject", _frame, "", 26, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -14f),
                new Vector2(460f, 34f));
            _name.rectTransform.pivot = new Vector2(0.5f, 1f);
            TextStyles.Chunky(_name, T.outline, new Color(0f, 0f, 0f, 0.5f));
        }

        void Bracket(string name, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            var image = UIFactory.Shape(name, _frame, T.Chip, color);
            UIFactory.Anchor(image.rectTransform, corner, offset, size);
            image.rectTransform.pivot = corner;
            image.raycastTarget = false;
        }

        /// <summary>The arrows, and the single clock they all share.</summary>
        void BuildRow()
        {
            _row = UIFactory.Rect("Arrows", _root);
            _row.sizeDelta = new Vector2(660f, 170f);
            _row.anchorMin = _row.anchorMax = _row.pivot = new Vector2(0.5f, 0.5f);

            var track = UIFactory.Rect("Timer", _row);
            UIFactory.Anchor(track, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(430f, 24f));
            track.pivot = new Vector2(0.5f, 0f);

            var outline = UIFactory.Shape("Outline", track, T.Chip, T.outline);
            UIFactory.Stretch(outline.rectTransform);
            outline.raycastTarget = false;

            var back = UIFactory.Shape("Back", track, T.Chip, T.paperDeep);
            UIFactory.Stretch(back.rectTransform, 5f);
            back.raycastTarget = false;

            var fillArea = UIFactory.Stretch(UIFactory.Rect("FillArea", track), 5f);
            _timerImage = UIFactory.Shape("Fill", fillArea, T.Chip, T.leaf);
            _timerFill = _timerImage.rectTransform;
            UIFactory.Stretch(_timerFill);
            _timerFill.pivot = new Vector2(0f, 0.5f);
            _timerImage.raycastTarget = false;

            _tally = UIFactory.Label("Tally", _row, "", 22, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_tally.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f),
                new Vector2(360f, 30f));
            _tally.rectTransform.pivot = new Vector2(0.5f, 1f);
            TextStyles.Chunky(_tally, T.outline, new Color(0f, 0f, 0f, 0.45f));
        }

        // --- the run ---------------------------------------------------------------

        /// <summary>
        /// Plays one sequence and reports how many were fumbled. Unscaled time, so it stays
        /// honest if anything ever slows the game down.
        /// </summary>
        public IEnumerator Run(PhotoSubject subject, int steps, float unusedPerStep,
            Action<int> onFinished)
        {
            _camera = Camera.main;
            Draw(steps);

            _name.text = subject.species != null ? subject.species.commonName : "";
            _tally.text = "press them in order";
            Step = 0;
            SetTimer(1f);

            yield return Fade(1f, 0.16f);

            // A beat to read the row before the clock starts.
            float settle = 0f;
            while (settle < 0.45f)
            {
                float dt = Time.unscaledDeltaTime;
                settle += dt;
                Track(subject);
                Animate(dt, 0);
                yield return null;
            }

            float total = baseSeconds + steps * secondsPerArrow;
            float left = total;
            int cursor = 0;
            int correct = 0;

            while (left > 0f && cursor < steps)
            {
                float dt = Time.unscaledDeltaTime;
                left -= dt;
                SetTimer(left / total);
                Track(subject);
                Animate(dt, cursor);

                var pressed = ReadArrow();
                if (pressed == Key.None) { yield return null; continue; }

                bool right = pressed == _expected[cursor];
                if (right) correct++;

                // The cursor moves either way. A wrong press costs you that arrow, not the
                // rest of the row - stalling on one mistake is how a small slip turns into
                // a ruined photograph, which is not the feeling this is for.
                Land(_chips[cursor], right ? Verdict.Right : Verdict.Wrong);
                cursor++;
                Step = cursor;
                _tally.text = correct + " of " + steps;

                yield return null;
            }

            // Whatever is left when the clock runs out simply never happened.
            for (int i = cursor; i < steps; i++) Land(_chips[i], Verdict.Missed);

            Step = -1;
            _tally.text = correct == steps ? "clean" : correct + " of " + steps;
            SetTimer(0f);

            float hold = 0f;
            while (hold < 0.3f)
            {
                float dt = Time.unscaledDeltaTime;
                hold += dt;
                Animate(dt, -1);
                yield return null;
            }

            yield return Shutter();
            yield return Fade(0f, 0.2f);

            onFinished?.Invoke(steps - correct);
        }

        void Draw(int steps)
        {
            foreach (var chip in _chips) if (chip.root != null) Destroy(chip.root.gameObject);
            _chips.Clear();
            _expected.Clear();

            var options = new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };

            const float spacing = 94f;
            float start = -(steps - 1) * spacing * 0.5f;

            for (int i = 0; i < steps; i++)
            {
                // Never the same key twice running: a double is unreadable at this size.
                Key key;
                do { key = options[UnityEngine.Random.Range(0, options.Length)]; }
                while (i > 0 && key == _expected[i - 1]);
                _expected.Add(key);

                _chips.Add(BuildChip(key, start + i * spacing));
            }
        }

        Chip BuildChip(Key key, float x)
        {
            var chip = new Chip { key = key };

            chip.root = UIFactory.Rect("Arrow", _row);
            UIFactory.Anchor(chip.root, new Vector2(0.5f, 0.5f), new Vector2(x, 30f),
                new Vector2(78f, 78f));

            // The halo. Behind everything and larger than the chip, so a press throws a
            // coloured ring outward from under the key rather than recolouring it in place.
            chip.ring = UIFactory.Shape("Ring", chip.root, T.Card, new Color(1f, 1f, 1f, 0f));
            UIFactory.Stretch(chip.ring.rectTransform, -10f);
            chip.ring.raycastTarget = false;

            chip.border = UIFactory.Shape("Border", chip.root, T.Card, T.outline);
            UIFactory.Stretch(chip.border.rectTransform);
            chip.border.raycastTarget = false;

            chip.fill = UIFactory.Shape("Fill", chip.root, T.Card, T.cream);
            UIFactory.Stretch(chip.fill.rectTransform, T.outlineWidth);
            chip.fill.raycastTarget = false;

            chip.glyph = UIFactory.Rect("Glyph", chip.root);
            UIFactory.Anchor(chip.glyph, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));

            chip.arrow = UIFactory.Shape("Arrow", chip.glyph, Sticker.Triangle(64), T.outline,
                Image.Type.Simple);
            UIFactory.Stretch(chip.arrow.rectTransform);
            chip.arrow.raycastTarget = false;
            chip.glyph.localRotation = Quaternion.Euler(0f, 0f, Degrees(key));

            return chip;
        }

        static float Degrees(Key key) => key switch
        {
            Key.UpArrow => 0f,
            Key.RightArrow => -90f,
            Key.DownArrow => 180f,
            _ => 90f
        };

        /// <summary>
        /// Either hand. WASD answers the same four directions as the arrow keys, and the
        /// chips keep their arrow glyphs - a direction is the thing being asked for, not
        /// a particular key, and nobody should have to move their hand across the board
        /// to take a photograph.
        ///
        /// This was arrows-only for a while and for a real reason: WASD is also how you
        /// walk, so anyone still moving burned the entire row in a quarter of a second.
        /// That is safe now only because the shot takes a movement hold the moment it
        /// starts - the walk keys do nothing but answer the row while it is up. If that
        /// hold is ever removed, this has to go back to arrows.
        /// </summary>
        static Key ReadArrow()
        {
            var kb = Keyboard.current;
            if (kb == null) return Key.None;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return Key.UpArrow;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return Key.DownArrow;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return Key.LeftArrow;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return Key.RightArrow;
            return Key.None;
        }

        // --- presentation -----------------------------------------------------------

        void SetTimer(float t)
        {
            t = Mathf.Clamp01(t);
            if (_timerFill != null) _timerFill.localScale = new Vector3(t, 1f, 1f);
            if (_timerImage != null)
                _timerImage.color = t < 0.25f ? T.berry : t < 0.5f ? T.honey : T.leaf;
        }

        /// <summary>
        /// Marks one arrow answered. Nothing is removed; the row stays readable, and the
        /// border it keeps is the answer: green for right, red for wrong, flat for a key
        /// the clock never gave you a chance to press.
        /// </summary>
        void Land(Chip chip, Verdict verdict)
        {
            if (chip == null || chip.verdict != Verdict.Waiting) return;
            chip.verdict = verdict;
            chip.flash = 1f;
            chip.pop = 1f;

            Color edge = Edge(verdict);
            chip.border.color = edge;
            chip.fill.color = verdict switch
            {
                Verdict.Right => Color.Lerp(T.cream, edge, 0.30f),
                Verdict.Wrong => Color.Lerp(T.cream, edge, 0.28f),
                _ => T.paperDeep
            };
            chip.arrow.color = verdict == Verdict.Missed ? T.inkSoft : T.outline;
            UIFactory.Group(chip.glyph).alpha = verdict == Verdict.Missed ? 0.35f : 1f;

            if (verdict != Verdict.Missed)
                CozySounds.Play(verdict == Verdict.Right
                    ? CozySounds.Active?.chipPop : CozySounds.Active?.buttonPress, 0.85f);
        }

        static Color Edge(Verdict verdict) => verdict switch
        {
            Verdict.Right => new Color32(0x4F, 0xC0, 0x4B, 0xFF),   // unmistakably green
            Verdict.Wrong => new Color32(0xE0, 0x3B, 0x36, 0xFF),   // unmistakably red
            Verdict.Missed => CozyTheme.Active.inkSoft,
            _ => CozyTheme.Active.outline
        };

        /// <summary>
        /// One frame of chip animation.
        ///
        /// Two jobs. The halo of a just-pressed key expands and fades, which is the part
        /// you see without looking at it. And the key you are meant to press next breathes
        /// and carries a warm border, so at any moment exactly one arrow is asking for
        /// something - the row used to be four identical shapes with no cursor at all.
        /// </summary>
        void Animate(float dt, int cursor)
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                var chip = _chips[i];
                if (chip.root == null) continue;

                if (chip.flash > 0f)
                {
                    chip.flash = Mathf.Max(0f, chip.flash - dt / 0.34f);
                    var edge = Edge(chip.verdict);
                    chip.ring.color = new Color(edge.r, edge.g, edge.b, chip.flash * 0.9f);
                    chip.ring.rectTransform.localScale =
                        Vector3.one * Mathf.Lerp(1.34f, 1f, chip.flash);
                }
                else if (chip.ring.color.a > 0f)
                {
                    chip.ring.color = new Color(1f, 1f, 1f, 0f);
                }

                chip.pop = Mathf.Max(0f, chip.pop - dt / 0.22f);
                float landed = chip.verdict == Verdict.Right ? 1.06f
                             : chip.verdict == Verdict.Wrong ? 0.94f
                             : chip.verdict == Verdict.Missed ? 0.9f : 1f;

                bool next = i == cursor && chip.verdict == Verdict.Waiting;
                float breathe = next ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.05f : 1f;

                chip.root.localScale = Vector3.one * (landed * breathe + chip.pop * 0.14f);

                if (chip.verdict == Verdict.Waiting)
                    chip.border.color = next
                        ? Color.Lerp(T.outline, T.honey,
                            0.55f + Mathf.Sin(Time.unscaledTime * 7f) * 0.25f)
                        : T.outline;
            }
        }

        IEnumerator Shutter()
        {
            var flash = UIFactory.Solid("Flash", _root, new Color(1f, 1f, 1f, 0f));
            UIFactory.Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            float t = 0f;
            while (t < 0.24f)
            {
                t += Time.unscaledDeltaTime;
                flash.color = new Color(1f, 1f, 1f, Mathf.Sin(t / 0.24f * Mathf.PI) * 0.75f);
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        /// <summary>Keeps the viewfinder and the arrows over the subject as the camera moves.</summary>
        void Track(PhotoSubject subject)
        {
            if (subject == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var canvas = _root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;

            Vector3 screen = _camera.WorldToScreenPoint(subject.AimPoint);
            var rect = _root.rect;
            var at = new Vector2(screen.x / scale - rect.width * 0.5f,
                                 screen.y / scale - rect.height * 0.5f);

            _frame.anchoredPosition = at;

            // The row lives in a band the rest of the interface does not occupy.
            //
            // Chasing the subject freely kept walking it into something - the counters,
            // the clock, the vitals column, the toast, the prompt, the journal, the
            // tutorial card. Rather than fix each collision as it turns up, the guards
            // below are simply where the other furniture ends, measured at the reference
            // resolution, and the row is clamped inside them. It still follows the
            // subject horizontally, which is what keeps it feeling attached to the thing
            // in the lens; the viewfinder brackets do the rest of that job.
            const float aboveTheFurniture = -150f;   // prompt, toast, journal, tutorial
            const float belowTheCounters = 200f;     // stick and food chips, sundial
            const float clearOfTheColumn = 460f;     // clock and the four vitals bars

            // Measured from the chips rather than the row's own generous rect, so a short
            // row is allowed further into the corners than a long one.
            float halfRow = 60f;
            if (_chips.Count > 0)
                halfRow = (_chips.Count - 1) * 94f * 0.5f + 39f + 16f;

            float halfWide = rect.width * 0.5f;

            // Above the subject, or below it when the subject is already high on screen.
            bool subjectIsHigh = at.y > 0f;
            float y = Mathf.Clamp(subjectIsHigh ? at.y - 215f : at.y + 215f,
                aboveTheFurniture, belowTheCounters);

            float x = Mathf.Clamp(at.x, -halfWide + halfRow,
                Mathf.Min(halfWide - halfRow, clearOfTheColumn));

            _row.anchoredPosition = new Vector2(x, y);
        }

        IEnumerator Fade(float to, float seconds)
        {
            float from = _group.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
