using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;
using Follow.Dog;
using Follow.UI;
using Follow.World;

namespace Follow.Game
{
    /// <summary>
    /// Night, and the end of a day.
    ///
    /// Dark is not a soft suggestion: after dusk the cold starts taking energy far faster
    /// than the day did, so staying out is a decision with a cost rather than a free extra
    /// shift. Sleeping needs the fire, which is what finally makes firewood matter.
    ///
    /// The dog decides for itself how close to the fire to lie down, and that distance is
    /// the only place in the game the bond is ever shown. A dog that trusts you sleeps
    /// against your back; one that does not keeps the fire between you.
    /// </summary>
    public class SleepSystem : MonoBehaviour
    {
        public static SleepSystem Instance { get; private set; }

        [Header("The cold")]
        [Tooltip("Extra energy per minute lost after dark with no fire nearby.")]
        public float coldDrainPerMinute = 0.22f;
        [Tooltip("How much of that the fire holds off, at the fire.")]
        public float fireShelter = 14f;

        [Header("Sleeping")]
        [Tooltip("How near the tent you have to be to lie down.")]
        public float campRange = 4.5f;
        public float sleepSeconds = 4.5f;

        public bool Sleeping { get; private set; }

        GameState _state;
        SleepZs _zs;
        Transform _bed;
        float _nagTimer;

        void Awake() { Instance = this; }

        void Start()
        {
            _state = GameState.Ensure();
            _zs = SleepZs.Create(transform);

            // The clock goes all the way round whether or not anybody lies down, and the
            // day counter has to go with it. Without this the date never changed for a
            // player who kept working through the night.
            var cycle = DayCycle.Instance;
            if (cycle != null) cycle.DayRolled += OnMidnight;
        }

        void OnDestroy()
        {
            var cycle = DayCycle.Instance;
            if (cycle != null) cycle.DayRolled -= OnMidnight;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Dawn arrived on its own. That is a new day, but it is not a rest: nothing is
        /// restored, which is the difference between going to bed and not.
        /// </summary>
        void OnMidnight()
        {
            if (Sleeping) return;
            _state.AdvanceDay();
            _state.AddBond(-0.02f);
            PickupLedger.Clear();
            Object.FindFirstObjectByType<ScentField>()?.NewDay();
            Object.FindFirstObjectByType<FloraField>()?.NewDay();
            GameHud.Instance?.Say("day " + _state.day + ", and you never sat down", 4f);
        }

        void Update()
        {
            if (_state == null || Sleeping) return;
            var cycle = DayCycle.Instance;
            if (cycle == null) return;

            Cold(cycle);
            Offer(cycle);
        }

        /// <summary>The reason to go home. Standing by a lit fire almost stops it.</summary>
        void Cold(DayCycle cycle)
        {
            if (!cycle.IsDusk) return;

            var player = PlayerMover.Instance;
            if (player == null) return;

            float night = Mathf.InverseLerp(cycle.duskAt, 1f, cycle.Time01);
            float shelter = 0f;

            var fire = Campfire.Instance;
            if (fire != null && fire.IsLit)
            {
                float d = Vector3.Distance(player.transform.position, fire.transform.position);
                shelter = Mathf.Clamp01(1f - d / fireShelter) * fire.Warmth;
            }

            float drain = coldDrainPerMinute * night * (1f - shelter * 0.92f);
            _state.energy = Mathf.Clamp01(_state.energy - drain * Time.deltaTime / 60f);

            // Exhaustion at night is the same blackout as starving; the survival system
            // owns that outcome, so this only has to hand it the empty bar.
            if (_state.energy <= 0f && _state.nourishment > 0f)
                _state.nourishment = 0f;
        }

        void Offer(DayCycle cycle)
        {
            var hud = GameHud.Instance;
            var player = PlayerMover.Instance;
            var fire = Campfire.Instance;
            if (hud == null || player == null || fire == null) return;

            // You lie down in the tent, not in the fire. Sharing one spot with the
            // campfire meant one key had to mean both "feed this" and "sleep here".
            if (_bed == null)
            {
                var camp = fire.transform;
                foreach (Transform child in camp)
                    if (child.name.StartsWith("tent")) { _bed = child; break; }
                if (_bed == null) _bed = camp;
            }

            if (Follow.UI.UIModal.Any) { hud.HidePrompt(this); return; }
            if (FishingGame.Instance != null && FishingGame.Instance.Busy) return;
            // Only a shot in progress blocks this. Merely having a subject in view
            // sets the camera to Aiming, and there is almost always something in view -
            // treating that as busy meant the fishing and sleeping prompts never once
            // appeared, and with no sleep the day could never end.
            if (Photography.Instance != null && Photography.Instance.Busy) return;

            // The same test the lens uses. It used to be IsDusk, whose window shuts at
            // dawnAt - so for the last stretch before sunrise it was too dark to work and
            // too late to sleep, and the night simply had to be waited out.
            bool night = cycle.IsDusk || cycle.LightHasGone;
            float d = Vector3.Distance(player.transform.position, _bed.position);

            if (!night || d > campRange)
            {
                // Out in the dark, away from camp.
                //
                // This is the state the player got stranded in: no subject in view so the
                // lens says nothing, too far for the fire or the tent to speak, and the
                // prompt line simply empty for the whole night. Standing in a black wood
                // being told nothing at all is indistinguishable from the game having
                // stopped. So the night itself says which way home is.
                if (cycle.LightHasGone)
                {
                    var fire2 = Campfire.Instance;
                    string where = fire2 != null
                        ? Toward(player.transform.position, fire2.transform.position)
                        : "";

                    hud.ShowPrompt(this, fire2 != null && fire2.IsLit
                        ? "the fire is " + where + " of you"
                        : _state.sticks >= 4
                            ? "dark. camp is " + where + " - you have wood for a fire"
                            : "dark. camp is " + where + " of you", 2);
                }
                else hud.HidePrompt(this);

                // A nudge, once, when the light goes.
                if (night && _nagTimer <= 0f)
                {
                    _nagTimer = 70f;
                    hud.Say(fire.IsLit ? "the light is going. head back to the fire."
                                       : "the light is going, and there is no fire.", 4f);
                }
                _nagTimer -= Time.deltaTime;
                return;
            }

            if (!fire.IsLit)
            {
                hud.ShowPrompt(this, "no fire, no sleep - the night is too cold", 4);
                return;
            }

            hud.ShowPrompt(this, "E   sleep until morning", 4);
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                StartCoroutine(Sleep());
        }

        /// <summary>
        /// Which way something is, in words rather than an arrow on the screen.
        /// </summary>
        static string Toward(Vector3 from, Vector3 target)
        {
            Vector3 to = target - from;
            float degrees = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            string[] names = { "north", "north-east", "east", "south-east",
                               "south", "south-west", "west", "north-west" };
            return names[Mathf.RoundToInt(degrees / 45f) % 8];
        }

        // --- the night ------------------------------------------------------------

        IEnumerator Sleep()
        {
            Sleeping = true;
            var hud = GameHud.Instance;
            hud?.HidePrompt(this);

            var player = PlayerMover.Instance;
            var mover = player;
            if (mover != null) mover.Hold(this);

            var dog = DogBrain.Instance;
            float settled = 9f;
            if (dog != null) settled = dog.SettleForTheNight();

            // The dog needs a moment to walk in and lie down before the Zs make sense.
            float wait = 0f;
            while (wait < 2.6f && dog != null && dog.State != DogState.Rest)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            _zs.Begin(player != null ? player.transform : null,
                      dog != null ? dog.transform : null);

            yield return new WaitForSeconds(sleepSeconds);
            _zs.End();

            Settle(settled);
            Morning();

            if (mover != null) mover.Release(this);
            Sleeping = false;
        }

        /// <summary>
        /// Where the dog chose to lie is the day's verdict. Close is trust earned; the far
        /// side of the fire is a dog that worked for you and went home unimpressed.
        /// </summary>
        void Settle(float distance)
        {
            float closeness = 1f - Mathf.Clamp01(distance / 9f);
            float delta = Mathf.Lerp(-0.03f, 0.07f, closeness);

            // Feeding it costs a ration and is worth more than anything else you can do.
            if (_state.food > 0 && _state.dogHunger > 0.25f)
            {
                _state.AddFood(-1);
                _state.dogHunger = Mathf.Max(0f, _state.dogHunger - 0.6f);
                delta += 0.05f;
                GameHud.Instance?.Say("you shared the last of it with her");
            }

            _state.AddBond(delta);

            GameHud.Instance?.Say(closeness > 0.7f ? "she slept against your back"
                               : closeness > 0.35f ? "she slept an arm's length away"
                               : "she slept on the far side of the fire", 4f);
        }

        void Morning()
        {
            _state.AdvanceDay();
            _state.energy = 1f;
            _state.dogEnergy = 1f;
            _state.nourishment = Mathf.Clamp01(_state.nourishment - 0.12f);
            _state.hydration = Mathf.Clamp01(_state.hydration - 0.15f);

            PickupLedger.Clear();
            DayCycle.Instance?.ResetToMorning();

            Object.FindFirstObjectByType<ScentField>()?.NewDay();
            Object.FindFirstObjectByType<FloraField>()?.NewDay();
            DogBrain.Instance?.NewDay();

            GameHud.Instance?.Say("day " + _state.day, 3.5f);
        }
    }

    /// <summary>
    /// The "z"s. Drawn on the interface canvas and tracked to a world point rather than
    /// placed in the scene, which sidesteps needing a 3D font and keeps them legible at
    /// any camera distance.
    /// </summary>
    public class SleepZs : MonoBehaviour
    {
        RectTransform _root;
        Transform _a, _b;
        float _timer;
        bool _running;

        public static SleepZs Create(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas("SleepCanvas", 60);
            canvas.transform.SetParent(parent, false);
            var root = UIFactory.Stretch(UIFactory.Rect("Zs", canvas.transform));
            var zs = root.gameObject.AddComponent<SleepZs>();
            zs._root = root;
            return zs;
        }

        public void Begin(Transform a, Transform b)
        {
            _a = a; _b = b;
            _running = true;
            _timer = 0f;
        }

        public void End() => _running = false;

        void Update()
        {
            if (!_running) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.55f;

            Puff(_a);
            Puff(_b);
        }

        void Puff(Transform over)
        {
            if (over == null || Camera.main == null) return;

            var label = UIFactory.Label("Z", _root, "z", Random.Range(30, 46),
                CozyTheme.Active.cream, TMPro.TextAlignmentOptions.Center, true);
            TextStyles.Chunky(label, CozyTheme.Active.outline, new Color(0f, 0f, 0f, 0.45f));
            label.rectTransform.sizeDelta = new Vector2(80f, 60f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            var drift = label.gameObject.AddComponent<DriftingZ>();
            drift.follow = over;
            drift.height = over.GetComponent<CharacterController>() is { } cc ? cc.height * 1.15f : 1.4f;
        }
    }

    /// <summary>One "z", rising and fading off whatever it was born over.</summary>
    public class DriftingZ : MonoBehaviour
    {
        public Transform follow;
        public float height = 1.4f;

        TMPro.TextMeshProUGUI _label;
        RectTransform _rect;
        float _t;
        float _sway;

        void Awake()
        {
            _label = GetComponent<TMPro.TextMeshProUGUI>();
            _rect = (RectTransform)transform;
            _sway = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            _t += Time.deltaTime / 2.2f;
            if (_t >= 1f || follow == null || Camera.main == null) { Destroy(gameObject); return; }

            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;

            Vector3 world = follow.position + Vector3.up * (height + _t * 1.1f);
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z < 0f) { Destroy(gameObject); return; }

            var parent = (RectTransform)_rect.parent;
            _rect.anchoredPosition = new Vector2(
                screen.x / scale - parent.rect.width * 0.5f + Mathf.Sin(_sway + _t * 3f) * 16f,
                screen.y / scale - parent.rect.height * 0.5f);

            var c = _label.color;
            c.a = Mathf.Sin(Mathf.Clamp01(_t) * Mathf.PI);
            _label.color = c;
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_sway + _t * 2f) * 12f);
        }
    }
}
