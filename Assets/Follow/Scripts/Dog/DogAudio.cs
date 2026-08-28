using UnityEngine;

namespace Follow.Dog
{
    /// <summary>
    /// The dog's voice and feet.
    ///
    /// The bark is the single most important sound in the game: it is how a find gets
    /// communicated, and it has to be locatable by ear alone. It therefore carries much
    /// further than anything else and sits on its own source.
    ///
    /// A real bark is loaded from Assets/Follow/Audio/Dog and used whenever one is there.
    /// The synthesised voice below is the fallback for when it is not: none of the asset
    /// packs contains a dog, so for a long time these arrays were simply empty and the
    /// mechanic the whole game is built on made no sound at all. Generating something
    /// plausible costs a few milliseconds at load and means that can never happen again.
    /// </summary>
    [RequireComponent(typeof(DogBrain))]
    public class DogAudio : MonoBehaviour
    {
        [Header("Clips (left empty, these are generated)")]
        public AudioClip[] barks;
        public AudioClip[] pants;
        public AudioClip whine;
        public AudioClip[] footsteps;

        [Header("Mix")]
        public float barkVolume = 0.8f;
        public float footVolume = 0.24f;
        [Tooltip("Barks carry a long way. Footsteps should not.")]
        public float barkRange = 85f;
        public float footRange = 16f;

        DogBrain _brain;
        AudioSource _voice;
        AudioSource _feet;
        float _stride;
        float _pantTimer;

        const int Rate = 44100;

        void Awake()
        {
            _brain = GetComponent<DogBrain>();

            _voice = gameObject.AddComponent<AudioSource>();
            Configure(_voice, barkRange);
            _feet = gameObject.AddComponent<AudioSource>();
            Configure(_feet, footRange);

            if (barks == null || barks.Length == 0)
                barks = new[] { Bark(1f), Bark(1.08f), Bark(0.93f) };
            if (pants == null || pants.Length == 0)
                pants = new[] { Pant() };
            if (whine == null) whine = Whine();

            _brain.Barked += Woof;
            _brain.Pointed += OnPointed;
        }

        void OnDestroy()
        {
            if (_brain == null) return;
            _brain.Barked -= Woof;
            _brain.Pointed -= OnPointed;
        }

        void OnPointed(ScentPoint point) => Woof();

        void Configure(AudioSource s, float range)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = range;
            s.dopplerLevel = 0.05f;
        }

        void Woof()
        {
            if (barks == null || barks.Length == 0) return;
            _voice.pitch = Random.Range(0.94f, 1.08f);
            _voice.PlayOneShot(barks[Random.Range(0, barks.Length)], barkVolume);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Footsteps(dt);
            Panting(dt);
        }

        void Footsteps(float dt)
        {
            if (footsteps == null || footsteps.Length == 0) return;
            float speed = _brain.Speed;
            if (speed < 0.4f) { _stride = 0f; return; }

            // One step per fixed distance, so cadence follows gait for free.
            _stride += speed * dt;
            float step = Mathf.Lerp(0.55f, 1.15f, _brain.Gait);
            if (_stride < step) return;
            _stride -= step;

            // Pitched up hard: a dog's feet are lighter and faster than a person's.
            _feet.pitch = Random.Range(1.15f, 1.35f);
            _feet.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)], footVolume);
        }

        void Panting(float dt)
        {
            if (pants == null || pants.Length == 0) return;
            _pantTimer -= dt;
            if (_pantTimer > 0f) return;

            var state = Follow.Core.GameState.Instance;
            float energy = state != null ? state.dogEnergy : 1f;

            bool tired = energy < 0.45f || _brain.Gait > 0.6f;
            _pantTimer = tired ? Random.Range(2.4f, 4.4f) : Random.Range(10f, 18f);
            if (!tired) return;

            _voice.pitch = Random.Range(0.95f, 1.05f);
            _voice.PlayOneShot(pants[Random.Range(0, pants.Length)], 0.3f);
        }

        // --- the voice ------------------------------------------------------------------

        /// <summary>
        /// One bark.
        ///
        /// A small dog's bark is a hard noise transient, then a very short buzzy tone that
        /// falls in pitch as it dies. Almost all of the character is in the first forty
        /// milliseconds, which is why the attack is nearly instant and the tail is not.
        /// </summary>
        static AudioClip Bark(float pitch)
        {
            const float seconds = 0.26f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];

            float baseHz = 430f * pitch;
            float phase = 0f;
            float low = 0f;
            var rng = new System.Random(Mathf.RoundToInt(pitch * 10000f));

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)Rate;
                float k = t / seconds;

                // The pitch drops away through the bark. A flat tone reads as a beep.
                float hz = baseHz * Mathf.Lerp(1.15f, 0.72f, Mathf.Pow(k, 0.55f));
                phase += hz / Rate * Mathf.PI * 2f;

                // Buzzy rather than pure - a saw, softened. This is the whole timbre.
                float saw = Mathf.Repeat(phase / (Mathf.PI * 2f), 1f) * 2f - 1f;
                float tone = saw * 0.55f + Mathf.Sin(phase) * 0.45f;

                // The transient: filtered noise, gone within thirty milliseconds.
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.42f;
                float transient = low * Mathf.Exp(-t * 90f) * 0.7f;

                // Nearly instant attack, exponential fall, silent by the end.
                float attack = Mathf.Min(1f, t / 0.006f);
                float body = Mathf.Exp(-t * 17f);
                float tail = Mathf.Min(1f, (1f - k) / 0.12f);

                data[i] = (tone * body + transient) * attack * tail * 0.9f;
            }

            return Finish("Dog_Bark", data);
        }

        /// <summary>A pant: soft breath noise in and out, twice.</summary>
        static AudioClip Pant()
        {
            const float seconds = 0.7f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];
            var rng = new System.Random(6611);
            float low = 0f, lower = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.30f;
                lower += (low - lower) * 0.30f;

                // Two breaths across the clip, each shaped in and out.
                float beat = Mathf.Repeat(t / 0.35f, 1f);
                float envelope = Mathf.Sin(beat * Mathf.PI);
                envelope *= envelope;

                data[i] = lower * envelope * 0.5f;
            }

            return Finish("Dog_Pant", data);
        }

        /// <summary>A whine: a wavering tone that slides up and thins out.</summary>
        static AudioClip Whine()
        {
            const float seconds = 0.85f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)Rate;
                float k = t / seconds;

                float hz = Mathf.Lerp(360f, 620f, Mathf.Pow(k, 0.7f))
                         * (1f + Mathf.Sin(t * 34f) * 0.02f);
                phase += hz / Rate * Mathf.PI * 2f;

                float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.25f;
                float envelope = Mathf.Min(1f, t / 0.06f) * Mathf.Min(1f, (1f - k) / 0.35f);
                data[i] = tone * envelope * 0.4f;
            }

            return Finish("Dog_Whine", data);
        }

        static AudioClip Finish(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.92f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
