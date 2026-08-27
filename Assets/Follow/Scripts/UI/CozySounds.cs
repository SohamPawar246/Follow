using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// Interface and world sounds in one asset. Every slot is optional: a missing clip
    /// simply plays nothing rather than throwing, so audio can land piece by piece.
    /// </summary>
    [CreateAssetMenu(menuName = "Follow/Cozy Sounds", fileName = "CozySounds")]
    public class CozySounds : ScriptableObject
    {
        static CozySounds _active;
        public static CozySounds Active
        {
            get
            {
                if (_active == null) _active = Resources.Load<CozySounds>("CozySounds");
                return _active;
            }
            set { _active = value; }
        }

        [Header("Journal")]
        public AudioClip bookOpen;
        public AudioClip bookClose;
        public AudioClip[] pageFlips;
        [Tooltip("Played once per name as it is crossed off.")]
        public AudioClip scratch;

        [Header("Interface")]
        public AudioClip buttonPress;
        public AudioClip buttonHover;
        public AudioClip chipPop;

        [Header("World")]
        public AudioClip[] footsteps;
        public AudioClip shutter;

        [Header("Mix")]
        [Range(0f, 1f)] public float uiVolume = 0.55f;

        static AudioSource _source;

        static AudioSource Source()
        {
            if (_source != null) return _source;
            var go = new GameObject("~CozyAudio");
            DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            return _source;
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;
            var src = Source();
            var sounds = Active;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume * (sounds != null ? sounds.uiVolume : 0.55f));
        }

        public static void PlayAny(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
