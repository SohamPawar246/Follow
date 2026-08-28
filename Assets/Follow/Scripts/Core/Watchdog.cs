using UnityEngine;
using Follow.UI;

namespace Follow.Core
{
    /// <summary>
    /// The promise that the game always comes back.
    ///
    /// Three things in this project can take the game away from the player: a movement
    /// hold, the modal counter, and the clock itself. Each is taken and given back by a
    /// coroutine, and a coroutine that throws in the middle never reaches the giving-back
    /// half. <see cref="Follow.Game.PlayerMover"/> already breaks its own stuck holds; this
    /// covers the other two.
    ///
    /// It is deliberately conservative. It only ever acts when the interface and the flag
    /// disagree - a modal counter above zero with no modal actually on screen, or a frozen
    /// clock with no pause menu open - and only after several seconds of that
    /// disagreement, so nothing here can interrupt a card the player is still reading. The
    /// worst case becomes a few seconds of being stuck rather than a session that has to
    /// be restarted.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class Watchdog : MonoBehaviour
    {
        public static Watchdog Instance { get; private set; }

        [Tooltip("Seconds of disagreement before anything is corrected.")]
        public float patience = 4f;

        float _modalMismatch;
        float _frozenClock;

        /// <summary>
        /// Installs itself into every run, however the run started.
        ///
        /// Boot asks for one too, but a developer pressing play straight into Game.unity
        /// never goes through Boot - and that is exactly the session where something is
        /// most likely to be half-finished and wedge itself.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => Ensure();

        public static Watchdog Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<Watchdog>();
            if (existing != null) return existing;

            var go = new GameObject("Watchdog");
            DontDestroyOnLoad(go);
            return go.AddComponent<Watchdog>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            ModalCounter(dt);
            FrozenClock(dt);
        }

        /// <summary>
        /// A leaked <c>UIModal.Push</c> is invisible and total: nothing offers a prompt,
        /// nothing can be started, and there is no card on screen to explain why.
        /// </summary>
        void ModalCounter(float dt)
        {
            if (!UIModal.Any) { _modalMismatch = 0f; return; }
            if (SomethingIsOpen()) { _modalMismatch = 0f; return; }

            _modalMismatch += dt;
            if (_modalMismatch < patience) return;

            Debug.LogWarning("Watchdog: the modal counter was above zero with nothing open. "
                           + "Clearing it - something pushed and did not pop.");
            UIModal.Clear();
            _modalMismatch = 0f;
        }

        /// <summary>Only the pause menu is allowed to stop the clock.</summary>
        void FrozenClock(float dt)
        {
            bool paused = PauseMenu.Instance != null && PauseMenu.Instance.IsOpen;
            if (Time.timeScale > 0f || paused) { _frozenClock = 0f; return; }

            _frozenClock += dt;
            if (_frozenClock < patience) return;

            Debug.LogWarning("Watchdog: time was stopped with no pause menu open. Restarting it.");
            Time.timeScale = 1f;
            _frozenClock = 0f;
        }

        static bool SomethingIsOpen()
        {
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) return true;

            foreach (var book in FindObjectsByType<JournalBook>(FindObjectsSortMode.None))
                if (book != null && book.IsOpen) return true;

            foreach (var review in FindObjectsByType<PhotoReviewUI>(FindObjectsSortMode.None))
                if (review != null && review.IsOpen) return true;

            return false;
        }
    }
}
