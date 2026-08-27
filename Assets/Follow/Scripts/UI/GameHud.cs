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

        RectTransform _toast;
        TextMeshProUGUI _toastLabel;
        Coroutine _toastRoutine;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

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
                new Vector2(-44f, -34f), new Vector2(290f, 210f));

            var vitals = VitalsWidget.Create(_root);
            UIFactory.Anchor(vitals.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(-52f, -266f), new Vector2(140f, 260f));

            BuildCounters();

            // The journal replaces the old corner list entirely.
            _journal = JournalBook.Create(_root);
            _book = BookButton.Create(_root, ToggleJournal);
            UIFactory.Anchor(_book.GetComponent<RectTransform>(), new Vector2(0f, 0f),
                new Vector2(52f, 48f), new Vector2(150f, 180f));

            BuildToast();
        }

        void BuildCounters()
        {
            var holder = UIFactory.Rect("Counters", _root);
            UIFactory.Anchor(holder, new Vector2(0f, 1f), new Vector2(44f, -36f), new Vector2(340f, 150f));

            _sticksChip = UIFactory.Chip("Sticks", holder, "0", T.amber, new Vector2(164f, 64f));
            UIFactory.Anchor(_sticksChip.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(164f, 64f));
            _sticksChip.transform.localRotation = Quaternion.Euler(0f, 0f, -1.4f);

            _foodChip = UIFactory.Chip("Food", holder, "0", T.leaf, new Vector2(164f, 64f));
            UIFactory.Anchor(_foodChip.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(14f, -76f), new Vector2(164f, 64f));
            _foodChip.transform.localRotation = Quaternion.Euler(0f, 0f, 1.8f);
        }

        void BuildToast()
        {
            _toast = UIFactory.Card("Toast", _root, new Vector2(580f, 94f), T.honey, -0.8f);
            UIFactory.Anchor(_toast, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(580f, 94f));
            _toast.pivot = new Vector2(0.5f, 0f);

            _toastLabel = UIFactory.Label("Text", _toast, "", 32, T.ink, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_toastLabel.rectTransform, 18f);
            TextStyles.Soft(_toastLabel, new Color(0f, 0f, 0f, 0.16f));
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
