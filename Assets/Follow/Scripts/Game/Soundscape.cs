using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// The sound of the place, and a little music under it.
    ///
    /// Neither of the asset packs contains an ambience bed or a music loop - they are UI
    /// clicks and RPG foley - so both are synthesised here into AudioClips at load. That is
    /// a deliberate choice rather than a stopgap: a wind bed is filtered noise, birdsong is
    /// a couple of swept sines, and a music box is a pentatonic scale with a fast attack
    /// and a long decay. All three are cheaper to generate than to store, and generating
    /// them means the day bed and the night bed can be genuinely different rather than the
    /// same file at two volumes.
    ///
    /// Everything is built to loop seamlessly: the noise bed is windowed at its own seam,
    /// and the music is a whole number of bars.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class Soundscape : MonoBehaviour
    {
        public static Soundscape Instance { get; private set; }

        [Header("Levels")]
        [Range(0f, 1f)] public float ambienceVolume = 0.26f;
        [Range(0f, 1f)] public float musicVolume = 0.2f;

        [Header("Music")]
        [Tooltip("Seconds of silence between phrases. Music that never stops stops being music.")]
        public Vector2 restBetweenPhrases = new Vector2(14f, 34f);

        const int Rate = 44100;

        AudioSource _day;
        AudioSource _night;
        AudioSource _music;

        float _restTimer;

        void Awake()
        {
            Instance = this;

            _day = MakeSource("AmbienceDay", Daytime(), true);
            _night = MakeSource("AmbienceNight", Nighttime(), true);
            _music = MakeSource("Music", null, false);

            _restTimer = 6f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource MakeSource(string name, AudioClip clip, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;      // a bed, not a point in the world
            source.volume = 0f;
            if (clip != null) source.Play();
            return source;
        }

        void Update()
        {
            var cycle = DayCycle.Instance;
            float night = cycle != null ? cycle.Night : 0f;
            float dt = Time.deltaTime;

            // Crossfade the two beds rather than switching, so dusk genuinely has both.
            _day.volume = Mathf.Lerp(_day.volume, ambienceVolume * (1f - night), dt * 0.8f);
            _night.volume = Mathf.Lerp(_night.volume, ambienceVolume * night, dt * 0.8f);

            Music(dt, night);
        }

        /// <summary>
        /// Phrases with real silence between them. A loop that never lets up becomes
        /// wallpaper within a minute and then becomes irritating.
        /// </summary>
        void Music(float dt, float night)
        {
            _music.volume = Mathf.Lerp(_music.volume, musicVolume, dt * 0.6f);
            if (_music.isPlaying) return;

            _restTimer -= dt;
            if (_restTimer > 0f) return;

            _music.clip = Phrase(night > 0.5f);
            _music.Play();
            _restTimer = _music.clip.length + Random.Range(restBetweenPhrases.x, restBetweenPhrases.y);
        }

        // --- ambience ----------------------------------------------------------------

        /// <summary>
        /// Wind in leaves, with birds over it. The wind is noise pushed through a one-pole
        /// low pass and then swelled slowly; the birds are short frequency sweeps, which is
        /// most of what a small bird actually is.
        /// </summary>
        AudioClip Daytime()
        {
            const float seconds = 22f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];

            Wind(data, 0.30f, 0.10f);

            var rng = new System.Random(4021);
            // Roughly one call every second and a half, scattered.
            for (int i = 0; i < 16; i++)
            {
                float at = (float)rng.NextDouble() * (seconds - 1.2f);
                Chirp(data, at, 1400f + (float)rng.NextDouble() * 1500f,
                    2 + rng.Next(3), 0.055f, rng);
            }

            return Finish("Ambience_Day", data);
        }

        /// <summary>Lower, emptier, and crickets instead of birds.</summary>
        AudioClip Nighttime()
        {
            const float seconds = 22f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];

            Wind(data, 0.22f, 0.07f);

            // A cricket is a short burst of a high tone, repeated on a steady beat.
            var rng = new System.Random(9137);
            float beat = 0.42f;
            for (float at = 0.2f; at < seconds - 0.2f; at += beat)
            {
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.06f;
                Burst(data, at + jitter, 4200f, 0.035f, 0.035f);
            }

            return Finish("Ambience_Night", data);
        }

        /// <summary>
        /// Wind in leaves: separate gusts, not a continuous hiss.
        ///
        /// A steady bed of filtered noise is the sound of the sea, which is what this used
        /// to be. Real wind in a wood arrives, rises, passes and leaves a gap, and it is
        /// the gaps that make it read as air moving through trees rather than as water.
        /// The noise is also filtered much harder, so it whispers instead of shushing.
        /// </summary>
        static void Wind(float[] data, float level, float brightness)
        {
            var rng = new System.Random(1723);
            float low = 0f, lower = 0f;

            // Four or five gusts across the loop, each with its own shape and length.
            const int gusts = 5;
            var starts = new float[gusts];
            var lengths = new float[gusts];
            float span = data.Length / (float)Rate;

            for (int g = 0; g < gusts; g++)
            {
                starts[g] = (g + (float)rng.NextDouble() * 0.6f) * (span / gusts);
                lengths[g] = 1.2f + (float)rng.NextDouble() * 2.2f;
            }

            for (int i = 0; i < data.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Two poles rather than one: much darker, much less like static.
                low += (white - low) * brightness;
                lower += (low - lower) * brightness;

                float t = i / (float)Rate;
                float envelope = 0f;
                for (int g = 0; g < gusts; g++)
                {
                    float k = (t - starts[g]) / lengths[g];
                    if (k <= 0f || k >= 1f) continue;
                    // Slow in, slower out. A gust does not switch on.
                    envelope += Mathf.Sin(k * Mathf.PI) * Mathf.Sin(k * Mathf.PI);
                }

                // A whisper underneath, so the wood is never completely silent.
                data[i] += lower * level * (0.18f + envelope);
            }
        }

        /// <summary>A bird: a few quick upward sweeps.</summary>
        static void Chirp(float[] data, float at, float baseHz, int notes, float level,
            System.Random rng)
        {
            for (int n = 0; n < notes; n++)
            {
                float start = at + n * 0.11f;
                float length = 0.06f + (float)rng.NextDouble() * 0.05f;
                float from = baseHz * (0.9f + (float)rng.NextDouble() * 0.2f);
                float to = from * (1.25f + (float)rng.NextDouble() * 0.5f);

                int first = Mathf.RoundToInt(start * Rate);
                int count = Mathf.RoundToInt(length * Rate);
                float phase = 0f;

                for (int i = 0; i < count; i++)
                {
                    int index = first + i;
                    if (index < 0 || index >= data.Length) continue;

                    float k = i / (float)count;
                    float hz = Mathf.Lerp(from, to, k);
                    phase += hz / Rate * Mathf.PI * 2f;

                    // Fast in, slow out, so it does not click at either end.
                    float envelope = Mathf.Sin(k * Mathf.PI);
                    data[index] += Mathf.Sin(phase) * envelope * level;
                }
            }
        }

        /// <summary>A cricket: a very short tone burst.</summary>
        static void Burst(float[] data, float at, float hz, float length, float level)
        {
            int first = Mathf.RoundToInt(at * Rate);
            int count = Mathf.RoundToInt(length * Rate);
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                if (index < 0 || index >= data.Length) continue;

                float k = i / (float)count;
                phase += hz / Rate * Mathf.PI * 2f;
                // Buzzy rather than pure: a cricket is closer to a square than a sine.
                float tone = Mathf.Sign(Mathf.Sin(phase)) * 0.35f + Mathf.Sin(phase) * 0.65f;
                data[index] += tone * Mathf.Sin(k * Mathf.PI) * level;
            }
        }

        // --- music --------------------------------------------------------------------

        // A minor pentatonic, which is the scale that cannot sound wrong against itself.
        static readonly int[] Scale = { 0, 3, 5, 7, 10, 12, 15, 17 };

        /// <summary>
        /// One short phrase for a music box: a handful of struck notes over a held fifth.
        /// Written fresh each time from a random walk through the scale, so the tune is
        /// familiar without ever being quite the same twice.
        /// </summary>
        AudioClip Phrase(bool nocturne)
        {
            float seconds = nocturne ? 11f : 9f;
            int samples = Mathf.RoundToInt(Rate * seconds);
            var data = new float[samples];

            var rng = new System.Random(Random.Range(0, 100000));
            // A whole tone lower at night, and slower.
            float root = nocturne ? 174.61f : 196f;      // F3 / G3
            float step = nocturne ? 0.78f : 0.62f;

            // The drone: root and fifth, very quiet, under everything.
            Drone(data, root * 0.5f, 0.05f);
            Drone(data, root * 0.75f, 0.035f);

            int index = 2;
            for (float at = 0.3f; at < seconds - 1.6f; at += step * (rng.Next(3) == 0 ? 2f : 1f))
            {
                // Step up or down the scale rather than leaping; leaps sound accidental.
                index = Mathf.Clamp(index + rng.Next(-2, 3), 0, Scale.Length - 1);
                float hz = root * Mathf.Pow(2f, Scale[index] / 12f);
                Pluck(data, at, hz, 0.13f, 2.2f);
            }

            return Finish(nocturne ? "Music_Night" : "Music_Day", data);
        }

        /// <summary>A struck note: fast attack, long exponential tail, a little overtone.</summary>
        static void Pluck(float[] data, float at, float hz, float level, float decay)
        {
            int first = Mathf.RoundToInt(at * Rate);
            int count = Mathf.RoundToInt(decay * Rate);

            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                if (index < 0 || index >= data.Length) continue;

                float t = i / (float)Rate;
                float phase = hz * t * Mathf.PI * 2f;

                // A struck bar is the fundamental plus a quiet, faster-fading octave.
                float tone = Mathf.Sin(phase)
                           + Mathf.Sin(phase * 2f) * 0.3f * Mathf.Exp(-t * 6f)
                           + Mathf.Sin(phase * 3f) * 0.1f * Mathf.Exp(-t * 11f);

                float attack = Mathf.Min(1f, t / 0.006f);
                data[index] += tone * level * attack * Mathf.Exp(-t * 2.4f);
            }
        }

        /// <summary>A held tone across the whole phrase, faded in and out at the ends.</summary>
        static void Drone(float[] data, float hz, float level)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)data.Length;
                float envelope = Mathf.Min(1f, k / 0.15f) * Mathf.Min(1f, (1f - k) / 0.3f);
                data[i] += Mathf.Sin(hz * t * Mathf.PI * 2f) * level * envelope;
            }
        }

        // --- shared -------------------------------------------------------------------

        /// <summary>
        /// Normalises, then crossfades the tail into the head so the loop has no seam.
        /// A click every twelve seconds is the fastest way to make ambience unbearable.
        /// </summary>
        static AudioClip Finish(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.9f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            int blend = Mathf.Min(Rate / 4, data.Length / 8);
            for (int i = 0; i < blend; i++)
            {
                float k = i / (float)blend;
                int tail = data.Length - blend + i;
                data[i] = Mathf.Lerp(data[tail], data[i], k);
            }
            // The copied tail is now redundant; fade it out so it does not double up.
            for (int i = 0; i < blend; i++)
            {
                float k = i / (float)blend;
                data[data.Length - blend + i] *= 1f - k;
            }

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
