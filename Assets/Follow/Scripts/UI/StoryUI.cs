using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Follow.Core;

namespace Follow.UI
{
    /// <summary>
    /// The opening. A few artwork panels with a line each, click to advance, always
    /// skippable - judges are on a clock and nobody should be trapped in a prologue.
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        [Serializable]
        public class Panel
        {
            [TextArea(2, 5)] public string line;
            public Sprite artwork;
        }

        public Panel[] panels =
        {
            new Panel { line = "The survey office sent me up here with a notebook, a camera, and a list of everything they think still lives in this forest." },
            new Panel { line = "They did not send me a guide. Nobody comes this far up any more." },
            new Panel { line = "What they did send was a dog. Young, half-wild, borrowed from a village three valleys back. She has not decided about me yet." },
            new Panel { line = "She knows where things are. I know what they are called.\nBetween us, that might be enough." }
        };

        public float typeSpeed = 34f;

        CozyTheme T => CozyTheme.Active;

        Canvas _canvas;
        Image _art;
        TextMeshProUGUI _text;
        CanvasGroup _artGroup;
        CanvasGroup _textGroup;
        int _index = -1;
        bool _busy;
        bool _finished;

        void Start()
        {
            GameState.Ensure();
            SceneFlow.Ensure();
            UIFactory.EnsureEventSystem();
            Build();
            Next();
        }

        void Build()
        {
            _canvas = UIFactory.CreateCanvas("StoryCanvas");
            var root = UIFactory.Stretch(UIFactory.Rect("Root", _canvas.transform));

            var bg = UIFactory.Solid("Bg", root, new Color(0.09f, 0.08f, 0.07f, 1f));
            UIFactory.Stretch(bg.rectTransform);

            // Artwork sits in the upper two thirds, text beneath it on paper.
            var artRt = UIFactory.Rect("Art", root);
            artRt.anchorMin = new Vector2(0.5f, 1f);
            artRt.anchorMax = new Vector2(0.5f, 1f);
            artRt.pivot = new Vector2(0.5f, 1f);
            artRt.anchoredPosition = new Vector2(0f, -84f);
            artRt.sizeDelta = new Vector2(1080f, 560f);

            _art = artRt.gameObject.AddComponent<Image>();
            _art.sprite = Sticker.Squircle(20);
            _art.type = Image.Type.Sliced;
            _art.color = new Color(0.16f, 0.15f, 0.13f, 1f);
            _art.preserveAspect = true;
            _artGroup = _art.gameObject.AddComponent<CanvasGroup>();

            var caption = UIFactory.Label("Placeholder", artRt, "[ artwork panel ]", 20,
                new Color(1f, 1f, 1f, 0.18f), TextAlignmentOptions.Center);
            UIFactory.Stretch(caption.rectTransform);

            var textPanel = UIFactory.Card("TextPanel", root, new Vector2(1120f, 240f), CozyTheme.Active.cream, -0.6f);
            textPanel.anchorMin = new Vector2(0.5f, 0f);
            textPanel.anchorMax = new Vector2(0.5f, 0f);
            textPanel.pivot = new Vector2(0.5f, 0f);
            textPanel.anchoredPosition = new Vector2(0f, 128f);

            _text = UIFactory.Label("Line", textPanel, "", T.noteSize + 4, T.ink, TextAlignmentOptions.Left, handwritten: true);
            UIFactory.Stretch(_text.rectTransform, 40f);
            _textGroup = _text.gameObject.AddComponent<CanvasGroup>();

            var hint = UIFactory.Label("Hint", root, "click to continue", 17,
                new Color(1f, 0.98f, 0.92f, 0.4f), TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 74f);
            hint.rectTransform.sizeDelta = new Vector2(400f, 30f);

            var skip = UIFactory.Button("Skip", root, "Skip", Finish, new Vector2(190f, 70f), UIFactory.Tone.Quiet);
            var srt = skip.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-42f, -42f);

            // Clicking anywhere advances, so the whole screen is a button underneath.
            var catcher = UIFactory.Solid("ClickCatcher", root, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch(catcher.rectTransform);
            catcher.rectTransform.SetSiblingIndex(1);
            var btn = catcher.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Next);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Next();
            if (kb.escapeKey.wasPressedThisFrame) Finish();
        }

        public void Next()
        {
            if (_finished) return;
            if (_busy) { StopAllCoroutines(); ShowWholeLine(); return; }

            _index++;
            if (_index >= panels.Length) { Finish(); return; }
            StartCoroutine(ShowPanel(panels[_index]));
        }

        IEnumerator ShowPanel(Panel panel)
        {
            _busy = true;

            if (panel.artwork != null)
            {
                _art.sprite = panel.artwork;
                _art.type = Image.Type.Simple;
                _art.color = Color.white;
                var placeholder = _art.transform.Find("Placeholder");
                if (placeholder != null) placeholder.gameObject.SetActive(false);
            }

            StartCoroutine(UITween.FadeGroup(_artGroup, 1f, 0.4f));
            _textGroup.alpha = 1f;
            _text.text = "";

            string line = panel.line;
            float shown = 0f;
            while (shown < line.Length)
            {
                shown += Time.deltaTime * typeSpeed;
                _text.text = line.Substring(0, Mathf.Min(line.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            _text.text = line;
            _busy = false;
        }

        void ShowWholeLine()
        {
            _text.text = panels[Mathf.Clamp(_index, 0, panels.Length - 1)].line;
            _artGroup.alpha = 1f;
            _busy = false;
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            SceneFlow.Ensure().Go(SceneFlow.Game);
        }
    }
}
