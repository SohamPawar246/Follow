using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// Drives the surveyor's animation from how fast they are actually moving.
    ///
    /// Clips are cross-faded directly rather than through a transition graph, matching the
    /// dog: the movement code already knows the speed, and a blend tree would add a second
    /// place for that truth to live.
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public class PlayerBody : MonoBehaviour
    {
        [Header("Rig")]
        public Animator animator;

        [Header("Clip names (as imported from KayKit)")]
        public string idle = "Idle";
        public string walk = "Walking_A";
        public string run = "Running_A";

        [Header("Thresholds")]
        public float walkThreshold = 0.25f;
        public float runThreshold = 3.6f;
        public float crossFade = 0.16f;

        PlayerMover _mover;
        string _current;

        void Awake()
        {
            _mover = GetComponent<PlayerMover>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (animator == null) return;

            float speed = _mover.PlanarVelocity.magnitude;
            string want = speed > runThreshold ? run
                        : speed > walkThreshold ? walk
                        : idle;

            if (want == _current) return;
            _current = want;
            animator.CrossFadeInFixedTime(want, crossFade);
        }
    }
}
