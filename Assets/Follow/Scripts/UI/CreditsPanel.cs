using System.Collections;
using TMPro;
using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// The credits, in the game rather than only in a file beside it.
    ///
    /// The jam requires third-party assets and AI tooling to be declared, and a text file
    /// in a repository is not a declaration anybody playing the build will ever see. This
    /// is the same information on a card, reachable from the menu.
    ///
    /// Two fixed columns rather than a scrolling list. The first attempt used a ScrollRect
    /// over a masked viewport and drew a solid cyan rectangle instead of any text - and a
    /// credits screen is not worth a stencil buffer. Everything fits if it is laid out in
    /// two halves, and a card with no moving parts cannot be broken by the next change.
    /// </summary>
    public class CreditsPanel : MonoBehaviour
    {
        static CozyTheme T => CozyTheme.Active;

        RectTransform _root;
        RectTransform _card;
        CanvasGroup _group;
        bool _open;

        public bool IsOpen => _open;

        public static CreditsPanel Create(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas("CreditsCanvas", 420);
            canvas.transform.SetParent(parent, false);

            // A canvas nested inside another canvas is not driven by the scaler - it keeps
            // whatever its own RectTransform says, which for a fresh one is a small box at
            // the origin. Everything inside then inherits that instead of the screen, and
            // the card lands off-centre with its banner over the top edge.
            UIFactory.Stretch((RectTransform)canvas.transform);

            var root = UIFactory.Stretch(UIFactory.Rect("Credits", canvas.transform));
            var panel = root.gameObject.AddComponent<CreditsPanel>();
            panel.Build(root);
            return panel;
        }

        void Build(RectTransform root)
        {
            _root = root;

            var scrim = UIFactory.Solid("Scrim", _root, T.scrim);
            UIFactory.Stretch(scrim.rectTransform);

            _card = UIFactory.Card("Card", _root, new Vector2(1320f, 760f), T.cream, -0.6f);
            // Anchors first, then size, then position. Setting them the other way round
            // leaves the card offset by whatever the previous anchoring implied.
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(1320f, 760f);
            _card.anchoredPosition = new Vector2(0f, -6f);

            var banner = UIFactory.Banner("Banner", _card, "Credits",
                new Vector2(420f, 108f), T.berry, 1.2f);
            UIFactory.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0f, 44f),
                new Vector2(420f, 108f));
            banner.pivot = new Vector2(0.5f, 1f);

            Column("Left", new Vector2(-322f, -14f), Left());
            Column("Right", new Vector2(322f, -14f), Right());

            var close = UIFactory.Button("Close", _card, "Close", Hide,
                new Vector2(240f, 82f), UIFactory.Tone.Primary, 0.8f);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(240f, 82f);
            closeRect.anchoredPosition = new Vector2(0f, -30f);

            _group = UIFactory.Group(_root);
            _group.alpha = 0f;
            _root.gameObject.SetActive(false);
        }

        void Column(string name, Vector2 at, string body)
        {
            var text = UIFactory.Label(name, _card, body, 21, T.ink, TextAlignmentOptions.TopLeft);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 590f);
            rect.anchoredPosition = at;

            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 15f;
            text.fontSizeMax = 21f;
            text.lineSpacing = 4f;
        }

        // --- content -------------------------------------------------------------

        // Kept in step with CREDITS.md by hand. If you import something, it goes in both.

        static string Left() =>
            "<b>Follow</b>\n" +
            "A survey of the forest, and the dog who makes it possible.\n" +
            "TXG Nagaland Game Jam 2026  ·  Where Nature Leads\n\n" +

            "<b>Built with</b>\n" +
            "Unity 6.3 · Universal Render Pipeline\n" +
            "Input System · TextMeshPro\n\n" +

            "<b>Art</b>\n" +
            "Stylized Nature MegaKit — Quaternius (CC0)\n" +
            "Ultimate Animated Animals — Quaternius (CC0)\n" +
            "Nature Kit — Kenney (CC0)\n" +
            "UI Pack, UI Pack Adventure — Kenney (CC0)\n" +
            "KayKit Adventurers — Kay Lousberg (CC0)\n" +
            "Bird models — Poly Pizza, per-model licences";

        static string Right() =>
            "<b>Audio</b>\n" +
            "RPG Audio — Kenney (CC0)\n" +
            "The wind, birdsong, crickets, music box,\n" +
            "whistle and the dog's fallback voice are\n" +
            "synthesised in code at load.\n\n" +

            "<b>Generative AI</b>\n" +
            "Studio logo intro — Google Veo 3\n" +
            "Story panels — Google Veo 3 and the\n" +
            "ChatGPT image model\n" +
            "No AI-generated 3D models or audio.\n\n" +

            "<b>Species</b>\n" +
            "Entries reference real species of Nagaland\n" +
            "and the Eastern Himalaya, from general\n" +
            "public reference material.\n\n" +

            "<b>Thanks</b>\n" +
            "To everyone who has ever been led\n" +
            "somewhere by a dog.";

        // --- showing -------------------------------------------------------------

        public void Show()
        {
            if (_open) return;
            _open = true;
            UIModal.Push();
            _root.gameObject.SetActive(true);
            CozySounds.Play(CozySounds.Active?.bookOpen, 0.7f);
            StartCoroutine(In());
        }

        public void Hide()
        {
            if (!_open) return;
            _open = false;
            UIModal.Pop();
            CozySounds.Play(CozySounds.Active?.bookClose, 0.6f);
            StartCoroutine(Out());
        }

        IEnumerator In()
        {
            yield return UITween.RiseIn(_card, _group, T.easeIn, 40f);
            _group.alpha = 1f;
        }

        IEnumerator Out()
        {
            yield return UITween.FadeGroup(_group, 0f, 0.22f);
            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_open) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }
    }
}
