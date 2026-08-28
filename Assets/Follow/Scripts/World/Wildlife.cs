using System.Collections.Generic;
using UnityEngine;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// The forest's background life: flocks of small birds on the ground, and butterflies
    /// over the open patches.
    ///
    /// None of this is photographable and none of it is a resource. It exists because a
    /// wood with no visible animal in it does not feel like a wood, however many animals
    /// are technically hidden in the scent field waiting to be found. The species the dog
    /// points at stay rare and stay hers; these are the ones you simply see, and the fact
    /// that they scatter when you or the dog get close is what makes them read as alive
    /// rather than as scenery that happens to be bird-shaped.
    ///
    /// Everything here is kept in a ring around the player and recycled, so walking for an
    /// hour never accumulates a single object.
    /// </summary>
    public class Wildlife : MonoBehaviour
    {
        [Header("Ground flocks")]
        public int flocks = 3;
        public Vector2Int birdsPerFlock = new Vector2Int(3, 6);
        public float minSpawn = 16f;
        public float maxSpawn = 46f;
        public float retireRadius = 90f;
        [Tooltip("How close before the whole flock goes up at once.")]
        public float flushDistance = 7.5f;

        [Header("Butterflies")]
        public int butterflies = 7;

        [Header("Look")]
        public float birdScale = 0.42f;

        readonly List<Flock> _flocks = new List<Flock>();
        readonly List<Flutter> _flutters = new List<Flutter>();
        Transform _root;
        System.Random _rng;
        float _timer;

        class Bird
        {
            public Transform t;
            public float phase;
            public float peckAt;
            public Vector3 home;
            public Vector3 drift;
        }

        class Flock
        {
            public readonly List<Bird> birds = new List<Bird>();
            public Vector3 centre;
            public bool flushed;
            public float flushedFor;
            public Vector3 escape;
        }

        class Flutter
        {
            public Transform t;
            public Transform leftWing;
            public Transform rightWing;
            public Vector3 anchor;
            public float phase;
            public float speed;
        }

        void Start()
        {
            _root = new GameObject("Wildlife").transform;
            _rng = new System.Random(20260828);
            for (int i = 0; i < butterflies; i++) _flutters.Add(MakeButterfly());
            Review();
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }

        void Update()
        {
            float dt = Time.deltaTime;

            _timer -= dt;
            if (_timer <= 0f) { _timer = 1.5f; Review(); }

            var player = PlayerMover.Instance;
            Vector3 here = player != null ? player.transform.position : Vector3.zero;
            Vector3 dogAt = Follow.Dog.DogBrain.Instance != null
                ? Follow.Dog.DogBrain.Instance.transform.position
                : here + Vector3.one * 9999f;

            for (int i = 0; i < _flocks.Count; i++) StepFlock(_flocks[i], dt, here, dogAt);
            for (int i = 0; i < _flutters.Count; i++) StepButterfly(_flutters[i], dt, here);
        }

        // --- flocks -------------------------------------------------------------------

        void Review()
        {
            var player = PlayerMover.Instance;
            if (player == null) return;
            Vector3 here = player.transform.position;

            for (int i = _flocks.Count - 1; i >= 0; i--)
            {
                var flock = _flocks[i];
                bool gone = flock.flushed && flock.flushedFor > 3.2f;
                bool far = Vector3.Distance(flock.centre, here) > retireRadius;
                if (!gone && !far) continue;

                foreach (var bird in flock.birds) if (bird.t != null) Destroy(bird.t.gameObject);
                _flocks.RemoveAt(i);
            }

            while (_flocks.Count < flocks)
            {
                var flock = MakeFlock(here);
                if (flock == null) break;
                _flocks.Add(flock);
            }
        }

        Flock MakeFlock(Vector3 around)
        {
            var model = Model();
            if (model == null) return null;

            // Open ground only. Birds under a closed canopy are birds nobody ever sees.
            Vector2 at = Vector2.zero;
            bool found = false;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minSpawn, maxSpawn, (float)_rng.NextDouble());
                at = new Vector2(around.x, around.z)
                   + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (!WorldComposer.IsWalkable(at.x, at.y)) continue;
                if (WorldComposer.Density(at.x, at.y) > 0.4f) continue;
                found = true;
                break;
            }
            if (!found) return null;

            var flock = new Flock
            {
                centre = new Vector3(at.x, WorldComposer.Height(at.x, at.y), at.y)
            };

            int count = _rng.Next(birdsPerFlock.x, birdsPerFlock.y + 1);
            for (int i = 0; i < count; i++)
            {
                var offset = new Vector2(
                    (float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f) * 5.5f;
                Vector3 spot = flock.centre + new Vector3(offset.x, 0f, offset.y);
                spot.y = WorldComposer.Height(spot.x, spot.z);

                var go = Instantiate(model, spot, Quaternion.Euler(
                    0f, (float)_rng.NextDouble() * 360f, 0f), _root);
                go.name = "Bird";
                go.transform.localScale = Vector3.one * birdScale;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

                flock.birds.Add(new Bird
                {
                    t = go.transform,
                    home = spot,
                    phase = (float)_rng.NextDouble() * 10f,
                    peckAt = (float)_rng.NextDouble() * 3f,
                    drift = Vector3.zero
                });
            }

            return flock.birds.Count > 0 ? flock : null;
        }

        void StepFlock(Flock flock, float dt, Vector3 player, Vector3 dog)
        {
            if (!flock.flushed)
            {
                float toPlayer = Vector3.Distance(flock.centre, player);
                float toDog = Vector3.Distance(flock.centre, dog);
                if (toPlayer < flushDistance || toDog < flushDistance)
                {
                    flock.flushed = true;
                    flock.flushedFor = 0f;

                    // Away from whoever startled them, which is what makes the dog
                    // running through a flock look like the dog doing it.
                    Vector3 from = toDog < toPlayer ? dog : player;
                    Vector3 away = flock.centre - from;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                    flock.escape = away.normalized;

                    Soundscape.Instance?.Flush(flock.centre);
                }
            }

            if (flock.flushed) flock.flushedFor += dt;

            foreach (var bird in flock.birds)
            {
                if (bird.t == null) continue;
                bird.phase += dt;

                if (!flock.flushed)
                {
                    // Pecking: a small forward-and-down dip on an irregular beat, plus a
                    // hop to a new spot now and then. Two motions is all a bird needs.
                    bird.peckAt -= dt;
                    if (bird.peckAt <= 0f)
                    {
                        bird.peckAt = 1.4f + (float)_rng.NextDouble() * 2.6f;
                        var hop = new Vector2(
                            (float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f) * 2.4f;
                        Vector3 want = bird.home + new Vector3(hop.x, 0f, hop.y);
                        want.y = WorldComposer.Height(want.x, want.z);
                        if (Vector3.Distance(want, flock.centre) < 6f) bird.home = want;
                    }

                    Vector3 to = bird.home - bird.t.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.02f)
                    {
                        bird.t.position += to.normalized * Mathf.Min(1.6f * dt, to.magnitude);
                        bird.t.rotation = Quaternion.Slerp(bird.t.rotation,
                            Quaternion.LookRotation(to.normalized, Vector3.up), dt * 6f);
                    }

                    float peck = Mathf.Max(0f, Mathf.Sin(bird.phase * 5.5f)) *
                                 Mathf.Max(0f, Mathf.Sin(bird.phase * 0.8f));
                    var p = bird.t.position;
                    p.y = WorldComposer.Height(p.x, p.z) + peck * 0.06f;
                    bird.t.position = p;
                    continue;
                }

                // Up and away, climbing as they go and banking into the turn.
                float climb = Mathf.Min(flock.flushedFor, 3.2f);
                bird.drift = Vector3.Lerp(bird.drift,
                    flock.escape * 9f + Vector3.up * 4.2f, dt * 2.4f);
                bird.t.position += bird.drift * dt;

                Vector3 heading = bird.drift.normalized;
                if (heading.sqrMagnitude > 0.01f)
                    bird.t.rotation = Quaternion.Slerp(bird.t.rotation,
                        Quaternion.LookRotation(heading, Vector3.up) *
                        Quaternion.Euler(0f, 0f, Mathf.Sin(bird.phase * 12f) * 18f), dt * 8f);

                // Fade out as they go, so nobody watches a bird pop out of existence.
                float k = 1f - Mathf.Clamp01((climb - 1.8f) / 1.4f);
                bird.t.localScale = Vector3.one * birdScale * Mathf.Max(0.01f, k);
            }
        }

        GameObject Model()
        {
            var palette = WorldPalette.Active;
            if (palette == null || palette.birdModels == null || palette.birdModels.Count == 0)
                return null;
            return palette.birdModels[_rng.Next(palette.birdModels.Count)];
        }

        // --- butterflies ---------------------------------------------------------------

        /// <summary>
        /// Two hinged quads and a body. Small enough that a model would be wasted on it,
        /// and building it here means the wings can actually beat.
        /// </summary>
        Flutter MakeButterfly()
        {
            var go = new GameObject("Butterfly");
            go.transform.SetParent(_root, false);

            Color wing = _rng.Next(3) switch
            {
                0 => new Color(1f, 0.83f, 0.35f),
                1 => new Color(0.98f, 0.62f, 0.75f),
                _ => new Color(0.72f, 0.86f, 1f)
            };

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.03f, 0.03f, 0.11f);
            Paint(body, new Color(0.22f, 0.17f, 0.13f));

            var flutter = new Flutter
            {
                t = go.transform,
                leftWing = Wing(go.transform, -1f, wing),
                rightWing = Wing(go.transform, 1f, wing),
                phase = (float)_rng.NextDouble() * 20f,
                speed = 0.7f + (float)_rng.NextDouble() * 0.5f
            };
            return flutter;
        }

        Transform Wing(Transform parent, float side, Color color)
        {
            var hinge = new GameObject(side < 0f ? "WingL" : "WingR").transform;
            hinge.SetParent(parent, false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(hinge, false);
            quad.transform.localPosition = new Vector3(side * 0.075f, 0f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(0.15f, 0.13f, 1f);
            Paint(quad, color);
            return hinge;
        }

        static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        void StepButterfly(Flutter f, float dt, Vector3 player)
        {
            if (f.t == null) return;
            f.phase += dt * f.speed;

            // Re-anchor when the player has walked away, so they are always somewhere
            // just off the path rather than clustered where the game started.
            if (f.anchor == Vector3.zero || Vector3.Distance(f.anchor, player) > 40f)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float radius = 6f + (float)_rng.NextDouble() * 16f;
                var at = new Vector2(player.x, player.z)
                       + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                f.anchor = new Vector3(at.x, WorldComposer.Height(at.x, at.y), at.y);
                f.phase = (float)_rng.NextDouble() * 20f;
            }

            // A wandering figure-of-eight, never quite repeating.
            float t = f.phase;
            var drift = new Vector3(
                Mathf.Sin(t * 0.9f) * 2.2f + Mathf.Sin(t * 0.31f) * 1.4f,
                0f,
                Mathf.Sin(t * 1.27f) * 1.8f + Mathf.Cos(t * 0.4f) * 1.2f);

            Vector3 want = f.anchor + drift;
            want.y = WorldComposer.Height(want.x, want.z)
                   + 0.75f + Mathf.Sin(t * 2.1f) * 0.35f;

            Vector3 heading = want - f.t.position;
            f.t.position = Vector3.Lerp(f.t.position, want, 1f - Mathf.Exp(-dt * 2.2f));
            if (heading.sqrMagnitude > 0.001f)
                f.t.rotation = Quaternion.Slerp(f.t.rotation,
                    Quaternion.LookRotation(heading.normalized, Vector3.up), dt * 4f);

            // The beat is what sells it. Fast, uneven, never fully closed.
            float beat = Mathf.Sin(t * 15f) * 55f + 25f;
            f.leftWing.localRotation = Quaternion.Euler(0f, 0f, beat);
            f.rightWing.localRotation = Quaternion.Euler(0f, 0f, -beat);
        }
    }
}
