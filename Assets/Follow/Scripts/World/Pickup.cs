using System.Collections.Generic;
using UnityEngine;
using Follow.Core;
using Follow.Game;

namespace Follow.World
{
    public enum PickupKind { Stick, Forage }

    /// <summary>
    /// A branch or a mushroom lying on the ground. Walking over it is enough - asking for
    /// a keypress on every twig would turn gathering firewood into paperwork.
    ///
    /// Chunks are rebuilt from the same seed whenever you walk back, so a picked-up stick
    /// would otherwise return. <see cref="PickupLedger"/> remembers what is gone by where
    /// it stood, which costs one hash per pickup instead of a saved object per pickup.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        /// <summary>
        /// Everything currently lying on the ground.
        ///
        /// The dog used to look for an errand with a whole-scene FindObjectsByType, which
        /// runs while she is ranging - every frame, allocating an array of every pickup in
        /// three hundred metres of streamed forest. A register costs one list insertion
        /// per stick and nothing at all per frame.
        /// </summary>
        static readonly List<Pickup> All = new List<Pickup>();
        public static IReadOnlyList<Pickup> Active => All;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public PickupKind kind = PickupKind.Stick;
        public int amount = 1;
        [Tooltip("Generous on purpose. Hunting for the exact centimetre is not a mechanic.")]
        public float reach = 2.3f;

        [Tooltip("Seconds of idle bobbing before it settles. Purely so it catches the eye.")]
        public float announce = 0.6f;

        long _id;
        float _spin;
        float _phase;
        Vector3 _restScale;
        Vector3 _rest;
        bool _taken;

        void Start()
        {
            _id = PickupLedger.Id(transform.position);
            if (PickupLedger.Taken(_id)) { Destroy(gameObject); return; }

            _restScale = transform.localScale;
            _phase = Random.value * 10f;
            // Settle onto the ground properly; the scatter grid only knows the height field.
            WorldStreamer.Drop(transform, 0.06f);
            _rest = transform.position;
        }

        void Update()
        {
            if (_taken) return;

            var player = PlayerMover.Instance;
            if (player == null) return;

            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            float d = flat.magnitude;

            // It always breathes a little, and lifts when you are close. A stick lying
            // brown-on-green is invisible otherwise, which is not a fair thing to hide a
            // required resource behind.
            float notice = d < reach * 4f ? 1f : 0.25f;
            _spin = Mathf.Lerp(_spin, notice, Time.deltaTime * 4f);

            float bob = Mathf.Sin(Time.time * 2.2f + _phase) * 0.5f + 0.5f;
            transform.localScale = _restScale * (1f + bob * 0.07f * _spin);
            transform.position = _rest + Vector3.up * (bob * 0.09f * _spin);
            transform.Rotate(Vector3.up, 22f * _spin * Time.deltaTime, Space.World);

            if (d > reach) return;
            Take();
        }

        void Take()
        {
            _taken = true;
            PickupLedger.Mark(_id);

            var state = GameState.Instance;
            if (state != null)
            {
                if (kind == PickupKind.Stick) state.AddSticks(amount);
                else state.AddFood(amount);
            }

            var hud = Follow.UI.GameHud.Instance;
            if (hud != null)
                hud.Say(kind == PickupKind.Stick
                    ? (amount > 1 ? amount + " sticks" : "a stick for the fire")
                    : "something to eat");

            Destroy(gameObject);
        }

        /// <summary>
        /// The dog has it in her mouth. It leaves the world now, but nothing is credited
        /// until she reaches you - otherwise the reward arrives before the delivery and
        /// the whole errand reads as a lie.
        /// </summary>
        public void TakenByDog()
        {
            _taken = true;
            PickupLedger.Mark(_id);
            Destroy(gameObject);
        }

        /// <summary>Straightforward collection, for anything that is not the dog.</summary>
        public void Collect() => Take();
    }

    /// <summary>
    /// What has already been gathered. Keyed by position, because the world regenerates
    /// from its coordinates and so does everything standing on it.
    /// </summary>
    public static class PickupLedger
    {
        static readonly HashSet<long> _taken = new HashSet<long>();

        /// <summary>Quantised to a third of a metre - far finer than two pickups ever sit.</summary>
        public static long Id(Vector3 position)
        {
            long x = Mathf.RoundToInt(position.x * 3f);
            long z = Mathf.RoundToInt(position.z * 3f);
            return (x << 32) ^ (z & 0xFFFFFFFFL);
        }

        public static bool Taken(long id) => _taken.Contains(id);
        public static void Mark(long id) => _taken.Add(id);

        /// <summary>A new day puts fresh windfall on the forest floor.</summary>
        public static void Clear() => _taken.Clear();
    }
}
