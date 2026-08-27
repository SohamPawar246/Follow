using System;
using System.Collections.Generic;
using UnityEngine;

namespace Follow.Core
{
    /// <summary>
    /// Everything that survives a scene change. There is no ending, so this is a day
    /// counter and a growing album rather than a level pointer.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("Journey")]
        public int day = 1;

        [Header("The dog")]
        [Range(0f, 1f)] public float bond = 0.12f;
        [Range(0f, 1f)] public float dogHunger = 0.3f;
        [Range(0f, 1f)] public float dogEnergy = 1f;

        [Header("You")]
        [Tooltip("Spent by walking and surveying, restored by eating and sleeping.")]
        [Range(0f, 1f)] public float energy = 1f;
        [Tooltip("How well fed you are. Empties over a day; a ration refills it.")]
        [Range(0f, 1f)] public float nourishment = 1f;
        [Tooltip("Empties faster than food. Any pond fills it instantly.")]
        [Range(0f, 1f)] public float hydration = 1f;

        [Header("Camp")]
        public int sticks = 0;
        public int food = 0;

        [Tooltip("Set once the fire has been built. The plot never comes back.")]
        public bool campfireBuilt;
        [Tooltip("Seconds of burn left. Zero means the fire is out, not that it is gone.")]
        public float campfireFuel;

        [Header("Rules")]
        [Tooltip("The dog cannot be lost before this day. Keeps a five-minute demo safe (GDD).")]
        public int graceDays = 3;
        [Tooltip("Bond floor while inside the grace period.")]
        public float graceBondFloor = 0.2f;

        public readonly Album album = new Album();

        public event Action<int> DayChanged;
        public event Action<float> BondChanged;

        /// <summary>
        /// Raised whenever a counter moves, so the interface can float the number where
        /// the player is already looking. Nothing should change sticks or food directly.
        /// </summary>
        public event Action<Track, int> Gained;

        public enum Track { Sticks, Food, Nourishment, Hydration, Energy, DogFed }

        public bool InGracePeriod => day <= graceDays;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static GameState Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<GameState>();
            if (existing != null) return existing;
            return new GameObject("GameState").AddComponent<GameState>();
        }

        public void NewRun()
        {
            day = 1;
            bond = 0.12f;
            dogHunger = 0.3f;
            dogEnergy = 1f;
            sticks = 0;
            food = 0;
            energy = 1f;
            nourishment = 1f;
            hydration = 1f;
            campfireBuilt = false;
            campfireFuel = 0f;
            album.Clear();
            DayChanged?.Invoke(day);
            BondChanged?.Invoke(bond);
        }

        public void AddSticks(int amount)
        {
            if (amount == 0) return;
            sticks = Mathf.Max(0, sticks + amount);
            Gained?.Invoke(Track.Sticks, amount);
        }

        public void AddFood(int amount)
        {
            if (amount == 0) return;
            food = Mathf.Max(0, food + amount);
            Gained?.Invoke(Track.Food, amount);
        }

        /// <summary>Announces a change to a bar as a percentage, for the floating number.</summary>
        public void Announce(Track track, float delta01)
        {
            int shown = Mathf.RoundToInt(delta01 * 100f);
            if (shown != 0) Gained?.Invoke(track, shown);
        }

        public void AdvanceDay()
        {
            day++;
            DayChanged?.Invoke(day);
        }

        /// <summary>Bond moves in both directions now, but never below the grace floor early on.</summary>
        public void AddBond(float delta)
        {
            float floor = InGracePeriod ? graceBondFloor : 0f;
            float next = Mathf.Clamp(bond + delta, floor, 1f);
            if (Mathf.Approximately(next, bond)) return;
            bond = next;
            BondChanged?.Invoke(bond);
        }

        /// <summary>Metres the dog will voluntarily range. A confident dog works wider (GDD).</summary>
        public float DogTether => Mathf.Lerp(9f, 34f, bond);

        /// <summary>How far from the fire it settles at night. The only place bond is ever shown.</summary>
        public float CampfireDistance => Mathf.Lerp(9f, 0.6f, Mathf.SmoothStep(0f, 1f, bond));
    }

    /// <summary>The score, the memory, and the reason to keep playing.</summary>
    [Serializable]
    public class Album
    {
        public readonly Dictionary<string, AlbumEntry> entries = new Dictionary<string, AlbumEntry>();

        public int Count => entries.Count;

        public void Clear() => entries.Clear();

        /// <summary>Only the best shot per species is kept, so re-photographing is always worth doing.</summary>
        public bool Record(string speciesId, float score, Texture2D photo, int dayTaken)
        {
            if (entries.TryGetValue(speciesId, out var existing) && existing.score >= score) return false;
            entries[speciesId] = new AlbumEntry
            {
                speciesId = speciesId,
                score = score,
                photo = photo,
                dayTaken = dayTaken
            };
            return true;
        }

        /// <summary>Throws a photograph away. There is no undo, which is why it is asked twice.</summary>
        public bool Remove(string speciesId) => entries.Remove(speciesId);

        public bool Has(string speciesId) => entries.ContainsKey(speciesId);
        public AlbumEntry Get(string speciesId) => entries.TryGetValue(speciesId, out var e) ? e : null;
        public float Completion(int totalSpecies) => totalSpecies <= 0 ? 0f : (float)Count / totalSpecies;
    }

    [Serializable]
    public class AlbumEntry
    {
        public string speciesId;
        public float score;
        public int dayTaken;
        public Texture2D photo;

        public PhotoGrade Grade => PhotoGrading.From(score);
    }

    public enum PhotoGrade { Recorded, Good, Fine, FieldGuide }

    public static class PhotoGrading
    {
        public static PhotoGrade From(float score)
        {
            if (score >= 0.85f) return PhotoGrade.FieldGuide;
            if (score >= 0.65f) return PhotoGrade.Fine;
            if (score >= 0.40f) return PhotoGrade.Good;
            return PhotoGrade.Recorded;
        }

        public static string Name(PhotoGrade g) => g switch
        {
            PhotoGrade.FieldGuide => "Field Guide",
            PhotoGrade.Fine => "Fine",
            PhotoGrade.Good => "Good",
            _ => "Recorded"
        };
    }
}
