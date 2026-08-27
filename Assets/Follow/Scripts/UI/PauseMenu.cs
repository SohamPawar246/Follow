using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;

namespace Follow.UI
{
    /// <summary>
    /// Escape, mid-survey.
    ///
    /// Built lazily the first time it is asked for, so a session that never pauses never
    /// pays for it. Time stops while it is up, which means every animation in here runs on
    /// unscaled time - the tweens already do, and the buttons already do.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        CozyTheme T => CozyTheme.Active;

        RectTransform _root;
        RectTransform _card;
        RectTransform _options;
        bool _open;
        float _resumeTimeScale = 1f;

        public bool IsOpen => _open;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            // Escape backs out of the options card first, then closes the menu.
            if (_open && _options != null && _options.gameObject.activeSelf) ShowOptions(false);
            else Toggle();
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            if (_open) return;
            Build();

            _open = true;
            UIModal.Push();
            _root.gameObject.SetActive(true);
            _resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            if (GameSettings.ReducedMotion) UIFactory.Group(_card).alpha = 1f;
            else StartCoroutine(UITween.RiseIn(_card, UIFactory.Group(_card), T.easeIn, 44f));
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            UIModal.Pop();
            if (_options != null) _options.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
            Time.timeScale = _resumeTimeScale;
        }

        // --- construction --------------------------------------------------------

        void Build()
        {
            if (_root != null) return;

            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("PauseCanvas", 500);
            canvas.transform.SetParent(transform, false);
            _root = UIFactory.Stretch(UIFactory.Rect("Root", canvas.transform));

            var dim = UIFactory.Solid("Dim", _root, T.scrim);
            UIFactory.Stretch(dim.rectTransform);

            _card = UIFactory.Card("Card", _root, new Vector2(560f, 620f), T.cream, -1.2f);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.anchoredPosition = Vector2.zero;

            var banner = UIFactory.Banner("Banner", _card, "Resting", new Vector2(430f, 132f), T.leaf, 1.6f);
            UIFactory.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0f, 56f), new Vector2(430f, 132f));
            banner.pivot = new Vector2(0.5f, 1f);
            var title = banner.Find("Label").GetComponent<TextMeshProUGUI>();
            title.fontSize = 54;
            TextStyles.Display(title, T.outline, new Color(0f, 0f, 0f, 0.4f));

            var note = UIFactory.Label("Note", _card, "the forest will wait", T.noteSize - 2,
                T.inkSoft, TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Anchor(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -104f),
                new Vector2(420f, 36f));

            int row = 0;
            Button("Resume", "Back to the trail", Close, UIFactory.Tone.Primary, ref row);
            Button("Options", "Options", () => ShowOptions(true), UIFactory.Tone.Secondary, ref row);
            Button("Menu", "Leave the forest", ToMenu, UIFactory.Tone.Quiet, ref row);

            _options = SettingsPanel.Build(_root, () => ShowOptions(false));

            _root.gameObject.SetActive(false);
        }

        void Button(string name, string label, System.Action onClick, UIFactory.Tone tone, ref int row)
        {
            var size = new Vector2(420f, 92f);
            float tilt = (row % 2 == 0 ? -1f : 1f) * T.cardTilt * 0.7f;
            var btn = UIFactory.Button(name, _card, label, onClick, size, tone, tilt);
            UIFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -170f - row * 108f), size);
            btn.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            row++;
        }

        void ShowOptions(bool show)
        {
            if (_options == null) return;
            if (show) SettingsPanel.Show(this, _options);
            else _options.gameObject.SetActive(false);
        }

        void ToMenu()
        {
            // Restoring the clock first: a scene loaded at timeScale zero never starts.
            Time.timeScale = 1f;
            _open = false;
            UIModal.Clear();
            _root.gameObject.SetActive(false);
            SceneFlow.Ensure().Go(SceneFlow.MainMenu);
        }
    }
}
