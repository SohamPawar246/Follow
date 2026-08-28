using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;
using Follow.Game;
using Follow.World;

namespace Follow.UI
{
    /// <summary>
    /// The first ten minutes, taught by doing them.
    ///
    /// It runs once, on the first day of a fresh run, and stops the moment the fire is
    /// lit - by then you have walked, met the dog, gathered, photographed and built, which
    /// is the whole game. Each step is a card in the corner that names one thing and waits
    /// for you to do it; nothing is gated, so a player who already knows can simply get on
    /// with it and watch the steps tick off behind them.
    ///
    /// It teaches by watching the game state rather than by locking input, because a
    /// tutorial you cannot walk away from is a cutscene.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        CozyTheme T => CozyTheme.Active;

        class Step
        {
            public string title;
            public string body;
            public System.Func<bool> done;
            public float minSeconds;
        }

        RectTransform _card;
        TextMeshProUGUI _title;
        TextMeshProUGUI _body;
        RectTransform _tick;
        CanvasGroup _group;

        GameState _state;
        readonly List<Step> _steps = new List<Step>();
        bool _photographed;
        float _wanted;

        void Start()
        {
            _state = GameState.Ensure();

            // Only ever on a fresh run. Waking up on day three to be told what W does
            // would be insulting.
            if (_state.day > 1 || _state.campfireBuilt || TutorialMemory.Finished)
            { enabled = false; return; }

            Build();
            BuildSteps();
            StartCoroutine(Run());
        }

        void BuildSteps()
        {
            _steps.Add(new Step
            {
                title = "Walk",
                body = "W A S D to move. The forest goes on as far as you care to go.",
                minSeconds = 2f,
                done = () => PlayerMover.Instance != null && PlayerMover.Instance.Speed01 > 0.35f
            });

            _steps.Add(new Step
            {
                title = "Whistle for her",
                body = "Press Q, or click the paw at the top right. She works ahead of you "
                     + "and finds what you would walk straight past.",
                minSeconds = 2f,
                done = () => _whistled
            });

            _steps.Add(new Step
            {
                title = "Listen for barking",
                body = "When she finds something she stops and barks until you come. "
                     + "That bark is the only thing telling you where to look.",
                minSeconds = 3f,
                done = () => Follow.Dog.DogBrain.Instance != null
                          && (Follow.Dog.DogBrain.Instance.State == Follow.Dog.DogState.Point
                           || _photographed)
            });

            _steps.Add(new Step
            {
                title = "Photograph it",
                body = "Press F when you are close. Arrow keys, in the order shown - "
                     + "green means you got it, red means you did not, and the bar is "
                     + "how long you have.",
                done = () => _photographed
            });

            _steps.Add(new Step
            {
                title = "Pick up firewood",
                body = "Fallen branches lie under the trees. Walk over one to take it. "
                     + "You need four for a fire.",
                done = () => _state.sticks >= 1
            });

            _steps.Add(new Step
            {
                title = "Read the journal",
                body = "J opens it. The list is what the forest has put in front of you; "
                     + "the right page fills in as you record things.",
                minSeconds = 1.5f,
                done = () => _journalOpened
            });

            _steps.Add(new Step
            {
                title = "Four sticks",
                body = "Keep gathering. Water is free - just stand at a pond. Food is not: "
                     + "press E at the water to fish, and G shares what you have with her.",
                done = () => _state.sticks >= 4
            });

            _steps.Add(new Step
            {
                title = "Build the fire",
                body = "Go back to the marked square at camp and press E. After dark the "
                     + "fire is the only warm thing there is, and you sleep in the tent.",
                done = () => _state.campfireBuilt
            });
        }

        bool _journalOpened;
        bool _whistled;

        void Update()
        {
            // It sits under the journal and the photo review on the canvas order, where it
            // shows through their dim as a grey slab. Simply get out of the way - and do
            // the same during a shot, where the arrow row can be driven down over it.
            bool busy = UIModal.Any
                     || (Photography.Instance != null && Photography.Instance.Busy);
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha,
                    busy ? 0f : _wanted, Time.unscaledDeltaTime / 0.15f);

            var kb = Keyboard.current;
            if (kb != null && (kb.jKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                _journalOpened = true;
            if (kb != null && kb.qKey.wasPressedThisFrame) _whistled = true;

            // Counting shots rather than album entries, so discarding a bad one still
            // counts as having learned how to take it.
            if (!_photographed && Photography.Instance != null && Photography.Instance.ShotsTaken > 0)
                _photographed = true;
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(1.6f);

            foreach (var step in _steps)
            {
                yield return Show(step);
                yield return Wait(step);
                yield return Tick();
            }

            _title.text = "That is all of it";
            _body.text = "Sleep in the tent when it gets dark. Everything else is the "
                       + "forest. Esc for options.";
            yield return new WaitForSeconds(5f);
            yield return Hide();

            TutorialMemory.Finished = true;
        }

        IEnumerator Wait(Step step)
        {
            float held = 0f;
            while (true)
            {
                held += Time.deltaTime;
                bool ready = held >= step.minSeconds;
                if (ready && (step.done == null || step.done())) yield break;
                yield return null;
            }
        }

        // --- the card ------------------------------------------------------------

        void Build()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("TutorialCanvas", 45);
            canvas.transform.SetParent(transform, false);
            var root = UIFactory.Stretch(UIFactory.Rect("Tutorial", canvas.transform));

            // Narrower and further left than it was, so its right edge clears the toast
            // card that rises out of the bottom centre.
            _card = UIFactory.Card("Card", root, new Vector2(420f, 190f), T.cream, -1.1f);
            UIFactory.Anchor(_card, new Vector2(0f, 0f), new Vector2(240f, 40f), new Vector2(420f, 186f));
            _card.pivot = new Vector2(0f, 0f);

            var tab = UIFactory.Card("Tab", _card, new Vector2(300f, 56f), T.leaf, 2.2f);
            UIFactory.Anchor(tab, new Vector2(0f, 1f), new Vector2(26f, 22f), new Vector2(300f, 56f));
            tab.pivot = new Vector2(0f, 1f);

            _title = UIFactory.Label("Title", tab, "", 26, T.cream, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(_title.rectTransform, 8f);
            TextStyles.Chunky(_title, T.outline, new Color(0f, 0f, 0f, 0.35f));

            // "Photograph something" is wider than the tab at 26pt and was spilling over
            // both edges. Let it shrink to fit rather than overflow.
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 15f;
            _title.fontSizeMax = 26f;
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Truncate;

            _body = UIFactory.Label("Body", _card, "", 21, T.ink, TextAlignmentOptions.TopLeft);
            UIFactory.Anchor(_body.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -34f),
                new Vector2(364f, 134f));
            _body.enableAutoSizing = true;
            _body.fontSizeMin = 14f;
            _body.fontSizeMax = 21f;

            // A tick that stamps on when the step is satisfied.
            _tick = UIFactory.Rect("Tick", _card);
            UIFactory.Anchor(_tick, new Vector2(1f, 1f), new Vector2(-18f, 20f), new Vector2(56f, 56f));
            _tick.pivot = new Vector2(1f, 1f);

            var ring = UIFactory.Shape("Ring", _tick, T.Dot, T.outline, UnityEngine.UI.Image.Type.Simple);
            UIFactory.Stretch(ring.rectTransform);
            ring.raycastTarget = false;
            var fill = UIFactory.Shape("Fill", _tick, T.Dot, T.leaf, UnityEngine.UI.Image.Type.Simple);
            UIFactory.Stretch(fill.rectTransform, T.outlineWidth * 0.8f);
            fill.raycastTarget = false;
            // The tick is drawn from two pills rather than typed, because the display
            // face has no glyph for it and a missing character shows as a blank box.
            var shortArm = UIFactory.Shape("ArmShort", _tick, T.Chip, T.cream);
            UIFactory.Anchor(shortArm.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-8f, -4f),
                new Vector2(18f, 7f));
            shortArm.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            shortArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            shortArm.raycastTarget = false;

            var longArm = UIFactory.Shape("ArmLong", _tick, T.Chip, T.cream);
            UIFactory.Anchor(longArm.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(3f, 1f),
                new Vector2(30f, 7f));
            longArm.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            longArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            longArm.raycastTarget = false;

            _tick.localScale = Vector3.zero;

            _group = UIFactory.Group(_card);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        IEnumerator Show(Step step)
        {
            _title.text = step.title;
            _body.text = step.body;
            _tick.localScale = Vector3.zero;

            CozySounds.Play(CozySounds.Active?.chipPop, 0.5f);
            _wanted = 1f;
            yield return StartCoroutine(UITween.RiseIn(_card, _group, 0.3f, 26f));
        }

        IEnumerator Tick()
        {
            CozySounds.Play(CozySounds.Active?.chipPop, 0.9f);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float k = t / 0.4f;
                _tick.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.35f) * Mathf.Min(1f, k * 3f);
                yield return null;
            }
            yield return new WaitForSeconds(0.7f);
            _wanted = 0f;
            yield return UITween.FadeGroup(_group, 0f, 0.22f);
        }

        IEnumerator Hide()
        {
            _wanted = 0f;
            yield return UITween.FadeGroup(_group, 0f, 0.4f);
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Whether the tutorial has already been played. Kept in PlayerPrefs rather than in
    /// the run's state, because starting over should not mean sitting through it again.
    /// </summary>
    public static class TutorialMemory
    {
        const string Key = "follow.tutorialDone";

        public static bool Finished
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Offered on the options screen, for anyone who wants it again.</summary>
        public static void Reset() => Finished = false;
    }
}
