using UnityEngine;

namespace Follow.Dog
{
    /// <summary>
    /// Drives the Shiba's animation and layers a procedural head-look on top.
    ///
    /// Clips are cross-faded directly rather than through a transition graph: the state
    /// machine already knows what the dog is doing, and a graph would only add a second
    /// place for that truth to live and disagree with it.
    /// </summary>
    [RequireComponent(typeof(DogBrain))]
    public class DogBody : MonoBehaviour
    {
        [Header("Rig")]
        public Animator animator;
        [Tooltip("Head or neck bone. Left empty, the look-at is skipped rather than erroring.")]
        public Transform head;
        public Transform bodyRoot;

        [Header("Clip names (as imported)")]
        public string idle = "AnimalArmature|Idle";
        public string idleAlt = "AnimalArmature|Idle_2";
        public string idleHeadLow = "AnimalArmature|Idle_2_HeadLow";
        public string walk = "AnimalArmature|Walk";
        public string gallop = "AnimalArmature|Gallop";
        public string eating = "AnimalArmature|Eating";

        [Header("Look")]
        public float maxLookAngle = 78f;
        public float lookSmoothing = 0.16f;

        [Header("Feel")]
        [Tooltip("Degrees the body banks into a turn.")]
        public float bankAmount = 13f;
        public float bobHeight = 0.05f;

        DogBrain _brain;
        string _current;
        Quaternion _lookRot = Quaternion.identity;
        Vector3 _bodyBase;
        float _bobPhase;
        float _lookHold;
        float _lastYaw;
        float _bank;

        void Awake()
        {
            _brain = GetComponent<DogBrain>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyRoot != null) _bodyBase = bodyRoot.localPosition;

            _brain.StateChanged += OnStateChanged;
            _brain.CheckedIn += OnCheckedIn;
            _lastYaw = transform.eulerAngles.y;
        }

        void OnDestroy()
        {
            if (_brain == null) return;
            _brain.StateChanged -= OnStateChanged;
            _brain.CheckedIn -= OnCheckedIn;
        }

        void OnStateChanged(DogState from, DogState to) => Retarget();
        void OnCheckedIn() => _lookHold = 1.8f;

        void Update()
        {
            Retarget();
            UpdateBank();
        }

        /// <summary>Picks the clip that matches what the brain is actually doing.</summary>
        void Retarget()
        {
            if (animator == null) return;

            string want;
            switch (_brain.State)
            {
                case DogState.Rest:
                    want = idleHeadLow;
                    break;
                case DogState.Eat:
                    want = eating;
                    break;
                case DogState.Scent:
                    // Nose down while working a scent, even when stationary.
                    want = _brain.Speed > 0.4f ? walk : idleHeadLow;
                    break;
                case DogState.Point:
                    // Frozen and alert. The stillness is the whole tell.
                    want = idle;
                    break;
                default:
                    if (_brain.Speed > 3.4f) want = gallop;
                    else if (_brain.Speed > 0.4f) want = walk;
                    else want = _brain.State == DogState.Idle ? idleAlt : idle;
                    break;
            }

            if (want == _current) return;
            _current = want;
            animator.CrossFadeInFixedTime(want, 0.18f);
        }

        void UpdateBank()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // Bank into turns, scaled by how fast it is actually going.
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;

            float wantBank = Mathf.Clamp(-yawRate * 0.03f, -bankAmount, bankAmount) * _brain.Gait;
            _bank = Mathf.Lerp(_bank, wantBank, 1f - Mathf.Exp(-dt / 0.12f));

            if (bodyRoot == null) return;
            _bobPhase += dt * Mathf.Lerp(3f, 9f, _brain.Gait);
            float bob = Mathf.Abs(Mathf.Sin(_bobPhase)) * bobHeight * _brain.Gait;
            bodyRoot.localPosition = _bodyBase + Vector3.up * bob;
            bodyRoot.localRotation = Quaternion.Euler(0f, 0f, _bank);
        }

        void LateUpdate()
        {
            if (head == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _lookHold -= dt;

            // Points at the find, glances at you on a check-in, otherwise looks ahead.
            Transform target = null;
            if (_brain.State == DogState.Point && _brain.Find != null) target = _brain.Find.transform;
            else if (_lookHold > 0f && _brain.LookTarget != null) target = _brain.LookTarget;

            Quaternion desired = Quaternion.identity;
            if (target != null)
            {
                Vector3 to = target.position + Vector3.up * 0.4f - head.position;
                if (to.sqrMagnitude > 1e-4f)
                {
                    Quaternion world = Quaternion.LookRotation(to, Vector3.up);
                    Quaternion local = Quaternion.Inverse(transform.rotation) * world;
                    Vector3 e = local.eulerAngles;
                    float y = Mathf.DeltaAngle(0f, e.y);
                    float x = Mathf.DeltaAngle(0f, e.x);
                    // Clamp so the neck never twists further than a real one could.
                    if (Mathf.Abs(y) <= maxLookAngle)
                        desired = Quaternion.Euler(Mathf.Clamp(x, -28f, 28f), y, 0f);
                }
            }

            _lookRot = Quaternion.Slerp(_lookRot, desired, 1f - Mathf.Exp(-dt / lookSmoothing));
            // Applied after the animator has written the pose, so it layers rather than fights.
            head.localRotation = head.localRotation * _lookRot;
        }
    }
}
