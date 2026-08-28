using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Follow.Core;
using Follow.Data;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// Keeps a handful of photographable plants standing around the player.
    ///
    /// The same ring idea as <see cref="ScentField"/>, but closer and in the open: flora
    /// has to be spotted rather than sniffed out, so a specimen that spawns inside a
    /// thicket is a specimen that never gets found. Each species is placed where it would
    /// actually grow - ferns in the damp, rhododendron on the high ground.
    /// </summary>
    public class FloraField : MonoBehaviour
    {
        [Header("Population")]
        public int target = 6;

        [Header("Ring, in metres")]
        public float minSpawn = 22f;
        public float maxSpawn = 78f;
        public float retireRadius = 150f;

        public float reviewInterval = 2f;

        readonly List<FloraSpecimen> _mine = new List<FloraSpecimen>();
        Transform _root;
        float _timer;
        System.Random _rng;

        void Start()
        {
            _root = new GameObject("FloraSpecimens").transform;
            _rng = new System.Random(unchecked(GameState.Ensure().day * 5231 + 17));
            Review();
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = reviewInterval;
            Review();
        }

        void Review()
        {
            var player = PlayerMover.Instance;
            if (player == null) return;
            Vector3 here = player.transform.position;

            for (int i = _mine.Count - 1; i >= 0; i--)
            {
                var specimen = _mine[i];
                if (specimen == null) { _mine.RemoveAt(i); continue; }

                var subject = specimen.GetComponent<PhotoSubject>();
                if (subject != null && subject.Busy) continue;

                bool done = subject != null && subject.Photographed;
                bool far = Vector3.Distance(specimen.transform.position, here) > retireRadius;
                if (!done && !far) continue;

                _mine.RemoveAt(i);
                Destroy(specimen.gameObject);
            }

            for (int i = _mine.Count; i < target; i++) TrySpawn(here);
        }

        void TrySpawn(Vector3 around)
        {
            var species = Pick();
            if (species == null) return;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minSpawn, maxSpawn, (float)_rng.NextDouble());
                var at = new Vector2(around.x, around.z)
                       + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (!WorldComposer.IsWalkable(at.x, at.y)) continue;
                // In the open, or on the fringe of it. Never buried in a thicket.
                if (WorldComposer.Density(at.x, at.y) > 0.55f) continue;
                if (!Suits(species, at)) continue;
                if (Crowded(at)) continue;

                var specimen = FloraSpecimen.Spawn(species,
                    new Vector3(at.x, WorldComposer.Height(at.x, at.y) - 0.05f, at.y), _root);
                if (specimen != null) _mine.Add(specimen);
                return;
            }
        }

        /// <summary>Each plant wants the ground it would really be found on.</summary>
        static bool Suits(SpeciesData species, Vector2 at)
        {
            var zone = WorldComposer.ZoneAt(at.x, at.y);
            switch (species.id)
            {
                case "tree_fern": return zone == WorldComposer.Zone.Damp;
                case "rhododendron": return zone == WorldComposer.Zone.Pine
                                         || zone == WorldComposer.Zone.Rocky;
                case "blue_vanda": return WorldComposer.Density(at.x, at.y) > 0.15f;
                default: return true;
            }
        }

        bool Crowded(Vector2 at)
        {
            foreach (var specimen in _mine)
            {
                if (specimen == null) continue;
                var p = specimen.transform.position;
                if ((new Vector2(p.x, p.z) - at).sqrMagnitude < 26f * 26f) return true;
            }
            return false;
        }

        SpeciesData Pick()
        {
            var library = SpeciesLibrary.Active;
            var state = GameState.Instance;
            if (library == null || state == null) return null;

            var flora = library.AvailableOn(state.day)
                .Where(s => s != null && s.kind == SpeciesKind.Flora && s.modelPrefab != null)
                .ToList();
            if (flora.Count == 0) return null;

            // Anything still missing from the album comes first.
            var wanted = flora.Where(s => !state.album.Has(s.id)).ToList();
            var pool = wanted.Count > 0 && _rng.NextDouble() < 0.75 ? wanted : flora;
            return pool[_rng.Next(pool.Count)];
        }

        public void NewDay()
        {
            foreach (var specimen in _mine) if (specimen != null) Destroy(specimen.gameObject);
            _mine.Clear();
            _rng = new System.Random(unchecked(GameState.Ensure().day * 5231 + 17));
            Review();
        }
    }
}
