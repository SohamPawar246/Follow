using System;
using System.Collections.Generic;
using UnityEngine;
using Follow.Core;
using Follow.Game;

namespace Follow.Dog
{
    public enum DogState
    {
        Idle,       // standing about, looking around
        Follow,     // keeping loose station on the player
        Range,      // out working the forest on its own
        Scent,      // nose down, closing on something it caught
        Point,      // frozen at the find, barking for you
        Fetch,      // going to collect a resource
        Deliver,    // carrying it back to camp
        Lead,       // taking you home
        Rest,       // lying down at camp
        Eat
    }

    /// <summary>
    /// The dog.
    ///
    /// Its job is finding, not fetching. Most fauna in this forest is invisible to the
    /// player until this animal catches its scent and calls you over - which is what makes
    /// it necessary rather than merely convenient. Gathering is a side effect of having a
    /// willing dog; it buys back daylight you would otherwise spend bent over picking up
    /// sticks, and daylight is the only real currency in the game.
    ///
    /// Nothing here is commanded. Every behaviour is chosen by the dog, weighted by how
    /// much it currently likes you.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DogBrain : MonoBehaviour
    {
        public static DogBrain Instance { get; private set; }

        [Header("Movement")]
        public float walkSpeed = 2.4f;
        public float trotSpeed = 4.4f;
        public float runSpeed = 7.2f;
        public float turnRate = 320f;
        public float acceleration = 14f;
        public float gravity = -20f;

        [Header("Station keeping")]
        [Tooltip("Closer than this and it stops crowding you.")]
        public float personalSpace = 2.6f;
        [Tooltip("Beyond this while following, it hurries back.")]
        public float followLeash = 9f;

        [Header("Ranging")]
        [Tooltip("How far it will wander at zero bond, and at full bond. A confident dog works wider.")]
        public Vector2 rangeByBond = new Vector2(11f, 34f);
        public float rangeRepickSeconds = 6f;
        [Tooltip("Seconds between voluntary glances back at you. Falls as bond rises.")]
        public Vector2 checkInByBond = new Vector2(999f, 7f);

        [Header("Scent")]
        public float scentSweep = 16f;
        [Tooltip("Seconds nose-down before it commits and calls you.")]
        public float scentWorkTime = 2.2f;
        [Tooltip("How close you must come for the find to count.")]
        public float findRadius = 6f;

        [Header("Errands")]
        [Tooltip("Bond at which it starts bringing sticks back unprompted.")]
        public float gatherBond = 0.25f;
        public float errandCooldown = 14f;

        // --- runtime -----------------------------------------------------------------

        public DogState State { get; private set; } = DogState.Idle;
        public Vector3 Velocity { get; private set; }
        public float Speed => Velocity.magnitude;
        public float DistanceToPlayer { get; private set; }
        public ScentPoint Find { get; private set; }
        public Transform LookTarget { get; private set; }
        /// <summary>0 walking, 1 flat out. Drives the animation blend.</summary>
        public float Gait { get; private set; }

        public event Action<DogState, DogState> StateChanged;
        public event Action<ScentPoint> Pointed;
        public event Action Barked;
        public event Action CheckedIn;

        CharacterController _cc;
        PlayerMover _player;
        Vector3 _target;
        Vector3 _planar;
        float _vertical;
        float _stateTime;
        float _repickTimer;
        float _checkInTimer;
        float _scentTimer;
        float _pointTimer;
        float _errandTimer;
        float _barkTimer;
        float _sniffPause;
        readonly List<ScentPoint> _ignored = new List<ScentPoint>();

        Follow.World.Pickup _errand;
        Follow.World.PickupKind _carryKind;
        int _carrying;
        Vector3 _nightSpot;
        bool _turningIn;
        float _floraTimer;
        readonly List<Follow.Game.PhotoSubject> _announced = new List<Follow.Game.PhotoSubject>();

        float Bond => GameState.Instance != null ? GameState.Instance.bond : 0.15f;
        float Energy => GameState.Instance != null ? GameState.Instance.dogEnergy : 1f;

        /// <summary>How far it will voluntarily go. The single most legible expression of bond.</summary>
        public float RangeRadius => Mathf.Lerp(rangeByBond.x, rangeByBond.y, Bond) * Mathf.Lerp(0.55f, 1f, Energy);

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _target = transform.position;
            _checkInTimer = UnityEngine.Random.Range(2f, 6f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            _player = PlayerMover.Instance;
            _stateTime += dt;

            if (_player != null)
            {
                Vector3 flat = _player.transform.position - transform.position;
                flat.y = 0f;
                DistanceToPlayer = flat.magnitude;
            }

            Think(dt);
            Steer(dt);
            UpdateCheckIn(dt);
        }

        // --- decisions ---------------------------------------------------------------

        void Think(float dt)
        {
            _errandTimer -= dt;
            _barkTimer -= dt;

            switch (State)
            {
                case DogState.Idle: ThinkIdle(dt); break;
                case DogState.Follow: ThinkFollow(dt); break;
                case DogState.Range: ThinkRange(dt); break;
                case DogState.Scent: ThinkScent(dt); break;
                case DogState.Point: ThinkPoint(dt); break;
                case DogState.Fetch: ThinkFetch(dt); break;
                case DogState.Deliver: ThinkDeliver(dt); break;
                case DogState.Lead: ThinkLead(dt); break;
                case DogState.Rest: ThinkRest(dt); break;
            }
        }

        void ThinkIdle(float dt)
        {
            if (_stateTime < 1.2f) return;

            // A dog with nothing to do drifts back toward its person, or goes to work.
            if (DistanceToPlayer > followLeash) { Switch(DogState.Follow); return; }
            if (TryCatchScent()) return;
            if (Energy > 0.3f) Switch(DogState.Range);
        }

        void ThinkFollow(float dt)
        {
            if (_player == null) return;

            // Hold station a little off to the side rather than treading on their heels.
            Vector3 side = _player.transform.right * (Mathf.PerlinNoise(Time.time * 0.2f, 4.1f) - 0.5f) * 3f;
            _target = _player.transform.position - _player.transform.forward * personalSpace + side;

            if (TryCatchScent()) return;

            // Once it is comfortable again it goes back to its own business.
            if (DistanceToPlayer < personalSpace + 1.5f && _stateTime > 1.5f && Energy > 0.3f)
                Switch(DogState.Range);
        }

        void ThinkRange(float dt)
        {
            if (TryCatchScent()) return;
            NoticeSubjects(dt);
            if (TryErrand()) return;

            if (Energy < 0.18f) { Switch(DogState.Follow); return; }

            // Low bond means it does not care how far away you are; high bond means it
            // keeps itself within reach of you on purpose.
            if (DistanceToPlayer > RangeRadius * 1.25f && Bond > 0.3f) { Switch(DogState.Follow); return; }

            _repickTimer -= dt;
            if (_repickTimer > 0f && (transform.position - _target).sqrMagnitude > 4f) return;
            _repickTimer = rangeRepickSeconds * UnityEngine.Random.Range(0.7f, 1.4f);

            // Wander to a new spot inside its comfortable radius, biased away from where
            // it already is so it explores rather than circling.
            Vector3 anchor = _player != null ? _player.transform.position : transform.position;
            Vector2 disc = UnityEngine.Random.insideUnitCircle.normalized
                           * UnityEngine.Random.Range(RangeRadius * 0.35f, RangeRadius);
            _target = anchor + new Vector3(disc.x, 0f, disc.y);

            // Dogs stop to sniff constantly. This one line does more for how alive it
            // looks than any amount of animation blending.
            if (UnityEngine.Random.value < 0.25f) _sniffPause = UnityEngine.Random.Range(0.6f, 1.6f);
        }

        void ThinkScent(float dt)
        {
            if (Find == null) { Switch(DogState.Range); return; }

            _target = Find.transform.position;
            float d = Vector3.Distance(transform.position, Find.transform.position);

            if (d > 2.5f) return;

            _scentTimer += dt;
            if (_scentTimer < scentWorkTime) return;

            Find.Reveal();
            Switch(DogState.Point);
            Pointed?.Invoke(Find);
        }

        void ThinkPoint(float dt)
        {
            if (Find == null) { Switch(DogState.Range); return; }

            _target = transform.position;   // frozen: this is the whole tell
            _pointTimer += dt;

            // Bark on a rhythm so you can find it by ear alone.
            if (_barkTimer <= 0f)
            {
                _barkTimer = 2.2f;
                Barked?.Invoke();
            }

            if (_player != null && DistanceToPlayer < findRadius)
            {
                // You made it. The subject stays up for the photograph.
                return;
            }

            if (_pointTimer > Find.patience)
            {
                // It waited as long as it could. The animal is gone.
                Find.Consume();
                _ignored.Add(Find);
                Find = null;
                Switch(DogState.Range);
            }
        }

        /// <summary>
        /// Firewood and food, brought back without being asked.
        ///
        /// This is the second reason the dog is not decorative: she covers ground you are
        /// not covering, and the wood she carries in is the wood the fire burns. A dog you
        /// have not earned will not bother, which is what makes earning it worth doing.
        /// </summary>
        bool TryErrand()
        {
            if (_errandTimer > 0f || Bond < gatherBond || Energy < 0.35f) return false;

            Follow.World.Pickup best = null;
            float bestDistance = 26f;

            foreach (var pickup in FindObjectsByType<Follow.World.Pickup>(FindObjectsSortMode.None))
            {
                if (pickup == null) continue;
                float d = Vector3.Distance(transform.position, pickup.transform.position);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = pickup;
            }

            if (best == null) return false;

            _errand = best;
            _target = best.transform.position;
            Switch(DogState.Fetch);
            return true;
        }

        void ThinkFetch(float dt)
        {
            if (_errand == null) { Switch(DogState.Range); return; }

            _target = _errand.transform.position;
            if (Vector3.Distance(transform.position, _target) > 1.4f)
            {
                // Give up rather than obsess if it is taking forever.
                if (_stateTime > 14f) { _errand = null; _errandTimer = errandCooldown; Switch(DogState.Range); }
                return;
            }

            _carryKind = _errand.kind;
            _carrying = _errand.amount;
            _errand.TakenByDog();
            _errand = null;

            if (_barkTimer <= 0f) { _barkTimer = 2f; Barked?.Invoke(); }
            Switch(DogState.Deliver);
        }

        void ThinkDeliver(float dt)
        {
            if (_player == null || _carrying <= 0) { Switch(DogState.Range); return; }

            _target = _player.transform.position;
            LookTarget = _player.transform;

            if (DistanceToPlayer > 2.2f)
            {
                if (_stateTime > 20f) { _carrying = 0; Switch(DogState.Range); }
                return;
            }

            var state = Follow.Core.GameState.Instance;
            if (state != null)
            {
                if (_carryKind == Follow.World.PickupKind.Stick) state.AddSticks(_carrying);
                else state.AddFood(_carrying);
            }

            Follow.UI.GameHud.Instance?.Say(_carryKind == Follow.World.PickupKind.Stick
                ? "she dropped a stick at your feet"
                : "she brought you something to eat");

            state?.AddBond(0.015f);
            _carrying = 0;
            _errandTimer = errandCooldown;
            Switch(DogState.Range);
        }

        /// <summary>
        /// A bark for anything photographable she has got close to - the flowering
        /// specimen as much as the animal she flushed.
        ///
        /// This is how you learn to look where she is looking, so it deliberately does not
        /// care whether the subject was hidden. She goes quiet about a given subject for a
        /// while after announcing it, or the wood turns into a smoke alarm.
        /// </summary>
        void NoticeSubjects(float dt)
        {
            _floraTimer -= dt;
            if (_floraTimer > 0f) return;
            _floraTimer = 1.5f;

            Follow.Game.PhotoSubject nearest = null;
            float best = 11f;

            var all = Follow.Game.PhotoSubject.Active;
            for (int i = 0; i < all.Count; i++)
            {
                var subject = all[i];
                if (subject == null || subject.Photographed) continue;
                if (_announced.Contains(subject)) continue;

                float d = Vector3.Distance(transform.position, subject.AimPoint);
                if (d >= best) continue;
                best = d;
                nearest = subject;
            }

            if (nearest == null) return;

            _announced.Add(nearest);
            LookTarget = nearest.transform;
            _floraTimer = 6f;

            if (_barkTimer > 0f) return;
            _barkTimer = 2.4f;
            Barked?.Invoke();
            Follow.UI.GameHud.Instance?.Say("she has found something");
        }

        void ThinkLead(float dt)
        {
            var camp = GameObject.Find("Camp");
            if (camp == null) { Switch(DogState.Follow); return; }

            _target = camp.transform.position;

            // Leading only works if it keeps checking you are still there.
            if (DistanceToPlayer > 9f)
            {
                _target = transform.position;
                if (_barkTimer <= 0f) { _barkTimer = 2.6f; Barked?.Invoke(); }
            }

            if (Vector3.Distance(transform.position, camp.transform.position) < 4f)
                Switch(DogState.Rest);
        }

        void ThinkRest(float dt)
        {
            _target = _turningIn ? _nightSpot : transform.position;
            if (_turningIn) return;

            if (Energy > 0.6f && _stateTime > 6f && DistanceToPlayer < followLeash)
                Switch(DogState.Idle);
        }

        /// <summary>
        /// Called when the player lies down. The dog picks its own spot relative to the
        /// fire and goes to it; how close that is IS the bond, and it is the only place in
        /// the whole game the number is ever shown to anybody.
        /// </summary>
        public float SettleForTheNight()
        {
            var state = Follow.Core.GameState.Instance;
            var fire = Follow.World.Campfire.Instance;
            Vector3 centre = fire != null ? fire.transform.position : Vector3.zero;

            float distance = state != null ? state.CampfireDistance : 6f;
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;

            _nightSpot = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            _nightSpot.y = Follow.World.WorldComposer.Height(_nightSpot.x, _nightSpot.z);

            _turningIn = true;
            _errand = null;
            _carrying = 0;
            Find = null;
            Switch(DogState.Rest);
            return distance;
        }

        /// <summary>
        /// The core of the whole design: sweep for scent points the player cannot see.
        /// Range and willingness both scale with bond, so a neglected dog simply finds less.
        /// </summary>
        bool TryCatchScent()
        {
            if (State == DogState.Scent || State == DogState.Point) return false;
            if (Energy < 0.15f) return false;

            ScentPoint best = null;
            float bestScore = 0f;
            float sweep = scentSweep * Mathf.Lerp(0.6f, 1.3f, Bond);

            foreach (var point in ScentPoint.Active)
            {
                if (point == null || !point.AvailableTo(Bond)) continue;
                if (_ignored.Contains(point)) continue;

                float d = Vector3.Distance(transform.position, point.transform.position);
                float reach = Mathf.Min(sweep, point.scentRadius);
                if (d > reach) continue;

                // Nearer is better; so is a subject it is confident enough to bother with.
                float score = (1f - d / reach) * (1.2f - point.bondRequired);
                if (score <= bestScore) continue;
                bestScore = score;
                best = point;
            }

            if (best == null) return false;

            Find = best;
            _scentTimer = 0f;
            _pointTimer = 0f;
            Switch(DogState.Scent);
            return true;
        }

        void UpdateCheckIn(float dt)
        {
            // Above a certain bond it starts glancing back at you unprompted. This is the
            // first visible sign the relationship is working, and it is never announced.
            if (Bond < 0.3f || State == DogState.Point || State == DogState.Rest) return;

            _checkInTimer -= dt;
            if (_checkInTimer > 0f) return;
            _checkInTimer = Mathf.Lerp(checkInByBond.x, checkInByBond.y, Bond)
                            * UnityEngine.Random.Range(0.7f, 1.3f);

            if (_player == null) return;
            LookTarget = _player.transform;
            _sniffPause = Mathf.Max(_sniffPause, 0.7f);
            CheckedIn?.Invoke();
        }

        // --- movement ------------------------------------------------------------------

        /// <summary>
        /// Pushes a point out of any pond it has landed in.
        ///
        /// The dog was wandering straight across the water, which reads as a bug even
        /// though a real dog would happily do it - the water has no depth here, so she
        /// looked like she was walking on it.
        /// </summary>
        static Vector3 KeepDry(Vector3 point, float margin)
        {
            var flat = new Vector2(point.x, point.z);
            var ponds = Follow.World.WorldComposer.LandmarksNear(flat, 60f);

            for (int i = 0; i < ponds.Count; i++)
            {
                var pond = ponds[i];
                if (pond.kind != Follow.World.WorldComposer.LandmarkKind.Pond) continue;

                float keepOut = pond.radius + margin;
                Vector2 away = flat - pond.position;
                float d = away.magnitude;
                if (d >= keepOut) continue;

                if (d < 0.01f) away = Vector2.right;
                flat = pond.position + away.normalized * keepOut;
                point = new Vector3(flat.x, point.y, flat.y);
            }
            return point;
        }

        void Steer(float dt)
        {
            _sniffPause -= dt;

            // Never head into the water, and if something has put her in it, walk out.
            _target = KeepDry(_target, 1.5f);
            Vector3 escape = KeepDry(transform.position, 0.8f);
            if ((escape - transform.position).sqrMagnitude > 0.04f) _target = escape;

            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            float desired = DesiredSpeed(distance);
            if (_sniffPause > 0f || State == DogState.Point || State == DogState.Rest) desired = 0f;

            Vector3 wish = distance > 0.35f ? toTarget.normalized : Vector3.zero;

            if (wish.sqrMagnitude > 0.01f && desired > 0.01f)
            {
                // Turn toward the heading, then slow for sharp turns. Animals do not strafe.
                var want = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * dt);

                float align = Vector3.Dot(transform.forward, wish);
                desired *= Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(-0.2f, 0.9f, align));
            }

            Vector3 targetVel = transform.forward * desired;
            _planar = Vector3.MoveTowards(_planar, targetVel, acceleration * dt);

            if (_cc.isGrounded && _vertical < 0f) _vertical = -2f;
            _vertical += gravity * dt;

            var motion = _planar;
            Velocity = motion;
            motion.y = _vertical;
            _cc.Move(motion * dt);

            Gait = Mathf.Clamp01(Speed / runSpeed);
        }

        float DesiredSpeed(float distance)
        {
            switch (State)
            {
                case DogState.Rest:
                case DogState.Point: return 0f;

                case DogState.Scent:
                    // Nose-down work is deliberate, not a sprint.
                    return distance > 6f ? trotSpeed : walkSpeed;

                case DogState.Follow:
                    if (distance < personalSpace) return 0f;
                    // Hurries when it has fallen behind, ambles when it is close.
                    return distance > followLeash ? runSpeed
                         : distance > personalSpace * 2f ? trotSpeed : walkSpeed;

                case DogState.Lead:
                    // Never outruns you: leading is useless if you cannot keep up.
                    return DistanceToPlayer > 7f ? 0f : trotSpeed * 0.85f;

                case DogState.Range:
                    return trotSpeed * Mathf.Lerp(0.7f, 1.1f, Energy);

                default:
                    return distance > 1f ? walkSpeed : 0f;
            }
        }

        void Switch(DogState next)
        {
            if (next == State) return;
            var previous = State;
            State = next;
            _stateTime = 0f;

            if (next == DogState.Scent || next == DogState.Point) _sniffPause = 0f;
            if (next != DogState.Point) _pointTimer = 0f;

            StateChanged?.Invoke(previous, next);
        }

        // --- external nudges ------------------------------------------------------------

        /// <summary>
        /// The player whistled. Whether anything happens is entirely a function of bond -
        /// at low bond the whistle simply dies in the trees, which is the design in one sound.
        /// </summary>
        public bool Whistle()
        {
            if (Bond < 0.2f) return false;
            if (State == DogState.Point) return true;   // it is already calling you

            LookTarget = _player != null ? _player.transform : null;
            Switch(DogState.Follow);
            return true;
        }

        /// <summary>She has just been given something. She stays with you for a moment.</summary>
        public void Thank()
        {
            LookTarget = _player != null ? _player.transform : null;
            _errandTimer = errandCooldown * 0.5f;
            Switch(DogState.Follow);
            if (_barkTimer <= 0f) { _barkTimer = 2f; Barked?.Invoke(); }
        }

        /// <summary>Night has fallen and the player is out. Come and get them.</summary>
        public bool LeadHome()
        {
            if (Bond < 0.2f) return false;
            Switch(DogState.Lead);
            return true;
        }

        /// <summary>The player photographed what the dog pointed at.</summary>
        public void FindCollected()
        {
            if (Find != null) Find.Consume();
            Find = null;
            Switch(DogState.Range);
        }

        /// <summary>Fresh day: forget what it gave up on yesterday.</summary>
        public void NewDay()
        {
            _ignored.Clear();
            _announced.Clear();
            Find = null;
            _errand = null;
            _carrying = 0;
            _turningIn = false;
            Switch(DogState.Idle);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? RangeRadius : rangeByBond.x);
            if (Find != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, Find.transform.position);
            }
        }
    }
}
