using UnityEngine;

namespace Follow.Dog
{
    /// <summary>
    /// The dog's voice and feet.
    ///
    /// The bark is the single most important sound in the game: it is how a find gets
    /// communicated, and it has to be locatable by ear alone. It therefore carries much
    /// further than anything else and sits on its own source.
    /// </summary>
    [RequireComponent(typeof(DogBrain))]
    public class DogAudio : MonoBehaviour
    {
        [Header("Clips")]
        public AudioClip[] barks;
        public AudioClip[] pants;
        public AudioClip whine;
        public AudioClip[] footsteps;

        [Header("Mix")]
        public float barkVolume = 0.85f;
        public float footVolume = 0.28f;
        [Tooltip("Barks carry a long way. Footsteps should not.")]
        public float barkRange = 70f;
        public float footRange = 18f;

        DogBrain _brain;
        AudioSource _voice;
        AudioSource _feet;
        float _stride;
        float _pantTimer;

        void Awake()
        {
            _brain = GetComponent<DogBrain>();

            _voice = gameObject.AddComponent<AudioSource>();
            Configure(_voice, barkRange);
            _feet = gameObject.AddComponent<AudioSource>();
            Configure(_feet, footRange);

            _brain.Barked += Bark;
            _brain.Pointed += OnPointed;
        }

        void OnDestroy()
        {
            if (_brain == null) return;
            _brain.Barked -= Bark;
            _brain.Pointed -= OnPointed;
        }

        void OnPointed(ScentPoint point) => Bark();

        void Configure(AudioSource s, float range)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 3f;
            s.maxDistance = range;
            s.dopplerLevel = 0.05f;
        }

        void Bark()
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
            _pantTimer = tired ? Random.Range(2.2f, 4f) : Random.Range(9f, 16f);
            if (!tired) return;

            _voice.pitch = Random.Range(0.95f, 1.05f);
            _voice.PlayOneShot(pants[Random.Range(0, pants.Length)], 0.35f);
        }
    }
}
