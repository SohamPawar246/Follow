using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Follow.Core;
using Follow.Data;

namespace Follow.UI
{
    /// <summary>
    /// The in-game interface. A clock top right, two vitals bars beside it, a couple of
    /// counters, and the journal pinned in the bottom corner. Nothing spans the screen.
    ///
    /// The bond is deliberately absent: it is never a number or a meter, you read it off
    /// where the dog chooses to sleep. Everything shown here is information the player
    /// genuinely cannot infer from looking at the world.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        CozyTheme T => CozyTheme.Active;

        RectTransform _root;
        CozyChip _sticksChip;
        CozyChip _foodChip;
        BookButton _book;
        JournalBook _journal;

        RectTransform _prompt;
        TextMeshProUGUI _promptLabel;
        object _promptOwner;

        RectTransform _toast;
        TextMeshProUGUI _toastLabel;
        Coroutine _toastRoutine;

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            var state = GameState.Instance;
            if (state != null) state.Gained -= OnGained;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            GameState.Ensure();
            UIFactory.EnsureEventSystem();
            Build();
            RefreshBadge();
            StartCoroutine(Reveal());
        }

        void Build()
        {
            var canvas = UIFactory.CreateCanvas("HudCanvas");
            _root = UIFactory.Stretch(UIFactory.Rect("Root", canvas.transform));

            // Clock, top right, with the vitals bars tucked under it.
            var dial = SundialWidget.Create(_root);
            UIFactory.Anchor(dial.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(-44f, -30f), new Vector2(300f, 244f));

            var vitals = VitalsWidget.Create(_root);
            UIFactory.Anchor(vitals.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(-58f, -290f), new Vector2(156f, 306f));

            BuildCounters();

            // The journal replaces the old corner list entirely.
            _journal = JournalBook.Create(_root);
            _book = BookButton.Create(_root, ToggleJournal);
            UIFactory.Anchor(_book.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(56f, 104f), new Vector2(160f, 190f));

            BuildToast();
            BuildPrompt();

            var state = GameState.Instance;
            if (state != null) state.Gained += OnGained;
        }

        void OnGained(GameState.Track track, int amount)
        {
            // The bars look after their own numbers; this one owns the stick counter,
            // which is a chip in the opposite corner and would otherwise change in silence.
            if (track != GameState.Track.Sticks || _sticksChip == null) return;
            FloatingNumber.Pop(_sticksChip.GetComponent<RectTransform>(), amount,
                amount >= 0 ? T.amber : T.berry);
        }

        void BuildCounters()
        {
            var holder = UIFactory.Rect("Counters", _root);
            UIFactory.Anchor(holder, new Vector2(0f, 1f), new Vector2(44f, -36f), new Vector2(340f, 150f));

            _sticksChip = LabelledChip("Sticks", holder, T.amber, 0f, -1.4f, out _);
            _foodChip = LabelledChip("Food", holder, T.leaf, -78f, 1.8f, out _);
        }

        /// <summary>A counter with its name spelled out. A coloured dot alone is a riddle.</summary>
        CozyChip LabelledChip(string name, RectTransform holder, Color dot, float y, float tilt,
            out TextMeshProUGUI caption)
        {
            var size = new Vector2(150f, 64f);
            var chip = UIFactory.Chip(name, holder, "0", dot, size);
            var rt = chip.GetComponent<RectTransform>();
            UIFactory.Anchor(rt, new Vector2(0f, 1f), new Vector2(y < 0f ? 14f : 0f, y), size);
            chip.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            caption = UIFactory.Label("Caption", rt, name, 21, T.cream,
                TextAlignmentOptions.Left, true);
            UIFactory.Anchor(caption.rectTransform, new Vector2(1f, 0.5f), new Vector2(12f, 0f),
                new Vector2(150f, 30f));
            caption.rectTransform.pivot = new Vector2(0f, 0.5f);
            TextStyles.Chunky(caption, T.outline, new Color(0f, 0f, 0f, 0.4f));
            return chip;
        }

        /// <summary>
        /// The "press E" line. Separate from the toast because it stays up for as long as
        /// you are standing somewhere, rather than announcing something that happened.
        /// </summary>
        void BuildPrompt()
        {
            _prompt = UIFactory.Card("Prompt", _root, new Vector2(560f, 78f), T.cream, 0.6f);
            UIFactory.Anchor(_prompt, new Vector2(0.5f, 0f), new Vector2(0f, 274f), new Vector2(560f, 78f));
            _prompt.pivot = new Vector2(0.5f, 0f);

            _promptLabel = UIFactory.Label("Text", _prompt, "", 28, T.ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_promptLabel.rectTransform, 16f);
            TextStyles.Soft(_promptLabel, new Color(0f, 0f, 0f, 0.16f));

            // Shrink to fit rather than spill. Prompts are assembled from a key name and
            // a phrase, and the longest of them do not fit at the full size.
            _promptLabel.enableAutoSizing = true;
            _promptLabel.fontSizeMin = 19f;
            _promptLabel.fontSizeMax = 28f;
            _promptLabel.textWrappingMode = TextWrappingModes.NoWrap;

            UIFactory.Group(_prompt).alpha = 0f;
        }

        /// <summary>
        /// Raise a standing prompt.
        ///
        /// Several systems can want the line at once - you can be knee-deep in a pond with
        /// the dog at your heel and a deer in front of you - and they all ask every frame,
        /// so whoever happened to run last used to win. Priority decides instead, and it is
        /// re-contested each frame, which is what makes the line stable rather than a
        /// flicker between three sentences.
        /// </summary>
        public void ShowPrompt(object owner, string text, int priority = 1)
        {
            if (_promptLabel == null) return;
            _askedThisFrame = true;
            if (priority < _bestPriority) return;

            _bestPriority = priority;
            _promptOwner = owner;
            _pendingText = text;
        }

        /// <summary>What the standing prompt currently says, or empty. Read by the probe.</summary>
        public string PromptText => _pendingText ?? "(none)";

        public void HidePrompt(object owner)
        {
            if (_promptOwner != owner) return;
            _promptOwner = null;
            _pendingText = null;
        }

        int _bestPriority;
        string _pendingText;
        bool _askedThisFrame;

        /// <summary>
        /// Settles the prompt after every system has had its say.
        ///
        /// LateUpdate rather than Update, so the outcome never depends on script execution
        /// order: whoever asked with the highest priority this frame gets the line, and if
        /// nobody asked at all the line goes away by itself. A system that simply stops
        /// asking no longer leaves a stale sentence on screen.
        /// </summary>
        void LateUpdate()
        {
            if (_prompt == null) return;

            if (!_askedThisFrame)
            {
                _pendingText = null;
                _promptOwner = null;
            }
            _askedThisFrame = false;
            _bestPriority = 0;

            var group = UIFactory.Group(_prompt);
            float want = _pendingText != null && !UIModal.Any ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, want, Time.unscaledDeltaTime / 0.18f);
        }

        void BuildToast()
        {
            _toast = UIFactory.Card("Toast", _root, new Vector2(560f, 92f), T.honey, -0.8f);
            UIFactory.Anchor(_toast, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(560f, 92f));
            _toast.pivot = new Vector2(0.5f, 0f);

            _toastLabel = UIFactory.Label("Text", _toast, "", 30, T.ink, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_toastLabel.rectTransform, 20f);
            TextStyles.Soft(_toastLabel, new Color(0f, 0f, 0f, 0.16f));

            // The dog's messages are whole sentences - "she is barking, away to the
            // north-east" is well past what this box holds at thirty points.
            _toastLabel.enableAutoSizing = true;
            _toastLabel.fontSizeMin = 20f;
            _toastLabel.fontSizeMax = 30f;

            UIFactory.Group(_toast).alpha = 0f;
        }

        // --- journal -------------------------------------------------------------

        public void ToggleJournal()
        {
            if (_journal == null) return;
            if (_journal.IsOpen) _journal.Close();
            else _journal.Open();
        }

        /// <summary>Call when a species is photographed: nudges the book and updates the badge.</summary>
        public void OnSpeciesLogged(SpeciesData species)
        {
            RefreshBadge();
            _book?.Nudge();
            if (species != null) Say(species.commonName + " recorded");
        }

        void RefreshBadge()
        {
            var library = SpeciesLibrary.Active;
            var state = GameState.Instance;
            if (library == null || state == null || _book == null) return;

            var list = library.BuildSurveyList(state.day, 3, 2, id => state.album.Has(id));
            int remaining = list.Count(s => !state.album.Has(s.id));
            _book.SetRemaining(remaining);
        }

        // --- toast ---------------------------------------------------------------

        /// <summary>A short message on a card that rises and fades. No dialogue boxes.</summary>
        public void Say(string message, float seconds = 2.4f)
        {
            if (_toastLabel == null) return;
            _toastLabel.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(seconds));
        }

        IEnumerator ToastRoutine(float seconds)
        {
            var group = UIFactory.Group(_toast);
            StartCoroutine(UITween.RiseIn(_toast, group, 0.3f, 34f));
            yield return new WaitForSeconds(seconds);
            yield return UITween.FadeGroup(group, 0f, 0.35f);
        }

        // --- live values ---------------------------------------------------------

        void Update()
        {
            var state = GameState.Instance;
            if (state == null) return;

            _sticksChip?.Set(state.sticks.ToString());
            _foodChip?.Set(state.food.ToString());

            if (_prompt != null && _pendingText != null) _promptLabel.text = _pendingText;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.jKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                ToggleJournal();
        }

        IEnumerator Reveal()
        {
            var dial = (RectTransform)_root.Find("Sundial");
            var counters = (RectTransform)_root.Find("Counters");
            var vitals = (RectTransform)_root.Find("Vitals");
            var book = (RectTransform)_root.Find("BookButton");

            StartCoroutine(UITween.RiseIn(dial, UIFactory.Group(dial), 0.42f, 30f));
            StartCoroutine(UITween.RiseIn(counters, UIFactory.Group(counters), 0.4f, 24f, 0.08f));
            StartCoroutine(UITween.RiseIn(vitals, UIFactory.Group(vitals), 0.4f, 24f, 0.14f));
            StartCoroutine(UITween.RiseIn(book, UIFactory.Group(book), 0.44f, 34f, 0.2f));
            yield break;
        }
    }
}
