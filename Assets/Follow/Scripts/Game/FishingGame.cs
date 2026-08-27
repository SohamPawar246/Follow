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
    /// </summary>
    public class FishingGame : MonoBehaviour
    {
        public static FishingGame Instance { get; private set; }

        [Header("Reach")]
        public float castRange = 5.5f;

        [Header("The bite")]
        public Vector2 waitForBite = new Vector2(1.8f, 4.2f);
        [Tooltip("How long you have to strike once it bites. Generous: this is not a reflex test.")]
        public float hookWindow = 1.4f;

        [Header("The fight")]
        public float catchSeconds = 14f;
        public float barHeight = 0.24f;
        public float lift = 2.2f;
        public float gravity = -1.6f;
        public float fillRate = 0.5f;
        public float slipRate = 0.22f;

        public bool Busy { get; private set; }

        GameState _state;

        // UI
        RectTransform _root;
        CanvasGroup _group;
        RectTransform _track;
        RectTransform _window;
        RectTransform _fish;
        RectTransform _progress;
        TextMeshProUGUI _caption;

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
            if (Follow.UI.UIModal.Any) { hud.HidePrompt(this); return; }
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

            hud.ShowPrompt(this, "E   fish here", 2);
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                StartCoroutine(Fish());
        }

        // --- the sequence ------------------------------------------------------------

        IEnumerator Fish()
        {
            Busy = true;
            var hud = GameHud.Instance;
            hud?.HidePrompt(this);

            var player = PlayerMover.Instance;
            var mover = player != null ? player.GetComponent<PlayerMover>() : null;
            if (mover != null) mover.enabled = false;

            Show(true);
            _caption.text = "wait for it";
            SetWindow(0.5f, barHeight);
            SetFish(0.5f);
            SetProgress(0f);

            // The E that started this is still "pressed this frame" right now. Reading it
            // here failed every single cast instantly with "too soon", which is why
            // fishing appeared not to work at all. Let the press go before listening.
            yield return null;
            float grace = 0f;
            while (grace < 0.4f) { grace += Time.deltaTime; yield return null; }

            float wait = Random.Range(waitForBite.x, waitForBite.y);
            float t = 0f;
            bool early = false;
            while (t < wait)
            {
                t += Time.deltaTime;
                // Jabbing at it before the bite loses the cast, which is what makes the
                // waiting a real part of it.
                if (Pressed()) { early = true; break; }
                yield return null;
            }

            if (early)
            {
                _caption.text = "too soon - wait for the bite";
                yield return new WaitForSeconds(0.9f);
                yield return Finish(mover, 0);
                yield break;
            }

            _caption.text = "NOW - press E";
            _caption.color = CozyTheme.Active.berry;
            CozySounds.Play(CozySounds.Active?.chipPop, 1f);

            float window = 0f;
            bool hooked = false;
            while (window < hookWindow)
            {
                window += Time.deltaTime;
                if (Pressed()) { hooked = true; break; }
                yield return null;
            }

            if (!hooked)
            {
                _caption.text = "it slipped the hook";
                yield return new WaitForSeconds(1f);
                yield return Finish(mover, 0);
                yield break;
            }

            _caption.color = CozyTheme.Active.ink;

            int caught = 0;
            yield return Fight(result => caught = result);
            yield return Finish(mover, caught);
        }

        IEnumerator Fight(System.Action<int> onDone)
        {
            _caption.text = "hold to raise";

            float bar = 0.5f;
            float barVelocity = 0f;
            float fish = 0.5f;
            float progress = 0.35f;
            float noiseSeed = Random.value * 100f;
            float elapsed = 0f;

            while (elapsed < catchSeconds)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // The fish wanders on smooth noise with the occasional dart, so it is
                // readable most of the time and surprising just often enough.
                float target = Mathf.PerlinNoise(noiseSeed, elapsed * 0.55f);
                if (Mathf.PerlinNoise(noiseSeed + 40f, elapsed * 2.3f) > 0.86f)
                    target = Mathf.Clamp01(target + Random.Range(-0.35f, 0.35f));
                fish = Mathf.Lerp(fish, Mathf.Clamp01(target), 1f - Mathf.Exp(-dt / 0.4f));

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
                _window.GetComponent<Image>().color = inside
                    ? CozyTheme.Active.leaf : CozyTheme.Active.paperDeep;

                if (progress >= 1f)
                {
                    _caption.text = "got it";
                    yield return new WaitForSeconds(0.6f);
                    // A long fight lands a better fish.
                    onDone?.Invoke(elapsed > catchSeconds * 0.5f ? 2 : 1);
                    yield break;
                }
                if (progress <= 0f)
                {
                    _caption.text = "it got away";
                    yield return new WaitForSeconds(0.9f);
                    onDone?.Invoke(0);
                    yield break;
                }

                yield return null;
            }

            _caption.text = "your arms gave out";
            yield return new WaitForSeconds(0.9f);
            onDone?.Invoke(0);
        }

        IEnumerator Finish(PlayerMover mover, int caught)
        {
            Show(false);

            if (caught > 0)
            {
                _state.AddFood(caught);
                GameHud.Instance?.Say(caught > 1 ? "two good fish" : "one for the pan");
                _state.AddBond(0.01f);
            }

            // Fishing costs the thing you actually have least of at dusk.
            _state.energy = Mathf.Clamp01(_state.energy - 0.05f);

            yield return null;
            if (mover != null) mover.enabled = true;
            Busy = false;
        }

        static bool Pressed() =>
            Keyboard.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);

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

            var card = UIFactory.Card("Rod", _root, new Vector2(280f, 540f), T.cream, -1.4f);
            UIFactory.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(280f, 540f));
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(-460f, -10f);

            // The lane the fish swims in.
            _track = UIFactory.Rect("Track", card);
            UIFactory.Anchor(_track, new Vector2(0.5f, 0.5f), new Vector2(-28f, 6f), new Vector2(74f, 400f));
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
            var windowImg = _window.gameObject.AddComponent<Image>();
            windowImg.sprite = T.Chip;
            windowImg.type = Image.Type.Sliced;
            windowImg.color = T.paperDeep;
            windowImg.raycastTarget = false;

            _fish = UIFactory.Rect("Fish", _track);
            _fish.anchorMin = _fish.anchorMax = new Vector2(0.5f, 0f);
            _fish.pivot = new Vector2(0.5f, 0.5f);
            _fish.sizeDelta = new Vector2(40f, 26f);
            var fishBody = UIFactory.Shape("Body", _fish, T.Dot, T.berry, Image.Type.Simple);
            UIFactory.Stretch(fishBody.rectTransform);
            fishBody.raycastTarget = false;
            var fishTail = UIFactory.Shape("Tail", _fish, Sticker.Triangle(48), T.berry, Image.Type.Simple);
            UIFactory.Anchor(fishTail.rectTransform, new Vector2(0f, 0.5f), new Vector2(-2f, 0f),
                new Vector2(18f, 18f));
            fishTail.rectTransform.pivot = new Vector2(1f, 0.5f);
            fishTail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            fishTail.raycastTarget = false;

            // How close you are to landing it, beside the lane.
            var meter = UIFactory.Rect("Meter", card);
            UIFactory.Anchor(meter, new Vector2(0.5f, 0.5f), new Vector2(46f, 6f), new Vector2(30f, 400f));
            meter.anchorMin = meter.anchorMax = meter.pivot = new Vector2(0.5f, 0.5f);

            var meterOutline = UIFactory.Shape("Outline", meter, T.Chip, T.outline);
            UIFactory.Stretch(meterOutline.rectTransform);
            var meterBack = UIFactory.Shape("Back", meter, T.Chip, T.paperDeep);
            UIFactory.Stretch(meterBack.rectTransform, T.outlineWidth * 0.6f);
            meterBack.raycastTarget = false;

            var progressImg = UIFactory.Shape("Fill", meter, T.Chip, T.honey);
            _progress = progressImg.rectTransform;
            _progress.anchorMin = new Vector2(0f, 0f);
            _progress.anchorMax = new Vector2(1f, 1f);
            _progress.pivot = new Vector2(0.5f, 0f);
            _progress.offsetMin = new Vector2(4f, 4f);
            _progress.offsetMax = new Vector2(-4f, -4f);
            progressImg.raycastTarget = false;

            _caption = UIFactory.Label("Caption", card, "", 22, T.ink,
                TextAlignmentOptions.Center, true);
            UIFactory.Anchor(_caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(250f, 56f));
            _caption.rectTransform.pivot = new Vector2(0.5f, 0f);

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
