using UnityEngine;
using UnityEngine.InputSystem;

namespace Follow.Game
{
    /// <summary>
    /// Top-down movement. Input is rotated into the camera's yaw so that pressing up
    /// always means up the screen, which is the only thing that matters in this view.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        public static PlayerMover Instance { get; private set; }

        [Header("Movement")]
        public float walkSpeed = 4.2f;
        public float acceleration = 22f;
        public float turnSmoothing = 0.08f;
        public float gravity = -18f;

        [Tooltip("Matches the camera yaw so screen-up is world-forward.")]
        public float cameraYaw = 45f;

        public Vector3 PlanarVelocity { get; private set; }
        public bool IsMoving => PlanarVelocity.sqrMagnitude > 0.05f;
        public float Speed01 => Mathf.Clamp01(PlanarVelocity.magnitude / Mathf.Max(0.01f, walkSpeed));

        CharacterController _cc;
        Vector3 _velocity;
        float _turnVelocity;
        float _vertical;

        readonly System.Collections.Generic.HashSet<object> _holds =
            new System.Collections.Generic.HashSet<object>();
        float _heldSince;

        [Header("Safety")]
        [Tooltip("Longest anything may hold movement before the hold is broken by force.")]
        public float maxHoldSeconds = 20f;

        /// <summary>Something is deliberately keeping you still: a shot, a cast, a night.</summary>
        public bool Frozen => _holds.Count > 0;

        /// <summary>
        /// Take and give back control by name.
        ///
        /// This used to be done by switching the component off, which worked right up until
        /// a coroutine threw between the two halves - and then movement was gone for the
        /// rest of the session with no way back. Holds are counted, and the watchdog below
        /// breaks any that outstays its welcome, so the worst case is a stutter rather than
        /// a game you have to restart.
        /// </summary>
        public void Hold(object owner)
        {
            if (owner == null) return;
            if (_holds.Count == 0) _heldSince = Time.unscaledTime;
            _holds.Add(owner);
        }

        public void Release(object owner)
        {
            if (owner == null) return;
            _holds.Remove(owner);
        }

        public void ReleaseAll() => _holds.Clear();

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            if (Frozen)
            {
                // Nothing lost: still fall, still settle, simply do not steer.
                if (Time.unscaledTime - _heldSince > maxHoldSeconds)
                {
                    Debug.LogWarning("PlayerMover: a hold outlasted " + maxHoldSeconds
                        + "s and was broken. Something did not give control back.");
                    ReleaseAll();
                }

                _velocity.x = 0f;
                _velocity.z = 0f;
                if (_cc.isGrounded && _vertical < 0f) _vertical = -2f;
                _vertical += gravity * dt;
                _velocity.y = _vertical;
                _cc.Move(_velocity * dt);
                PlanarVelocity = Vector3.zero;
                return;
            }

            Vector2 axis = ReadAxis();

            Vector3 wish = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(axis.x, 0f, axis.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            planar = Vector3.MoveTowards(planar, wish * walkSpeed, acceleration * dt);
            _velocity.x = planar.x;
            _velocity.z = planar.z;

            if (_cc.isGrounded && _vertical < 0f) _vertical = -2f;
            _vertical += gravity * dt;
            _velocity.y = _vertical;

            _cc.Move(_velocity * dt);
            PlanarVelocity = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);

            if (wish.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                float y = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVelocity, turnSmoothing);
                transform.rotation = Quaternion.Euler(0f, y, 0f);
            }
        }

        /// <summary>
        /// Turns to look at something, over the next moment rather than instantly. Used
        /// when the lens comes up: you cannot photograph what you are standing side-on to.
        /// </summary>
        public void FaceTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            StopAllCoroutines();
            StartCoroutine(TurnTo(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg));
        }

        System.Collections.IEnumerator TurnTo(float targetYaw)
        {
            float t = 0f;
            float from = transform.eulerAngles.y;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                float y = Mathf.LerpAngle(from, targetYaw, Mathf.SmoothStep(0f, 1f, t / 0.28f));
                transform.rotation = Quaternion.Euler(0f, y, 0f);
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
        }

        static Vector2 ReadAxis()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}
