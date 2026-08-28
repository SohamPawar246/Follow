using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Follow.Core;
using Follow.UI;
using Follow.World;

namespace Follow.Game
{
    /// <summary>
    /// Fishing, at any pond.
    ///
    /// The dog brings back what it finds, which is generous but not reliable, and the point
    /// of fishing is that it is the one source of food entirely under your own control. It
    /// costs the thing the dog's finds do not - time standing still, at a pond, which is
    /// usually a long way from wherever you were going.
    ///
    /// The bar is Stardew's, because it is the right idea: hold to rise, release to fall,
    /// and keep a wandering fish inside a window you are always slightly behind.
    ///
    /// It starts the instant you press. There used to be a wait-for-the-bite phase in
    /// front of it, and every honest instinct a player has - press the button, see if it
    /// worked - failed the cast with "too soon". A phase whose only rule is "do nothing
    /// yet" is not tension, it is a locked door.
    /// </summary>
    public class FishingGame : MonoBehaviour
    {
        public static FishingGame Instance { get; private set; }

        [Header("Reach")]
        public float castRange = 6.5f;

        [Header("The fight")]
        public float catchSeconds = 16f;
        public float barHeight = 0.26f;
        public float lift = 2.2f;
        public float gravity = -1.6f;
        public float fillRate = 0.52f;
        public float slipRate = 0.2f;

        public bool Busy { get; private set; }

        GameState _state;

        // UI
        RectTransform _root;
        RectTransform _card;
        CanvasGroup _group;
        RectTransform _track;
        RectTransform _window;
        Image _windowImage;
        RectTransform _fish;
        RectTransform _progress;
        Image _progressImage;
        TextMeshProUGUI _caption;
        TextMeshProUGUI _hint;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _state = GameState.Ensure();
            Build();
        }

        void Update()
        {
            if (Busy) return;

            var player = PlayerMover.Instance;
            var hud = GameHud.Instance;
            if (player == null || hud == null) return;

            // Never offer fishing over the top of something more urgent.
            if (UIModal.Any) { hud.HidePrompt(this); return; }
            if (SleepSystem.Instance != null && SleepSystem.Instance.Sleeping) return;
            // Only a shot in progress blocks this. Merely having a subject in view
            // sets the camera to Aiming, and there is almost always something in view -
            // treating that as busy meant the fishing and sleeping prompts never once
            // appeared, and with no sleep the day could never end.
            if (Photography.Instance != null && Photography.Instance.Busy) return;

            var p = player.transform.position;
            if (!WorldComposer.NearestPond(new Vector2(p.x, p.z), 90f, out var pond))
            {
                hud.HidePrompt(this);
                return;
            }

            float edge = Vector2.Distance(new Vector2(p.x, p.z), pond.position) - pond.radius;
            if (edge > castRange) { hud.HidePrompt(this); return; }

            hud.ShowPrompt(this, "E   cast a line", 2);
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                StartCoroutine(Fish());
        }

        // --- the sequence ------------------------------------------------------------

        IEnumerator Fish()
        {
            Busy = true;
            GameHud.Instance?.HidePrompt(this);

            var mover = PlayerMover.Instance;
            if (mover != null) mover.Hold(this);

            Show(true);
            SetWindow(0.5f, barHeight);
            SetFish(0.5f);
            SetProgress(0.4f);

            _caption.color = CozyTheme.Active.ink;
            _caption.text = "cast";
            _hint.text = "";
            CozySounds.Play(CozySounds.Active?.chipPop, 0.7f);

            // Long enough to see the card arrive, short enough that it reads as one
            // continuous action with the keypress that started it.
            yield return StartCoroutine(SlideIn(0.28f));

            int caught = 0;
            yield return Fight(result => caught = result);
            yield return Finish(mover, caught);
        }

        /// <summary>The card comes in from the left as the line goes out.</summary>
        IEnumerator SlideIn(float seconds)
        {
            Vector2 home = new Vector2(-500f, -10f);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                // Overshoot slightly, then settle. A card that simply appears reads as a
                // popup; one that arrives reads as part of the throw.
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                _card.anchoredPosition = Vector2.Lerp(home + new Vector2(-140f, 0f), home, ease);
                _group.alpha = k;
                yield return null;
            }
            _card.anchoredPosition = home;
            _group.alpha = 1f;
        }

        IEnumerator Fight(System.Action<int> onDone)
        {
            _caption.text = "hold E";
            _hint.text = "keep it in the green";

            float bar = 0.5f;
            float barVelocity = 0f;
            float fish = 0.5f;
            float progress = 0.4f;
            float noiseSeed = Random.value * 100f;
            float elapsed = 0f;
            float wasInside = 1f;

            while (elapsed < catchSeconds)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // The fish wanders on smooth noise with the occasional dart, so it is
                // readable most of the time and surprising just often enough. It settles
                // down for the first second so the opening is never a scramble.
                float calm = Mathf.Clamp01(elapsed / 1.2f);
                float target = Mathf.PerlinNoise(noiseSeed, elapsed * 0.5f);
                if (calm > 0.9f && Mathf.PerlinNoise(noiseSeed + 40f, elapsed * 2.3f) > 0.88f)
                    target = Mathf.Clamp01(target + Random.Range(-0.3f, 0.3f));
                fish = Mathf.Lerp(fish, Mathf.Lerp(0.5f, Mathf.Clamp01(target), calm),
                    1f - Mathf.Exp(-dt / 0.4f));

                barVelocity += (Held() ? lift : gravity) * dt;
                barVelocity = Mathf.Clamp(barVelocity, -1.6f, 1.6f);
                bar += barVelocity * dt;

                // Soft walls: hitting an end kills the momentum instead of sticking there.
                if (bar < barHeight * 0.5f) { bar = barHeight * 0.5f; barVelocity = Mathf.Max(0f, barVelocity); }
                if (bar > 1f - barHeight * 0.5f) { bar = 1f - barHeight * 0.5f; barVelocity = Mathf.Min(0f, barVelocity); }

                bool inside = Mathf.Abs(fish - bar) < barHeight * 0.5f;
                progress += (inside ? fillRate : -slipRate) * dt;
                progress = Mathf.Clamp01(progress);

                SetWindow(bar, barHeight);
                SetFish(fish);
                SetProgress(progress);

                // The window itself is the readout: green while you have it, pale the
                // instant you lose it. Nothing else on the card needs to be watched.
                wasInside = Mathf.MoveTowards(wasInside, inside ? 1f : 0f, dt / 0.12f);
                _windowImage.color = Color.Lerp(CozyTheme.Active.paperDeep,
                    CozyTheme.Active.leaf, wasInside);
                _progressImage.color = progress < 0.25f
                    ? CozyTheme.Active.berry : CozyTheme.Active.honey;

                if (progress >= 1f)
                {
                    _caption.color = CozyTheme.Active.forest;
                    _caption.text = "got it";
                    _hint.text = "";
                    CozySounds.Play(CozySounds.Active?.chipPop, 1f);
                    yield return new WaitForSeconds(0.7f);
                    // A long fight lands a better fish.
                    onDone?.Invoke(elapsed > catchSeconds * 0.45f ? 2 : 1);
                    yield break;
                }
                if (progress <= 0f)
                {
                    _caption.color = CozyTheme.Active.berry;
                    _caption.text = "it got away";
                    _hint.text = "";
                    yield return new WaitForSeconds(0.9f);
                    onDone?.Invoke(0);
                    yield break;
                }

                yield return null;
            }

            _caption.text = "your arms gave out";
            _hint.text = "";
            yield return new WaitForSeconds(0.9f);
            onDone?.Invoke(0);
        }

        IEnumerator Finish(PlayerMover mover, int caught)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                _group.alpha = 1f - t / 0.2f;
                yield return null;
            }
            Show(false);

            if (caught > 0)
            {
                _state.AddFood(caught);
                GameHud.Instance?.Say(caught > 1 ? "two good fish" : "one for the pan");
                _state.AddBond(0.01f);
            }

            // Fishing costs the thing you actually have least of at dusk.
            _state.energy = Mathf.Clamp01(_state.energy - 0.04f);

            yield return null;
            if (mover != null) mover.Release(this);
            Busy = false;
        }

        static bool Held() =>
            Keyboard.current != null &&
            (Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed);

        // --- the interface --------------------------------------------------------------

        void Build()
        {
            var T = CozyTheme.Active;

            var canvas = UIFactory.CreateCanvas("FishingCanvas", 280);
            canvas.transform.SetParent(transform, false);
            _root = UIFactory.Stretch(UIFactory.Rect("Fishing", canvas.transform));

            _card = UIFactory.Card("Rod", _root, new Vector2(268f, 500f), T.cream, -1.4f);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(268f, 500f);
            _card.anchoredPosition = new Vector2(-500f, -10f);

            var title = UIFactory.Label("Title", _card, "fishing", 24, T.inkSoft,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(230f, 32f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.characterSpacing = 4f;

            // The lane the fish swims in.
            _track = UIFactory.Rect("Track", _card);
            UIFactory.Anchor(_track, new Vector2(0.5f, 0.5f), new Vector2(-28f, 10f),
                new Vector2(74f, 340f));
            _track.anchorMin = _track.anchorMax = _track.pivot = new Vector2(0.5f, 0.5f);

            var trackOutline = UIFactory.Shape("Outline", _track, T.Chip, T.outline);
            UIFactory.Stretch(trackOutline.rectTransform);
            var trackBack = UIFactory.Shape("Back", _track, T.Chip, T.sky);
            UIFactory.Stretch(trackBack.rectTransform, T.outlineWidth * 0.7f);
            trackBack.raycastTarget = false;

            _window = UIFactory.Rect("Window", _track);
            _window.anchorMin = new Vector2(0f, 0f);
            _window.anchorMax = new Vector2(1f, 0f);
            _window.pivot = new Vector2(0.5f, 0.5f);
            _window.offsetMin = new Vector2(6f, 0f);
            _window.offsetMax = new Vector2(-6f, 0f);
            _windowImage = _window.gameObject.AddComponent<Image>();
            _windowImage.sprite = T.Chip;
            _windowImage.type = Image.Type.Sliced;
            _windowImage.color = T.paperDeep;
            _windowImage.raycastTarget = false;

            _fish = UIFactory.Rect("Fish", _track);
            _fish.anchorMin = _fish.anchorMax = new Vector2(0.5f, 0f);
            _fish.pivot = new Vector2(0.5f, 0.5f);
            _fish.sizeDelta = new Vector2(40f, 26f);
            var fishBody = UIFactory.Shape("Body", _fish, T.Dot, T.berry, Image.Type.Simple);
            UIFactory.Stretch(fishBody.rectTransform);
            fishBody.raycastTarget = false;
            var fishTail = UIFactory.Shape("Tail", _fish, Sticker.Triangle(48), T.berry,
                Image.Type.Simple);
            UIFactory.Anchor(fishTail.rectTransform, new Vector2(0f, 0.5f), new Vector2(-2f, 0f),
                new Vector2(18f, 18f));
            fishTail.rectTransform.pivot = new Vector2(1f, 0.5f);
            fishTail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            fishTail.raycastTarget = false;

            // How close you are to landing it, beside the lane.
            var meter = UIFactory.Rect("Meter", _card);
            UIFactory.Anchor(meter, new Vector2(0.5f, 0.5f), new Vector2(46f, 10f),
                new Vector2(30f, 340f));
            meter.anchorMin = meter.anchorMax = meter.pivot = new Vector2(0.5f, 0.5f);

            var meterOutline = UIFactory.Shape("Outline", meter, T.Chip, T.outline);
            UIFactory.Stretch(meterOutline.rectTransform);
            var meterBack = UIFactory.Shape("Back", meter, T.Chip, T.paperDeep);
            UIFactory.Stretch(meterBack.rectTransform, T.outlineWidth * 0.6f);
            meterBack.raycastTarget = false;

            _progressImage = UIFactory.Shape("Fill", meter, T.Chip, T.honey);
            _progress = _progressImage.rectTransform;
            _progress.anchorMin = new Vector2(0f, 0f);
            _progress.anchorMax = new Vector2(1f, 1f);
            _progress.pivot = new Vector2(0.5f, 0f);
            _progress.offsetMin = new Vector2(4f, 4f);
            _progress.offsetMax = new Vector2(-4f, -4f);
            _progressImage.raycastTarget = false;

            _caption = UIFactory.Label("Caption", _card, "", 26, T.ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 44f),
                new Vector2(236f, 38f));
            _caption.rectTransform.pivot = new Vector2(0.5f, 0f);

            _hint = UIFactory.Label("Hint", _card, "", 19, T.inkSoft,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(236f, 30f));
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);

            _group = UIFactory.Group(_root);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _root.gameObject.SetActive(false);
        }

        void Show(bool show)
        {
            _root.gameObject.SetActive(show);
            _group.alpha = show ? 1f : 0f;
        }

        void SetWindow(float centre01, float height01)
        {
            float h = _track.rect.height;
            _window.sizeDelta = new Vector2(_window.sizeDelta.x, h * height01);
            _window.anchoredPosition = new Vector2(0f, h * Mathf.Clamp01(centre01));
        }

        void SetFish(float at01)
        {
            float h = _track.rect.height;
            _fish.anchoredPosition = new Vector2(0f, h * Mathf.Clamp01(at01));
        }

        void SetProgress(float value01)
        {
            _progress.localScale = new Vector3(1f, Mathf.Clamp01(value01), 1f);
        }
    }
}
