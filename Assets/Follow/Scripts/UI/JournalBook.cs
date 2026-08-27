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
        Coroutine _flipRoutine;

        readonly List<SpeciesData> _catalogue = new List<SpeciesData>();
        int _page;
        TextMeshProUGUI _pageLabel;
        CozyButton _prev, _next, _discard;
        bool _discardArmed;

        const int RowsPerPage = 6;

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
                new Vector2(220f, 74f), UIFactory.Tone.Quiet, 1.5f);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, -60f), new Vector2(220f, 74f));
            close.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);

            BuildPager();

            _overlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// The two corners you turn pages with. The album outgrew one page on about the
        /// second day, and a list you cannot scroll past is a list that is lying to you.
        /// </summary>
        void BuildPager()
        {
            _prev = UIFactory.Button("Prev", _leftPage, "Back", () => TurnTo(_page - 1),
                new Vector2(150f, 68f), UIFactory.Tone.Primary, -2f);
            UIFactory.Anchor(_prev.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(56f, 34f), new Vector2(150f, 68f));

            _next = UIFactory.Button("Next", _leftPage, "More", () => TurnTo(_page + 1),
                new Vector2(150f, 68f), UIFactory.Tone.Primary, 2f);
            UIFactory.Anchor(_next.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(220f, 34f), new Vector2(150f, 68f));

            _pageLabel = UIFactory.Label("PageNo", _leftPage, "", 24, T.inkSoft,
                TextAlignmentOptions.Right, handwritten: true);
            UIFactory.Anchor(_pageLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 56f),
                new Vector2(240f, 34f));
            _pageLabel.rectTransform.pivot = new Vector2(1f, 0.5f);

            var hint = UIFactory.Label("Hint", _leftPage, "tap a name to read its page", 20,
                T.inkSoft, TextAlignmentOptions.Right, handwritten: true);
            UIFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 90f),
                new Vector2(360f, 30f));
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
        }

        void TurnTo(int page)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_catalogue.Count / (float)RowsPerPage));
            int next = Mathf.Clamp(page, 0, pages - 1);
            if (next == _page) return;

            _page = next;
            CozySounds.PlayAny(CozySounds.Active?.pageFlips, 0.85f);
            BuildList();
            foreach (var entry in _entries) if (entry.logged) SetStruck(entry, 1f);
        }

        // --- open / close --------------------------------------------------------

        public void Open()
        {
            if (IsOpen) return;
            UIModal.Push();
            _overlay.gameObject.SetActive(true);
            CozySounds.Play(CozySounds.Active?.bookOpen, 0.9f);
            BuildCatalogue();
            BuildList();
            ShowSpecies(PickDefaultSelection());

            if (_openRoutine != null) StopCoroutine(_openRoutine);
            _openRoutine = StartCoroutine(OpenRoutine());
        }

        public void Close()
        {
            if (!IsOpen) return;
            UIModal.Pop();
            if (_openRoutine != null) StopCoroutine(_openRoutine);
            CozySounds.Play(CozySounds.Active?.bookClose, 0.9f);
            _overlay.gameObject.SetActive(false);
        }

        IEnumerator OpenRoutine()
        {
            // Opens like a book being laid down: tips up from the spine, settles flat.
            var group = UIFactory.Group(_spread);
            group.alpha = 0f;
            _spread.localScale = new Vector3(0.86f, 0.86f, 1f);
            _spread.localRotation = Quaternion.Euler(0f, 0f, -2.6f);

            StartCoroutine(UITween.ScaleTo(_spread, Vector3.one, 0.38f));
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.38f;
                float e = UITween.Settle(t, 1.5f);
                _spread.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-2.6f, 0f, e));
                group.alpha = Mathf.Min(1f, t * 2.4f);
                yield return null;
            }
            _spread.localRotation = Quaternion.identity;
            group.alpha = 1f;

            // Cross off everything logged since the book was last opened, one at a time.
            yield return new WaitForSecondsRealtime(0.18f);
            foreach (var entry in _entries)
            {
                if (!entry.logged) continue;
                bool isNew = !_alreadyStruck.Contains(entry.species.id);
                if (isNew)
                {
                    CozySounds.Play(CozySounds.Active?.scratch, 0.7f, 0.12f);
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

        /// <summary>
        /// Everything the survey knows about, in the order it is useful: what the forest
        /// has put in front of you today first, then the rest of the guide.
        /// </summary>
        void BuildCatalogue()
        {
            _catalogue.Clear();

            var library = SpeciesLibrary.Active;
            var state = GameState.Instance;
            if (library == null || state == null) return;

            foreach (var species in library.BuildSurveyList(state.day, 3, 2))
                if (species != null && !_catalogue.Contains(species)) _catalogue.Add(species);

            foreach (var species in library.AvailableOn(state.day))
                if (species != null && !_catalogue.Contains(species)) _catalogue.Add(species);

            // Then everything else in the guide. Species you will not meet for days still
            // belong in the book - a field guide you have not finished is the whole point,
            // and it is what gives the page arrows something to turn to.
            foreach (var species in library.species)
                if (species != null && !_catalogue.Contains(species)) _catalogue.Add(species);
        }

        void BuildList()
        {
            foreach (var e in _entries) if (e.row != null) Destroy(e.row.gameObject);
            _entries.Clear();

            var state = GameState.Instance;
            if (state == null) return;

            if (_leftPage.Find("Heading") == null)
            {
                // Just "Survey". Calling it today's implies the forest agreed to a
                // schedule, and the whole point is that you do not know what you will meet.
                var h = UIFactory.Label("Heading", _leftPage, "Survey", 34, T.ink,
                    TextAlignmentOptions.Left, true);
                UIFactory.Anchor(h.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -50f),
                    new Vector2(520f, 48f));

                var sub = UIFactory.Label("Sub", _leftPage, "", 22, T.inkSoft,
                    TextAlignmentOptions.Right, handwritten: true);
                UIFactory.Anchor(sub.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -58f),
                    new Vector2(300f, 34f));
                sub.rectTransform.pivot = new Vector2(1f, 1f);
            }

            var counter = _leftPage.Find("Sub");
            if (counter != null)
                counter.GetComponent<TextMeshProUGUI>().text =
                    state.album.Count + " of " + _catalogue.Count + " recorded";

            int pages = Mathf.Max(1, Mathf.CeilToInt(_catalogue.Count / (float)RowsPerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            if (_pageLabel != null) _pageLabel.text = (_page + 1) + " / " + pages;
            if (_prev != null) _prev.interactable = _page > 0;
            if (_next != null) _next.interactable = _page < pages - 1;

            float y = -132f;
            int from = _page * RowsPerPage;
            int to = Mathf.Min(_catalogue.Count, from + RowsPerPage);

            var wanted = SpeciesLibrary.Active != null
                ? SpeciesLibrary.Active.BuildSurveyList(state.day, 3, 2)
                : new List<SpeciesData>();

            for (int i = from; i < to; i++)
            {
                var species = _catalogue[i];

                var row = UIFactory.Rect("Row_" + species.id, _leftPage);
                UIFactory.Anchor(row, new Vector2(0f, 1f), new Vector2(56f, y), new Vector2(560f, 62f));

                // A dot marks the ones the forest handed you today.
                if (wanted.Contains(species))
                {
                    var mark = UIFactory.Shape("Mark", row, T.Dot, T.berry, Image.Type.Simple);
                    UIFactory.Anchor(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(-30f, 0f),
                        new Vector2(14f, 14f));
                    mark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    mark.raycastTarget = false;
                }

                var label = UIFactory.Label("Name", row, species.commonName, 32, T.ink,
                    TextAlignmentOptions.Left, handwritten: true);
                UIFactory.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(470f, 52f));
                label.rectTransform.pivot = new Vector2(0f, 0.5f);

                bool inSeason = species.AvailableOn(state.day);
                var kind = UIFactory.Label("Kind", row,
                    !inSeason ? "not yet"
                    : species.kind == SpeciesKind.Flora ? "plant" : "animal",
                    20, T.inkSoft, TextAlignmentOptions.Right, handwritten: true);
                if (!inSeason) label.color = new Color(T.ink.r, T.ink.g, T.ink.b, 0.45f);
                UIFactory.Anchor(kind.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
                    new Vector2(140f, 40f));
                kind.rectTransform.pivot = new Vector2(1f, 0.5f);

                var strokeA = MakeStroke(row, -1.6f, 2f, T.berry);
                var strokeB = MakeStroke(row, 1.2f, -3f, new Color(T.berry.r, T.berry.g, T.berry.b, 0.7f));

                var entry = new Entry
                {
                    species = species, row = row, label = label,
                    strokeA = strokeA, strokeB = strokeB,
                    logged = state.album.Has(species.id)
                };
                SetStruck(entry, 0f);
                _entries.Add(entry);

                // A plate that lights up under the pointer, and a chevron on the right.
                // Bare text on paper gives a player no reason to think it can be clicked,
                // which is exactly the complaint: nobody could tell the list was a list of
                // buttons. The plate goes in first so it sits behind the writing.
                var plate = UIFactory.Shape("Plate", row, T.Card, T.paperDeep);
                UIFactory.Stretch(plate.rectTransform, -6f);
                plate.rectTransform.SetAsFirstSibling();
                plate.raycastTarget = true;

                var chevron = UIFactory.Shape("Chevron", row, Sticker.Triangle(48), T.inkSoft,
                    Image.Type.Simple);
                UIFactory.Anchor(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(6f, 0f),
                    new Vector2(20f, 20f));
                chevron.rectTransform.pivot = new Vector2(1f, 0.5f);
                chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                chevron.raycastTarget = false;

                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                var captured = species;
                btn.onClick.AddListener(() => ShowSpecies(captured));

                var highlight = row.gameObject.AddComponent<JournalRow>();
                highlight.Bind(plate, chevron, label, _selected == species);

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
            foreach (var e in _entries)
                if (e.logged && !_alreadyStruck.Contains(e.species.id)) return e.species;
            foreach (var e in _entries)
                if (e.logged) return e.species;
            return _entries.Count > 0 ? _entries[0].species : null;
        }

        // --- right page: the entry -----------------------------------------------

        void ShowSpecies(SpeciesData species)
        {
            bool changed = _selected != null && species != null && _selected != species;
            _selected = species;
            if (changed)
            {
                CozySounds.PlayAny(CozySounds.Active?.pageFlips, 0.75f);
                if (_flipRoutine != null) StopCoroutine(_flipRoutine);
                _flipRoutine = StartCoroutine(FlipPage());
            }

            foreach (Transform child in _rightContent) Destroy(child.gameObject);
            if (species == null) return;

            var state = GameState.Instance;
            var record = state != null ? state.album.Get(species.id) : null;
            bool have = record != null;

            // Photo frame, top of the page.
            var frame = UIFactory.Card("PhotoFrame", _rightContent, new Vector2(536f, 204f),
                have ? T.paper : T.paperDeep, -0.8f);
            UIFactory.Anchor(frame, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(536f, 204f));
            frame.pivot = new Vector2(0.5f, 1f);

            if (have && record.photo != null)
            {
                var photo = UIFactory.Rect("Photo", frame).gameObject.AddComponent<RawImage>();
                photo.texture = record.photo;
                UIFactory.Stretch(photo.rectTransform, 18f);
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
                // Under the print, not on it. Sitting inside the frame it collided with
                // the picture and with the discard button, and the label was clipped.
                var grade = UIFactory.Chip("Grade", _rightContent,
                    PhotoGrading.Name(record.Grade), T.honey, new Vector2(250f, 50f));
                UIFactory.Anchor(grade.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                    new Vector2(66f, -240f), new Vector2(250f, 50f));
                grade.transform.localRotation = Quaternion.Euler(0f, 0f, -1.6f);
            }

            // Names.
            var common = UIFactory.Label("Common", _rightContent, species.commonName, 34, T.ink,
                TextAlignmentOptions.Left, true);
            UIFactory.Anchor(common.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -300f),
                new Vector2(530f, 42f));

            var sci = UIFactory.Label("Scientific", _rightContent, species.scientificName, 23, T.inkSoft,
                TextAlignmentOptions.Left, handwritten: true);
            sci.fontStyle = FontStyles.Italic;
            UIFactory.Anchor(sci.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -340f),
                new Vector2(530f, 34f));

            float y = -382f;
            y = Field("Habitat", species.habitat, y);

            // Before you have seen one, the guide can only tell you where to look. The
            // diet, the notes and the record of your own sighting are the reward for
            // actually finding it, which is what makes the page worth filling.
            if (!have)
            {
                var pending = UIFactory.Label("Pending", _rightContent,
                    "the rest of this page fills in once you have photographed one.",
                    23, T.inkSoft, TextAlignmentOptions.TopLeft, handwritten: true);
                UIFactory.Anchor(pending.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y - 6f),
                    new Vector2(530f, 90f));
                return;
            }

            if (!string.IsNullOrEmpty(species.diet)) y = Field("Diet", species.diet, y);

            y = Field("Your record",
                "First recorded on day " + record.dayTaken + ". "
                + PhotoGrading.Name(record.Grade) + ", " + Mathf.RoundToInt(record.score * 100f)
                + " out of 100. " + Rarity(species), y);

            if (!string.IsNullOrEmpty(species.fieldNote))
            {
                var note = UIFactory.Label("Note", _rightContent, "“" + species.fieldNote + "”",
                    23, T.ink, TextAlignmentOptions.TopLeft, handwritten: true);
                UIFactory.Anchor(note.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y - 2f),
                    new Vector2(530f, 106f));
            }

            // Tucked into the corner of the print rather than sitting under the writing.
            // Throwing a photograph away is a thing you do to the photograph.
            _discardArmed = false;
            _discard = UIFactory.Button("Discard", _rightContent, "Discard",
                () => OnDiscard(species), new Vector2(180f, 50f), UIFactory.Tone.Quiet, 1.6f);
            UIFactory.Anchor(_discard.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(-62f, -240f), new Vector2(180f, 50f));
            _discard.Label.fontSize = 21;
        }

        /// <summary>How hard it was to get, said in words rather than as a rarity number.</summary>
        static string Rarity(SpeciesData species) =>
            species.rarity > 0.8f ? "Rarely seen, and rarely twice."
            : species.rarity > 0.55f ? "Uncommon in this valley."
            : species.rarity > 0.3f ? "Regular here, if you are quiet."
            : "Common enough, once you know the shape of it.";

        void OnDiscard(SpeciesData species)
        {
            if (!_discardArmed)
            {
                _discardArmed = true;
                _discard.Label.text = "Sure?";
                return;
            }

            var state = GameState.Instance;
            if (state == null) return;

            state.album.Remove(species.id);
            _alreadyStruck.Remove(species.id);
            _discardArmed = false;

            BuildList();
            ShowSpecies(species);
        }

        /// <summary>
        /// Squashes the page horizontally about its inner edge and springs it back, which
        /// reads as a sheet turning. Cheap, and far more legible than a cross-fade.
        /// </summary>
        System.Collections.IEnumerator FlipPage()
        {
            if (_rightContent == null) yield break;

            var rt = _rightContent;
            rt.pivot = new Vector2(0f, 0.5f);
            var group = UIFactory.Group(rt);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.34f;
                // Collapse to a sliver, then open out again.
                float x = Mathf.Abs(Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI));
                rt.localScale = new Vector3(Mathf.Max(0.04f, x), 1f, 1f);
                group.alpha = Mathf.Lerp(0.35f, 1f, x);
                yield return null;
            }

            rt.localScale = Vector3.one;
            group.alpha = 1f;
        }

        float Field(string title, string body, float y)
        {
            var head = UIFactory.Label(title + "Head", _rightContent, title.ToUpperInvariant(), 17, T.berry,
                TextAlignmentOptions.Left, true);
            head.characterSpacing = 6f;
            UIFactory.Anchor(head.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y),
                new Vector2(530f, 24f));

            var text = UIFactory.Label(title + "Body", _rightContent, body, 22, T.inkSoft,
                TextAlignmentOptions.TopLeft);
            UIFactory.Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(66f, y - 24f),
                new Vector2(530f, 56f));

            return y - 82f;
        }
    }
}
