using System.Collections.Generic;
using UnityEngine;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// Thins out whatever is standing between the camera and the surveyor.
    ///
    /// A forest dense enough to be worth walking through is dense enough to swallow the
    /// character, and the honest fix is to move the trees out of the way rather than to
    /// draw a glowing cut-out of the player on top of them - which was the earlier attempt,
    /// and which looked like a bug because the overlay fought the character's own depth.
    ///
    /// The trees fade with an ordered dither in the foliage shader, so they stay opaque
    /// geometry the whole time. Screen-door transparency is the right tool here: real alpha
    /// would put every leaf into the transparent queue, where the canopy sorts against
    /// itself and flickers.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class CanopyFade : MonoBehaviour
    {
        [Tooltip("How thin an obstructing plant gets. Zero would remove it entirely.")]
        [Range(0f, 1f)] public float fadeTo = 0.22f;

        [Tooltip("Radius of the sweep from the camera to the player, in metres.")]
        public float sweepRadius = 1.6f;

        [Tooltip("Seconds to fade out, and to come back.")]
        public float fadeOut = 0.12f;
        public float fadeIn = 0.35f;

        static readonly int FadeId = Shader.PropertyToID("_Fade");

        readonly Dictionary<Renderer, float> _faded = new Dictionary<Renderer, float>();
        readonly List<Renderer> _hitThisFrame = new List<Renderer>(16);
        readonly List<Renderer> _finished = new List<Renderer>(8);
        readonly RaycastHit[] _hits = new RaycastHit[24];

        MaterialPropertyBlock _block;
        Camera _camera;
        int _mask;

        /// <summary>How many renderers are currently thinned. Read by the editor probe.</summary>
        public int Fading { get; private set; }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            int canopy = LayerMask.NameToLayer("Canopy");
            _mask = canopy < 0 ? 0 : 1 << canopy;
        }

        void LateUpdate()
        {
            if (_mask == 0) return;

            var player = PlayerMover.Instance;
            if (player == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            _hitThisFrame.Clear();
            Sweep(player.transform.position + Vector3.up * 1.1f);
            Fading = _hitThisFrame.Count;

            float dt = Time.deltaTime;

            // Anything in the sweep heads for the faded value; everything else heads back
            // to solid, and drops out of the dictionary once it gets there.
            foreach (var renderer in _hitThisFrame)
                if (!_faded.ContainsKey(renderer)) _faded[renderer] = 1f;

            _finished.Clear();
            var keys = new List<Renderer>(_faded.Keys);

            foreach (var renderer in keys)
            {
                if (renderer == null) { _finished.Add(renderer); continue; }

                bool blocking = _hitThisFrame.Contains(renderer);
                float target = blocking ? fadeTo : 1f;
                float speed = blocking ? fadeOut : fadeIn;

                float value = Mathf.MoveTowards(_faded[renderer], target, dt / Mathf.Max(0.01f, speed));
                _faded[renderer] = value;

                renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, value);
                renderer.SetPropertyBlock(_block);

                if (!blocking && value >= 0.999f) _finished.Add(renderer);
            }

            foreach (var renderer in _finished)
            {
                if (renderer != null)
                {
                    // Clear the override, so the tree goes back to being SRP-batched.
                    renderer.GetPropertyBlock(_block);
                    _block.SetFloat(FadeId, 1f);
                    renderer.SetPropertyBlock(_block);
                }
                _faded.Remove(renderer);
            }
        }

        void Sweep(Vector3 target)
        {
            Vector3 from = _camera.transform.position;
            Vector3 to = target;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return;

            int count = Physics.SphereCastNonAlloc(from, sweepRadius, delta / distance, _hits,
                distance, _mask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i].collider;
                if (hit == null) continue;

                // The crown trigger is an empty child; the geometry to thin is its sibling,
                // so this has to climb to the plant's root before collecting renderers.
                var root = hit.transform.parent != null ? hit.transform.parent : hit.transform;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    if (!_hitThisFrame.Contains(renderer)) _hitThisFrame.Add(renderer);
            }
        }
    }
}
