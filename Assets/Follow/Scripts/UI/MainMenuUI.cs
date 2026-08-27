using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Follow.Core;

namespace Follow.UI
{
    /// <summary>
    /// The menu. Nothing is centred and nothing is square: the title sits on a tilted
    /// honey card and the buttons stagger down in a gentle arc, so the screen reads as
    /// things laid on a table rather than a dialog box.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        RectTransform _root;
        RectTransform _column;
        RectTransform _options;

        void Start()
        {
            GameState.Ensure();
            SceneFlow.Ensure();
            UIFactory.EnsureEventSystem();
            Build();
            StartCoroutine(Reveal());
        }

        void Build()
        {
            var canvas = UIFactory.CreateCanvas("MenuCanvas");
            _root = UIFactory.Stretch(UIFactory.Rect("Root", canvas.transform));

            var wash = UIFactory.Solid("Wash", _root, new Color(0.10f, 0.08f, 0.05f, 0.30f));
            UIFactory.Stretch(wash.rectTransform);
            wash.raycastTarget = false;

            var vig = UIFactory.Shape("Vignette", _root, Sticker.Vignette(256, 0.30f, 1.4f),
                new Color(0.06f, 0.05f, 0.03f, 0.8f), Image.Type.Simple);
            UIFactory.Stretch(vig.rectTransform);
            vig.raycastTarget = false;

            BuildTitle();
            BuildButtons();
            BuildOptions();

            var footer = UIFactory.Label("Footer", _root,
                "TXG Nagaland Game Jam 2026   ·   Where Nature Leads", 20,
                new Color(1f, 0.97f, 0.9f, 0.55f), TextAlignmentOptions.BottomRight);
            UIFactory.Anchor(footer.rectTransform, new Vector2(1f, 0f), new Vector2(-54f, 40f),
                new Vector2(820f, 34f));
        }

        void BuildTitle()
        {
            _column = UIFactory.Rect("Column", _root);
            UIFactory.Anchor(_column, new Vector2(0f, 0.5f), new Vector2(150f, 30f), new Vector2(700f, 900f));

            // Cream plate with a saturated banner hanging over its top edge. The overlap
            // is the whole trick: a bare panel reads as a document, a banner reads as a game.
            var plate = UIFactory.Card("TitlePlate", _column, new Vector2(560f, 200f), T.cream, -1.6f);
            UIFactory.Anchor(plate, new Vector2(0f, 1f), new Vector2(0f, -46f), new Vector2(560f, 200f));

            var blurb = UIFactory.Label("Blurb", plate, "a survey of the forest", T.noteSize,
                T.inkSoft, TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Stretch(blurb.rectTransform, 20f);
            blurb.rectTransform.offsetMax = new Vector2(-20f, -76f);

            var banner = UIFactory.Banner("TitleBanner", plate, "Follow",
                new Vector2(540f, 150f), T.berry, 1.4f);
            UIFactory.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0f, 62f), new Vector2(540f, 150f));
            banner.pivot = new Vector2(0.5f, 1f);

            var title = banner.Find("Label").GetComponent<TextMeshProUGUI>();
            title.fontSize = 92;
            title.characterSpacing = -2f;
            TextStyles.Display(title, T.outline, new Color(0f, 0f, 0f, 0.45f));

            // A second, smaller note tucked under the plate at an angle.
            var note = UIFactory.Card("Note", _column, new Vector2(420f, 74f), T.paper, 2.4f);
            UIFactory.Anchor(note, new Vector2(0f, 1f), new Vector2(84f, -258f), new Vector2(420f, 74f));
            var noteText = UIFactory.Label("Text", note, "and the dog who makes it possible",
                T.noteSize - 4, T.inkSoft, TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Stretch(noteText.rectTransform, 10f);
        }

        void BuildButtons()
        {
            var stack = UIFactory.Rect("Buttons", _column);
            UIFactory.Anchor(stack, new Vector2(0f, 1f), new Vector2(0f, -372f), new Vector2(560f, 520f));

            bool hasRun = GameState.Instance != null && GameState.Instance.day > 1;
            _row = 0;

            Row(stack, "Play", hasRun ? "Continue" : "Begin the survey", OnPlay, UIFactory.Tone.Primary, 470f);
            if (hasRun) Row(stack, "NewRun", "Start over", OnNewRun, UIFactory.Tone.Quiet, 400f);
            Row(stack, "Album", "Album", OnAlbum, UIFactory.Tone.Leafy, 420f);
            Row(stack, "Options", "Options", () => ShowOptions(true), UIFactory.Tone.Secondary, 400f);
            Row(stack, "Quit", "Quit", OnQuit, UIFactory.Tone.Quiet, 340f);
        }

        int _row;

        /// <summary>
        /// Staggers each button: alternating tilt, a slight horizontal drift and shrinking
        /// widths, so the column reads hand-stacked instead of generated.
        /// </summary>
        void Row(RectTransform parent, string name, string label, Action onClick,
            UIFactory.Tone tone, float width)
        {
            float tilt = (_row % 2 == 0 ? -1f : 1f) * T.cardTilt * 0.8f;
            var size = new Vector2(width, 96f);
            var btn = UIFactory.Button(name, parent, label, onClick, size, tone, tilt);

            float y = -_row * 112f;
            float drift = Mathf.Sin(_row * 1.1f) * 16f;
            UIFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(drift, y), size);
            _row++;
        }

        // --- options -----------------------------------------------------------

        public static bool ReducedMotion { get; private set; }

        void BuildOptions()
        {
            _options = UIFactory.Stretch(UIFactory.Rect("OptionsLayer", _root));

            var dim = UIFactory.Solid("Dim", _options, T.scrim);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Card("Panel", _options, new Vector2(760f, 560f), T.cream, -0.8f);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var tab = UIFactory.Card("Tab", panel, new Vector2(300f, 84f), T.honey, 2f);
            UIFactory.Anchor(tab, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(300f, 84f));
            var heading = UIFactory.Label("Heading", tab, "Options", T.headingSize - 6, T.cream,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(heading.rectTransform);
            TextStyles.Chunky(heading, T.outline, new Color(0f, 0f, 0f, 0.35f));

            Slider(panel, "Sound", -150f, AudioListener.volume, v => AudioListener.volume = v);
            Check(panel, "Fullscreen", -262f, Screen.fullScreen, v => Screen.fullScreen = v);
            Check(panel, "Reduced motion", -348f, false, v => ReducedMotion = v);

            var back = UIFactory.Button("Back", panel, "Back", () => ShowOptions(false),
                new Vector2(300f, 88f), UIFactory.Tone.Primary);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(300f, 88f));

            _options.gameObject.SetActive(false);
        }

        void Slider(RectTransform parent, string label, float y, float value,
            UnityEngine.Events.UnityAction<float> onChange)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600f, 64f));

            var text = UIFactory.Label("Label", row, label, T.bodySize, T.ink, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.36f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            var track = UIFactory.Rect("Slider", row);
            track.anchorMin = new Vector2(0.36f, 0.5f);
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
        }

        void Check(RectTransform parent, string label, float y, bool value,
            UnityEngine.Events.UnityAction<bool> onChange)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600f, 62f));

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

        void ShowOptions(bool show)
        {
            _options.gameObject.SetActive(show);
            if (!show) return;
            var panel = (RectTransform)_options.Find("Panel");
            StartCoroutine(UITween.RiseIn(panel, UIFactory.Group(panel), T.easeIn, 40f));
        }

        // --- reveal & actions --------------------------------------------------

        IEnumerator Reveal()
        {
            var plate = (RectTransform)_column.Find("TitlePlate");
            var sub = (RectTransform)_column.Find("Note");
            StartCoroutine(UITween.RiseIn(plate, UIFactory.Group(plate), 0.44f, 46f));
            StartCoroutine(UITween.RiseIn(sub, UIFactory.Group(sub), 0.4f, 30f, 0.1f));

            float delay = 0.18f;
            foreach (Transform child in _column.Find("Buttons"))
            {
                var rt = (RectTransform)child;
                StartCoroutine(UITween.RiseIn(rt, UIFactory.Group(rt), 0.36f, 24f, delay));
                delay += 0.06f;
            }
            yield break;
        }

        void OnPlay()
        {
            var state = GameState.Ensure();
            SceneFlow.Instance.Go(state.day > 1 ? SceneFlow.Game : SceneFlow.Story);
        }

        void OnNewRun()
        {
            GameState.Ensure().NewRun();
            SceneFlow.Instance.Go(SceneFlow.Story);
        }

        void OnAlbum() => Debug.Log("Album requested from the menu.");

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
