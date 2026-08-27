using System.Collections.Generic;
using UnityEngine;
using Follow.Data;

namespace Follow.Game
{
    /// <summary>
    /// Anything that can be photographed: a revealed animal, or a flowering specimen.
    ///
    /// One component rather than two parallel systems, because the viewfinder should not
    /// know or care whether the thing in it has legs. What differs between fauna and flora
    /// is the shot type on the species asset, which decides how hard the sequence is.
    /// </summary>
    public class PhotoSubject : MonoBehaviour
    {
        static readonly List<PhotoSubject> All = new List<PhotoSubject>();
        public static IReadOnlyList<PhotoSubject> Active => All;

        public SpeciesData species;

        [Tooltip("Where the lens should point. Usually the head, or the flower.")]
        public Transform aimAt;

        [Tooltip("How close you may get before it leaves. Flora ignores this.")]
        public float wariness = 12f;

        [Tooltip("Raised once it has been shot, so the same individual is not farmed.")]
        public bool Photographed { get; private set; }

        /// <summary>Set while the shot is being taken; the subject holds absolutely still.</summary>
        public bool Calm { get; private set; }

        Animator _animator;
        System.Action _onLeave;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            if (aimAt == null) aimAt = transform;
            _animator = GetComponentInChildren<Animator>();
        }

        public Vector3 AimPoint => aimAt != null ? aimAt.position : transform.position;

        public void OnLeave(System.Action callback) => _onLeave = callback;

        /// <summary>
        /// Stops the subject dead for the duration of the minigame. An animal that keeps
        /// walking while you are pressing arrows is unphotographable and reads as broken.
        /// </summary>
        public void SetCalm(bool calm)
        {
            Calm = calm;
            if (_animator == null) return;
            _animator.speed = calm ? 0.35f : 1f;
        }

        public void MarkPhotographed()
        {
            Photographed = true;
            SetCalm(false);
            _onLeave?.Invoke();
        }

        /// <summary>The best subject for a lens held at this point, or none.</summary>
        public static PhotoSubject Best(Vector3 from, Vector3 facing, float range)
        {
            PhotoSubject best = null;
            float bestScore = 0f;

            for (int i = 0; i < All.Count; i++)
            {
                var subject = All[i];
                if (subject == null || subject.Photographed || subject.species == null) continue;

                Vector3 to = subject.AimPoint - from;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > range || distance < 0.4f) continue;

                // Facing counts, but not absolutely: swinging round to a rustle behind you
                // is a normal thing to do and should not need a perfect turn first.
                float alignment = Vector3.Dot(facing.normalized, to.normalized);
                if (alignment < -0.2f) continue;

                float score = (alignment + 1.2f) / (1f + distance * 0.08f);
                if (score <= bestScore) continue;
                bestScore = score;
                best = subject;
            }
            return best;
        }
    }
}
