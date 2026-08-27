using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;
using Follow.UI;
using Follow.World;

namespace Follow.Game
{
    /// <summary>
    /// Hunger, thirst and the consequence of ignoring either.
    ///
    /// The two bars run at deliberately different speeds. Water empties fast but any pond
    /// fills it instantly, so thirst is a routing problem - it keeps pulling you toward
    /// the landmarks the forest is built around. Food empties slowly but can only be
    /// solved with something you had to earn, so hunger is a planning problem.
    ///
    /// Running either to zero does not end the run. You black out and wake at camp a day
    /// older with nothing but a little food, which costs you the day's work and a scrap of
    /// the dog's trust, and that is a much better teacher than a defeat screen.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SurvivalSystem : MonoBehaviour
    {
        public static SurvivalSystem Instance { get; private set; }

        [Header("Drain, in bar-fractions per minute")]
        public float hungerPerMinute = 0.055f;
        public float thirstPerMinute = 0.085f;

        [Header("Effort")]
        [Tooltip("Extra energy walking costs, per minute at full speed.")]
        public float energyPerMinute = 0.05f;
        public float energyRecoveryPerMinute = 0.02f;

        [Header("Rations")]
        [Tooltip("How much of the food bar one gathered ration restores.")]
        public float rationValue = 0.5f;

        [Header("Water")]
        public float drinkRange = 2.5f;

        public bool Collapsing { get; private set; }

        GameState _state;
        IrisWipe _iris;
        float _drinkCooldown;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _state = GameState.Ensure();
            _iris = IrisWipe.Create(transform, new Color(0.05f, 0.04f, 0.03f, 1f));
        }

        void Update()
        {
            if (_state == null || Collapsing) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Drain(dt);
            Drink(dt);
            Eat();
            FeedTheDog();

            if (_state.nourishment <= 0f || _state.hydration <= 0f) StartCoroutine(Collapse());
        }

        void Drain(float dt)
        {
            float minutes = dt / 60f;

            _state.nourishment = Mathf.Clamp01(_state.nourishment - hungerPerMinute * minutes);
            _state.hydration = Mathf.Clamp01(_state.hydration - thirstPerMinute * minutes);

            var player = PlayerMover.Instance;
            float effort = player != null ? player.Speed01 : 0f;

            // Standing still slowly gives energy back; an empty stomach stops that.
            float change = effort > 0.15f
                ? -energyPerMinute * effort * minutes
                : energyRecoveryPerMinute * minutes * Mathf.Clamp01(_state.nourishment * 2f);
            _state.energy = Mathf.Clamp01(_state.energy + change);

            // The dog gets hungry from working, not from existing.
            var dog = Follow.Dog.DogBrain.Instance;
            float dogEffort = dog != null ? Mathf.Clamp01(dog.Speed / 5f) : 0f;
            _state.dogHunger = Mathf.Clamp01(_state.dogHunger + (0.02f + dogEffort * 0.05f) * minutes);
            _state.dogEnergy = Mathf.Clamp01(_state.dogEnergy
                + (dogEffort > 0.2f ? -0.05f * minutes : 0.035f * minutes));
        }

        void Drink(float dt)
        {
            _drinkCooldown -= dt;

            var player = PlayerMover.Instance;
            if (player == null) return;

            var p = player.transform.position;
            if (!WorldComposer.NearestPond(new Vector2(p.x, p.z), 120f, out var pond)) return;

            float edge = Vector2.Distance(new Vector2(p.x, p.z), pond.position) - pond.radius;
            if (edge > drinkRange) return;

            if (_state.hydration > 0.985f) return;
            if (_drinkCooldown > 0f) return;

            // Reaching water is its own reward; making the player press a key for it would
            // only ever be an opportunity to forget.
            float before = _state.hydration;
            _state.hydration = 1f;
            _state.Announce(GameState.Track.Hydration, 1f - before);
            GameHud.Instance?.Say("cold, clean water");
            _drinkCooldown = 6f;
        }

        void Eat()
        {
            bool asked = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            bool starving = _state.nourishment <= 0.02f;
            if (!asked && !starving) return;

            if (_state.food <= 0)
            {
                if (asked) GameHud.Instance?.Say("nothing left to eat");
                return;
            }
            if (_state.nourishment > 0.94f)
            {
                if (asked) GameHud.Instance?.Say("not hungry yet");
                return;
            }

            _state.AddFood(-1);
            float before = _state.nourishment;
            _state.nourishment = Mathf.Clamp01(_state.nourishment + rationValue);
            _state.Announce(GameState.Track.Nourishment, _state.nourishment - before);
            GameHud.Instance?.Say("that is better");
        }

        /// <summary>
        /// Hand a ration to the dog.
        ///
        /// She used to be fed only as a side effect of going to sleep, which meant a
        /// player who wanted to look after her had no way to actually do it. Sharing food
        /// is the plainest thing you can do for an animal and it should be a button.
        /// </summary>
        void FeedTheDog()
        {
            var dog = Follow.Dog.DogBrain.Instance;
            var player = PlayerMover.Instance;
            var hud = GameHud.Instance;
            if (dog == null || player == null || hud == null) return;

            // Never over the top of something that is already asking a question.
            if (UIModal.Any) { hud.HidePrompt(this); return; }
            if (FishingGame.Instance != null && FishingGame.Instance.Busy) return;
            if (Photography.Instance != null && Photography.Instance.Busy) return;
            if (SleepSystem.Instance != null && SleepSystem.Instance.Sleeping) return;

            if (dog.DistanceToPlayer > 3.2f) { hud.HidePrompt(this); return; }

            if (_state.food <= 0)
            {
                if (_state.dogHunger > 0.5f)
                    hud.ShowPrompt(this, "she is hungry, and you have nothing", 5);
                else hud.HidePrompt(this);
                return;
            }
            if (_state.dogHunger < 0.12f)
            {
                hud.ShowPrompt(this, "she has had plenty", 4);
                return;
            }

            // G, not E. E is the key for whatever you are standing on - the fire, the
            // water, the tent - and the dog is at your heel in all three of those places.
            hud.ShowPrompt(this, "G   share your food with her", 5);
            if (Keyboard.current == null || !Keyboard.current.gKey.wasPressedThisFrame) return;

            _state.AddFood(-1);
            float before = _state.dogHunger;
            _state.dogHunger = Mathf.Max(0f, _state.dogHunger - 0.55f);
            _state.Announce(GameState.Track.DogFed, before - _state.dogHunger);

            // Feeding her is the most reliable thing you can do for the bond, by design.
            _state.AddBond(0.045f);
            _state.dogEnergy = Mathf.Clamp01(_state.dogEnergy + 0.25f);
            hud.Say("she takes it very gently");
            dog.Thank();
        }

        // --- the blackout ---------------------------------------------------------

        IEnumerator Collapse()
        {
            Collapsing = true;

            bool thirst = _state.hydration <= 0f;
            GameHud.Instance?.Say(thirst ? "you cannot go on without water" : "you are too weak to stand", 3.5f);

            var player = PlayerMover.Instance;
            if (player != null)
            {
                var mover = player.GetComponent<PlayerMover>();
                if (mover != null) mover.enabled = false;
            }

            // Close the iris on the player, not on the middle of the screen.
            if (_iris != null && player != null && Camera.main != null)
            {
                var canvas = _iris.GetComponentInParent<Canvas>();
                var screen = Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up);
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                var rect = _iris.rectTransform.rect;
                _iris.focus = new Vector2(
                    screen.x / scale - rect.width * 0.5f,
                    screen.y / scale - rect.height * 0.5f);
            }

            if (_iris != null) yield return _iris.Sweep(1f, 0f, 1.4f);
            yield return new WaitForSecondsRealtime(0.9f);

            WakeAtCamp();

            if (_iris != null) yield return _iris.Sweep(0f, 1f, 1.5f);

            if (player != null)
            {
                var mover = player.GetComponent<PlayerMover>();
                if (mover != null) mover.enabled = true;
            }
            Collapsing = false;
        }

        /// <summary>
        /// The morning after. Everything you were carrying is gone except enough food to
        /// get moving, which is what makes it a setback rather than a wall.
        /// </summary>
        void WakeAtCamp()
        {
            _state.AdvanceDay();
            _state.sticks = 0;
            _state.food = 2;
            _state.nourishment = 0.55f;
            _state.hydration = 0.8f;
            _state.energy = 0.7f;
            _state.dogEnergy = 1f;
            _state.AddBond(-0.05f);

            PickupLedger.Clear();
            DayCycle.Instance?.ResetToMorning();

            var player = PlayerMover.Instance;
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = new Vector3(2.5f, WorldComposer.Height(2.5f, 3f) + 0.3f, 3f);
                if (cc != null) cc.enabled = true;
            }

            var dog = Follow.Dog.DogBrain.Instance;
            if (dog != null)
            {
                var cc = dog.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                dog.transform.position = new Vector3(4.2f, WorldComposer.Height(4.2f, 2f) + 0.3f, 2f);
                if (cc != null) cc.enabled = true;
                dog.NewDay();
            }

            Object.FindFirstObjectByType<ScentField>()?.NewDay();
            GameHud.Instance?.Say("you woke at camp. day " + _state.day + ".", 4f);
        }

        /// <summary>Used by sleeping, which is the same handover without the collapse.</summary>
        public IrisWipe Iris => _iris;
    }
}
