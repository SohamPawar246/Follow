using System.Collections.Generic;
using UnityEngine;
using Follow.Data;

namespace Follow.Dog
{
    /// <summary>
    /// A subject the player cannot find on their own. It sits in the world unrevealed
    /// until the dog gets close enough to catch it, which is the entire reason the dog
    /// is not optional.
    /// </summary>
    public class ScentPoint : MonoBehaviour
    {
        static readonly List<ScentPoint> All = new List<ScentPoint>();
        public static IReadOnlyList<ScentPoint> Active => All;

        public SpeciesData species;

        [Tooltip("How close the dog must be to catch it. Rarer subjects hide better.")]
        public float scentRadius = 14f;

        [Tooltip("Bond needed before the dog will bother reporting this one.")]
        [Range(0f, 1f)] public float bondRequired = 0f;

        [Tooltip("Seconds the subject waits once pointed before it leaves.")]
        public float patience = 14f;

        [Tooltip("Set once the dog has reported it; it will not be offered twice in a day.")]
        public bool Consumed { get; private set; }

        public bool Revealed { get; private set; }

        GameObject _model;
        Follow.Game.PhotoSubject _subject;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            // The subject exists in the scene but is not visible until pointed at.
            if (species != null && species.modelPrefab != null)
            {
                _model = Instantiate(species.modelPrefab, transform.position, transform.rotation, transform);
                _model.transform.localScale = Vector3.one * Mathf.Max(0.01f, species.worldScale);
                foreach (var c in _model.GetComponentsInChildren<Collider>()) Destroy(c);

                // Without a controller the model imports as a T-pose, which is the single
                // most embarrassing thing a photographed animal could be doing.
                var animator = _model.GetComponentInChildren<Animator>();
                if (animator != null && species.animator != null)
                {
                    animator.runtimeAnimatorController = species.animator;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }

                SetVisible(false);
            }
        }

        void SetVisible(bool visible)
        {
            if (_model == null) return;
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>The dog has caught it. The animal becomes visible as the player closes in.</summary>
        public void Reveal()
        {
            if (Revealed) return;
            Revealed = true;
            SetVisible(true);

            // It only becomes photographable once it has been found. That is the whole
            // reason the dog is not decorative.
            if (_model == null || _subject != null) return;
            _subject = _model.AddComponent<Follow.Game.PhotoSubject>();
            _subject.species = species;
            _subject.wariness = species.wariness;
            _subject.OnLeave(Consume);
        }

        /// <summary>Photographed, or the animal gave up waiting.</summary>
        public void Consume()
        {
            Consumed = true;
            SetVisible(false);
        }

        public bool AvailableTo(float bond) => !Consumed && bond >= bondRequired;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, scentRadius);
        }
    }
}
