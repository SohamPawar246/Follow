using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Follow.Core;
using Follow.Data;
using Follow.Dog;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// Keeps the forest stocked with things only the dog can find.
    ///
    /// Scent points used to be seeded once into a map with edges. In an endless forest
    /// there is no "once" and no edge, so instead a handful of subjects are kept alive in
    /// a ring around the player: far enough that you cannot simply walk to them, close
    /// enough that the dog's range reaches them. Walk a kilometre and the ring travels
    /// with you, so the wood is never empty and never crowded.
    ///
    /// The day's survey list is weighted heavily, because a list you cannot complete is
    /// worse than no list at all.
    /// </summary>
    public class ScentField : MonoBehaviour
    {
        [Header("Population")]
        [Tooltip("Unconsumed subjects kept alive around the player.")]
        public int target = 12;

        [Header("Ring, in metres")]
        public float minSpawn = 38f;
        public float maxSpawn = 95f;
        [Tooltip("Beyond this the subject is retired - it wandered off.")]
        public float retireRadius = 165f;

        [Tooltip("Share of subjects placed near water, where animals actually go.")]
        [Range(0f, 1f)] public float pondBias = 0.45f;

        public float reviewInterval = 1.5f;

        readonly List<ScentPoint> _mine = new List<ScentPoint>();
        Transform _root;
        float _timer;
        System.Random _rng;

        void Start()
        {
            _root = new GameObject("ScentPoints").transform;
            _rng = new System.Random(unchecked(GameState.Ensure().day * 7919 + 31));
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

            var held = DogBrain.Instance != null ? DogBrain.Instance.Find : null;

            for (int i = _mine.Count - 1; i >= 0; i--)
            {
                var point = _mine[i];
                if (point == null) { _mine.RemoveAt(i); continue; }

                bool gone = point.Consumed;
                bool tooFar = Vector3.Distance(point.transform.position, here) > retireRadius;
                // Never retire the one the dog is standing over; that is the payoff.
                if (!gone && (!tooFar || point == held)) continue;

                _mine.RemoveAt(i);
                Destroy(point.gameObject);
            }

            int missing = target - _mine.Count;
            for (int i = 0; i < missing; i++) TrySpawn(here);
        }

        void TrySpawn(Vector3 around)
        {
            var species = PickSpecies();
            if (species == null) return;

            for (int attempt = 0; attempt < 14; attempt++)
            {
                Vector2 at;

                // Water first: a pond is where you would actually look for an animal, and
                // it gives the player a reason to walk toward the landmark they can see.
                if (_rng.NextDouble() < pondBias &&
                    WorldComposer.NearestPond(new Vector2(around.x, around.z), maxSpawn, out var pond))
                {
                    float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                    float ring = pond.radius * Mathf.Lerp(1.3f, 2.2f, (float)_rng.NextDouble());
                    at = pond.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                }
                else
                {
                    float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                    float radius = Mathf.Lerp(minSpawn, maxSpawn, (float)_rng.NextDouble());
                    at = new Vector2(around.x, around.z)
                       + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }

                float d = Vector2.Distance(at, new Vector2(around.x, around.z));
                if (d < minSpawn * 0.7f || d > maxSpawn * 1.3f) continue;
                if (!WorldComposer.IsWalkable(at.x, at.y)) continue;
                if (Crowded(at)) continue;

                Place(species, at);
                return;
            }
        }

        bool Crowded(Vector2 at)
        {
            foreach (var point in _mine)
            {
                if (point == null) continue;
                var p = point.transform.position;
                if ((new Vector2(p.x, p.z) - at).sqrMagnitude < 18f * 18f) return true;
            }
            return false;
        }

        /// <summary>
        /// Two thirds from today's list, one third from anything. The list has to be
        /// findable, but a forest that only contains your homework is not a forest.
        /// </summary>
        SpeciesData PickSpecies()
        {
            var library = SpeciesLibrary.Active;
            var state = GameState.Instance;
            if (library == null || state == null) return null;

            var fauna = library.AvailableOn(state.day)
                .Where(s => s != null && s.kind == SpeciesKind.Fauna && s.modelPrefab != null)
                .ToList();
            if (fauna.Count == 0) return null;

            if (_rng.NextDouble() < 0.66f)
            {
                var wanted = library.BuildSurveyList(state.day, 3, 2)
                    .Where(s => s != null && s.kind == SpeciesKind.Fauna
                             && s.modelPrefab != null && !state.album.Has(s.id))
                    .ToList();
                if (wanted.Count > 0) return wanted[_rng.Next(wanted.Count)];
            }
            return fauna[_rng.Next(fauna.Count)];
        }

        void Place(SpeciesData species, Vector2 at)
        {
            var go = new GameObject("Scent_" + species.id);
            go.transform.SetParent(_root, true);
            go.transform.position = new Vector3(at.x, WorldComposer.Height(at.x, at.y), at.y);
            go.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);

            var scent = go.AddComponent<ScentPoint>();
            scent.species = species;
            // Rarer species hide better and need a dog that trusts you more.
            scent.scentRadius = Mathf.Lerp(20f, 11f, species.rarity);
            scent.bondRequired = Mathf.Clamp01(species.rarity - 0.35f);
            scent.patience = Mathf.Lerp(22f, 11f, species.rarity);

            _mine.Add(scent);
        }

        /// <summary>Clears the board so tomorrow is not yesterday's leftovers.</summary>
        public void NewDay()
        {
            foreach (var point in _mine) if (point != null) Destroy(point.gameObject);
            _mine.Clear();
            _rng = new System.Random(unchecked(GameState.Ensure().day * 7919 + 31));
            Review();
        }
    }
}
