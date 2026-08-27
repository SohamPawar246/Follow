using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Follow.Core;
using Follow.Data;

namespace Follow.UI
{
    /// <summary>
    /// The journal: a two page spread. Today's list runs down the left, and whatever you
    /// select fills the right with the photograph you took and the field entry it unlocked.
    ///
    /// Opening it plays the crossing-off: every species already logged gets struck through
    /// in sequence, top to bottom, so the reward for a good day is a little ceremony rather
    /// than a number going up.
    /// </summary>
    public class JournalBook : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        RectTransform _overlay;
        RectTransform _spread;
        RectTransform _leftPage;
        RectTransform _rightPage;
        RectTransform _rightContent;

        readonly List<Entry> _entries = new List<Entry>();
        readonly HashSet<string> _alreadyStruck = new HashSet<string>();

        SpeciesData _selected;
        Coroutine _openRoutine;

        class Entry
        {
            public SpeciesData species;
            public RectTransform row;
            public TextMeshProUGUI label;
            public RectTransform strokeA;
            public RectTransform strokeB;
            public bool logged;
        }

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public static JournalBook Create(Transform parent)
        {
            var root = UIFactory.Stretch(UIFactory.Rect("JournalBook", parent));
            var book = root.gameObject.AddComponent<JournalBook>();
            book.Build(root);
            return book;
        }

        // --- construction --------------------------------------------------------

        void Build(RectTransform root)
        {
            _overlay = root;

            var dim = UIFactory.Solid("Dim", _overlay, T.scrim);
            UIFactory.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            _spread = UIFactory.Rect("Spread", _overlay);
            UIFactory.Anchor(_spread, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1420f, 760f));
            _spread.pivot = new Vector2(0.5f, 0.5f);

            // Two pages fanned slightly apart, with a dark spine between them.
            _leftPage = UIFactory.Card("LeftPage", _spread, new Vector2(690f, 740f), T.cream, -1.1f);
            UIFactory.Anchor(_leftPage, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(690f, 740f));
            _leftPage.pivot = new Vector2(0f, 0.5f);

            _rightPage = UIFactory.Card("RightPage", _spread, new Vector2(690f, 740f), T.cream, 1.1f);
            UIFactory.Anchor(_rightPage, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(690f, 740f));
            _rightPage.pivot = new Vector2(1f, 0.5f);

            // Content lives in its own container so rebuilding it never destroys the
            // card's outline and fill, which are siblings underneath.
            _rightContent = UIFactory.Stretch(UIFactory.Rect("Content", _rightPage));

            var spine = UIFactory.Shape("Spine", _spread, T.Card, T.forest);
            UIFactory.Anchor(spine.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 770f));
            spine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            spine.raycastTarget = false;
            spine.transform.SetAsFirstSibling();

            var banner = UIFactory.Banner("Header", _spread, "Field Journal",
                new Vector2(420f, 116f), T.berry, -1.2f);
            UIFactory.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0f, 56f), new Vector2(420f, 116f));
            banner.pivot = new Vector2(0.5f, 1f);
            banner.Find("Label").GetComponent<TextMeshProUGUI>().fontSize = 40;

            var close = UIFactory.Button("Close", _spread, "Close", Close,
                new Vector2(220f, 78f), UIFactory.Tone.Quiet, 1.5f);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(300f, -54f), new Vector2(220f, 78f));

            _overlay.gameObject.SetActive(false);
        }

        // --- open / close --------------------------------------------------------

        public void Open()
        {
            _overlay.gameObject.SetActive(true);
            BuildList();
            ShowSpecies(PickDefaultSelection());

            if (_openRoutine != null) StopCoroutine(_openRoutine);
            _openRoutine = StartCoroutine(OpenRoutine());
        }

        public void Close()
        {
            if (_openRoutine != null) StopCoroutine(_openRoutine);
            _overlay.gameObject.SetActive(false);
        }

        IEnumerator OpenRoutine()
        {
            _spread.localScale = new Vector3(0.9f, 0.9f, 1f);
            var group = UIFactory.Group(_spread);
            group.alpha = 0f;

            StartCoroutine(UITween.ScaleTo(_spread, Vector3.one, 0.34f));
            yield return UITween.FadeGroup(group, 1f, 0.22f);

            // Cross off everything logged since the book was last opened, one at a time.
            yield return new WaitForSecondsRealtime(0.18f);
            foreach (var entry in _entries)
            {
                if (!entry.logged) continue;
                bool isNew = !_alreadyStruck.Contains(entry.species.id);
                if (isNew)
                {
                    yield return StrikeThrough(entry, 0.26f);
                    _alreadyStruck.Add(entry.species.id);
                    yield return new WaitForSecondsRealtime(0.14f);
                }
                else
                {
                    SetStruck(entry, 1f);
                }
            }
        }

        /// <summary>Draws two slightly crossed strokes so the crossing-off looks hand-made.</summary>
        IEnumerator StrikeThrough(Entry entry, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                SetStruck(entry, UITween.EaseOut(t));
                yield return null;
            }
            SetStruck(entry, 1f);
        }

        void SetStruck(Entry entry, float amount)
        {
            float w = entry.label.preferredWidth + 18f;
            entry.strokeA.sizeDelta = new Vector2(w * Mathf.Clamp01(amount), 7f);
            entry.strokeB.sizeDelta = new Vector2(w * Mathf.Clamp01(amount * 1.08f), 5f);
            entry.strokeA.gameObject.SetActive(amount > 0.001f);
            entry.strokeB.gameObject.SetActive(amount > 0.3f);

            if (amount > 0.9f)
            {
                entry.label.color = T.inkSoft;
            }
        }

        // --- left page: the list -------------------------------------------------

        void BuildList()
        {
            foreach (var e in _entries) if (e.row != null) Destroy(e.row.gameObject);
            _entries.Clear();

            var library = SpeciesLibrary.Active;
            var state = GameState.Instance;
            if (library == null || state == null) return;

            var heading = _leftPage.Find("Heading");
            if (heading == null)
            {
                var h = UIFactory.Label("Heading", _leftPage, "Today's survey", 34, T.ink,
                    TextAlignmentOptions.Left, true);
                UIFactory.Anchor(h.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -50f),
                    new Vector2(520f, 48f));
            }

            var list = library.BuildSurveyList(state.day, 3, 2, id => state.album.Has(id));
            float y = -132f;

            foreach (var species in list)
            {
                var row = UIFactory.Rect("Row_" + species.id, _leftPage);
                UIFactory.Anchor(row, new Vector2(0f, 1f), new Vector2(56f, y), new Vector2(560f, 62f));

                var label = UIFactory.Label("Name", row, species.commonName, 32, T.ink,
                    TextAlignmentOptions.Left, handwritten: true);
                UIFactory.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(480f, 52f));
                label.rectTransform.pivot = new Vector2(0f, 0.5f);

                var kind = UIFactory.Label("Kind", row,
                    species.kind == SpeciesKind.Flora ? "plant" : "animal", 20, T.inkSoft,
                    TextAlignmentOptions.Right, handwritten: true);
                UIFactory.Anchor(kind.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
                    new Vector2(140f, 40f));
                kind.rectTransform.pivot = new Vector2(1f, 0.5f);

                var strokeA = MakeStroke(row, -1.6f, 2f, T.berry);
                var strokeB = MakeStroke(row, 1.2f, -3f, new Color(T.berry.r, T.berry.g, T.berry.b, 0.7f));

                bool logged = state.album.Has(species.id);

                var entry = new Entry
                {
                    species = species, row = row, label = label,
                    strokeA = strokeA, strokeB = strokeB, logged = logged
                };
                SetStruck(entry, 0f);
                _entries.Add(entry);

                // Clicking a row shows that species on the right page.
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var captured = species;
                btn.onClick.AddListener(() => ShowSpecies(captured));

                y -= 74f;
            }
        }

        RectTransform MakeStroke(RectTransform row, float tilt, float yOffset, Color color)
        {
            var stroke = UIFactory.Shape("Stroke", row, T.Chip, color);
            UIFactory.Anchor(stroke.rectTransform, new Vector2(0f, 0.5f), new Vector2(-6f, yOffset),
                new Vector2(0f, 7f));
            stroke.rectTransform.pivot = new Vector2(0f, 0.5f);
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            stroke.raycastTarget = false;
            return stroke.rectTransform;
        }

        SpeciesData PickDefaultSelection()
        {
            var state = GameState.Instance;
            foreach (var e in _entries)
                if (e.logged && !_alreadyStruck.Contains(e.species.id)) return e.species;
            foreach (var e in _entries)
                if (e.logged) return e.species;
            return _entries.Count > 0 ? _entries[0].species : null;
        }

        // --- right page: the entry -----------------------------------------------

        void ShowSpecies(SpeciesData species)
        {
            _selected = species;

            foreach (Transform child in _rightContent) Destroy(child.gameObject);
            if (species == null) return;

            var state = GameState.Instance;
            var record = state != null ? state.album.Get(species.id) : null;
            bool have = record != null;

            // Photo frame, top of the page.
            var frame = UIFactory.Card("PhotoFrame", _rightContent, new Vector2(560f, 262f),
                have ? T.paper : T.paperDeep, -0.8f);
            UIFactory.Anchor(frame, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(560f, 262f));
            frame.pivot = new Vector2(0.5f, 1f);

            if (have && record.photo != null)
            {
                var photo = UIFactory.Rect("Photo", frame).gameObject.AddComponent<RawImage>();
                photo.texture = record.photo;
                UIFactory.Stretch(photo.rectTransform, 22f);
            }
            else
            {
                var empty = UIFactory.Label("Empty", frame,
                    have ? "photographed" : "not yet photographed", 26, T.inkSoft,
                    TextAlignmentOptions.Center, handwritten: true);
                UIFactory.Stretch(empty.rectTransform, 24f);
            }

            if (have)
            {
                var grade = UIFactory.Chip("Grade", frame,
                    PhotoGrading.Name(record.Grade), T.honey, new Vector2(200f, 54f));
                UIFactory.Anchor(grade.GetComponent<RectTransform>(), new Vector2(1f, 0f),
                    new Vector2(-16f, 14f), new Vector2(200f, 54f));
                grade.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            }

            // Names.
            var common = UIFactory.Label("Common", _rightContent, species.commonName, 40, T.ink,
                TextAlignmentOptions.Left, true);
            UIFactory.Anchor(common.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -322f),
                new Vector2(560f, 52f));

            var sci = UIFactory.Label("Scientific", _rightContent, species.scientificName, 26, T.inkSoft,
                TextAlignmentOptions.Left, handwritten: true);
            sci.fontStyle = FontStyles.Italic;
            UIFactory.Anchor(sci.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -372f),
                new Vector2(560f, 40f));

            float y = -424f;
            y = Field("Habitat", species.habitat, y);
            if (!string.IsNullOrEmpty(species.diet)) y = Field("Diet", species.diet, y);

            // The field note is the surveyor's own voice, so it gets the handwritten face.
            if (have && !string.IsNullOrEmpty(species.fieldNote))
            {
                var note = UIFactory.Label("Note", _rightContent, "“" + species.fieldNote + "”",
                    25, T.ink, TextAlignmentOptions.TopLeft, handwritten: true);
                UIFactory.Anchor(note.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y - 8f),
                    new Vector2(560f, 150f));
            }
        }

        float Field(string title, string body, float y)
        {
            var head = UIFactory.Label(title + "Head", _rightContent, title.ToUpperInvariant(), 18, T.berry,
                TextAlignmentOptions.Left, true);
            head.characterSpacing = 6f;
            UIFactory.Anchor(head.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y),
                new Vector2(560f, 26f));

            var text = UIFactory.Label(title + "Body", _rightContent, body, 24, T.inkSoft,
                TextAlignmentOptions.TopLeft);
            UIFactory.Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y - 28f),
                new Vector2(560f, 64f));

            return y - 92f;
        }
    }
}
