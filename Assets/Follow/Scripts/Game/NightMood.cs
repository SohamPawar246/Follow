using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Follow.Game
{
    /// <summary>
    /// What night actually looks like.
    ///
    /// Turning the sun down does not make night; it makes a badly exposed day. Night is
    /// blue, low in contrast at the top end, desaturated but not grey, and tight - you can
    /// see a short way and no further. This drives the grade off the day cycle so all of
    /// that arrives together, and it does it on its own volume so the daytime profile the
    /// forest was authored against is never edited underneath it.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class NightMood : MonoBehaviour
    {
        [Header("The blue")]
        public Color nightFilter = new Color(0.58f, 0.72f, 1.05f);
        public float nightExposure = -0.75f;
        public float nightSaturation = -26f;
        public float nightContrast = -6f;

        [Header("Closing in")]
        public float nightVignette = 0.46f;
        public Color nightVignetteColor = new Color(0.05f, 0.07f, 0.16f);

        Volume _volume;
        ColorAdjustments _grade;
        Vignette _vignette;
        Bloom _bloom;

        void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.isGlobal = true;
            // Above the forest profile, so this is a layer on top rather than a rewrite.
            _volume.priority = 10f;
            _volume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            _volume.profile = profile;

            _grade = profile.Add<ColorAdjustments>(true);
            _grade.colorFilter.Override(nightFilter);
            _grade.postExposure.Override(nightExposure);
            _grade.saturation.Override(nightSaturation);
            _grade.contrast.Override(nightContrast);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(nightVignette);
            _vignette.smoothness.Override(0.62f);
            _vignette.color.Override(nightVignetteColor);

            // Moonlight and firelight both want to bleed a little; the fire especially.
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.55f);
            _bloom.intensity.Override(1.5f);
            _bloom.scatter.Override(0.78f);
            _bloom.tint.Override(new Color(0.85f, 0.9f, 1f));
        }

        void Update()
        {
            var cycle = DayCycle.Instance;
            if (cycle == null || _volume == null) return;

            // Eased, and then rate-limited on top, so the grade can never arrive faster
            // than about three seconds however abruptly the clock is moved - sleeping
            // sets the time outright and a hard cut to full night looks like a glitch.
            float target = Mathf.SmoothStep(0f, 1f, cycle.Night);
            _volume.weight = Mathf.MoveTowards(_volume.weight, target, Time.deltaTime * 0.35f);
        }
    }
}
