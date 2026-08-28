using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Follow.Core;
using Follow.Data;

namespace Follow.UI
{
    /// <summary>
    /// The print, held up to the light.
    ///
    /// Every shot gets looked at before it goes in the album, because a photograph you
    /// were not allowed to reject is not a photograph you took. Discarding asks twice -
    /// the second press is the whole safeguard, since the picture cannot be got back.
    /// </summary>
    public class PhotoReviewUI : MonoBehaviour
    {
        static CozyTheme T => CozyTheme.Active;

        RectTransform _root;

        /// <summary>Whether the card is on screen. The watchdog asks, so a leaked modal
        /// push can be told apart from a card the player is still looking at.</summary>
        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        RectTransform _card;
        RawImage _print;
        TextMeshProUGUI _title;
        TextMeshProUGUI _grade;
        TextMeshProUGUI _note;
        CozyButton _keep;
        CozyButton _discard;
        CanvasGroup _group;

        bool? _answer;
        bool _armed;

        public static PhotoReviewUI Create(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas("PhotoCanvas", 320);
            canvas.transform.SetParent(parent, false);

            var root = UIFactory.Stretch(UIFactory.Rect("Review", canvas.transform));
            var ui = root.gameObject.AddComponent<PhotoReviewUI>();
            ui.Build(root);
            return ui;
        }

        void Build(RectTransform root)
        {
            _root = root;
            UIFactory.EnsureEventSystem();

            var dim = UIFactory.Solid("Dim", root, new Color(0.08f, 0.06f, 0.04f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            // A polaroid: the print sits high with a fat white margin and the writing
            // underneath, which is where a real one leaves room for it.
            _card = UIFactory.Card("Print", root, new Vector2(660f, 720f), T.cream, -1.6f);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.anchoredPosition = Vector2.zero;

            var window = UIFactory.Shape("Window", _card, T.Card, T.outline);
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f),
                new Vector2(576f, 384f));
            window.rectTransform.pivot = new Vector2(0.5f, 1f);

            var printRect = UIFactory.Rect("Photo", window.rectTransform);
            UIFactory.Stretch(printRect, 8f);
            _print = printRect.gameObject.AddComponent<RawImage>();
            _print.raycastTarget = false;

            _title = UIFactory.Label("Title", _card, "", 40, T.ink, TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -440f),
                new Vector2(580f, 50f));
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);

            _grade = UIFactory.Label("Grade", _card, "", 26, T.inkSoft,
                TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Anchor(_grade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -492f),
                new Vector2(580f, 36f));
            _grade.rectTransform.pivot = new Vector2(0.5f, 1f);

            _note = UIFactory.Label("Note", _card, "", 22, T.inkSoft,
                TextAlignmentOptions.Center, handwritten: true);
            UIFactory.Anchor(_note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -532f),
                new Vector2(560f, 60f));
            _note.rectTransform.pivot = new Vector2(0.5f, 1f);

            _keep = UIFactory.Button("Keep", _card, "Keep it", () => _answer = true,
                new Vector2(250f, 86f), UIFactory.Tone.Leafy, -1.2f);
            UIFactory.Anchor(_keep.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(-134f, 40f), new Vector2(250f, 86f));
            _keep.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

            _discard = UIFactory.Button("Discard", _card, "Discard", OnDiscard,
                new Vector2(250f, 86f), UIFactory.Tone.Quiet, 1.2f);
            UIFactory.Anchor(_discard.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(134f, 40f), new Vector2(250f, 86f));
            _discard.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

            _group = UIFactory.Group(root);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            root.gameObject.SetActive(false);
        }

        /// <summary>First press arms it, second press means it. Nothing is thrown away by accident.</summary>
        void OnDiscard()
        {
            if (!_armed)
            {
                _armed = true;
                _discard.Label.text = "Sure?";
                return;
            }
            _answer = false;
        }

        public IEnumerator Show(SpeciesData species, Texture2D photo, float score, int misses,
            Action<bool> onDecided)
        {
            _answer = null;
            _armed = false;
            _discard.Label.text = "Discard";

            _print.texture = photo;
            _title.text = species != null ? species.commonName : "Unidentified";

            var grade = PhotoGrading.From(score);
            _grade.text = PhotoGrading.Name(grade) + "   ·   " + Mathf.RoundToInt(score * 100f) + "%";
            _note.text = Verdict(misses, score);

            _root.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            UIModal.Push();

            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = t / 0.28f;
                // Drops in like a print being laid down.
                _card.localScale = Vector3.one * Mathf.Lerp(1.14f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.28f));
                yield return null;
            }
            _group.alpha = 1f;
            _card.localScale = Vector3.one;

            while (_answer == null) yield return null;

            t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - t / 0.2f;
                yield return null;
            }

            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _root.gameObject.SetActive(false);
            UIModal.Pop();

            onDecided?.Invoke(_answer.Value);
        }

        /// <summary>
        /// Says what went wrong in the surveyor's own words rather than reporting a number
        /// that is already on the card.
        /// </summary>
        static string Verdict(int misses, float score)
        {
            var cycle = Follow.Game.DayCycle.Instance;
            if (cycle != null && cycle.IsDark && score < 0.5f)
                return "too dark. it needed more light than there was.";

            if (misses == 0) return "steady as anything. that is the one.";
            if (misses == 1) return "one fumble. you can see it in the edges.";
            if (misses == 2) return "the hands went. still, you can tell what it is.";
            return "a smear. worth going back for.";
        }
    }
}
