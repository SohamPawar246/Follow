using System;
using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// A full twenty-four hours, not a working day that stops at nine.
    ///
    /// The clock used to run out at dusk and sit there. Now it goes all the way round: the
    /// sun sets, the moon takes over the same directional light from the other side, and
    /// dawn arrives on its own whether you slept through it or not. Night runs at several
    /// times the speed of the day, because standing in the cold is a consequence and not
    /// an activity - you feel the length of it without waiting out the length of it.
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        public static DayCycle Instance { get; private set; }

        [Header("Clock")]
        [Tooltip("Real seconds for the daylight part of one day.")]
        public float dayLengthSeconds = 340f;
        [Tooltip("How much faster the clock runs once it is dark.")]
        public float nightSpeed = 2.6f;
        [Range(0f, 1f)] public float startTime = 0.10f;
        public bool paused;

        [Header("Sun and moon")]
        public Light sun;
        public Gradient sunColor;
        public AnimationCurve sunIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float sunYaw = 40f;
        [Tooltip("The same light, turned round and dimmed, once the sun is down.")]
        public Color moonColor = new Color(0.55f, 0.66f, 1f);
        public float moonIntensity = 0.32f;

        [Header("Atmosphere")]
        public Gradient ambientColor;
        public Gradient fogColor;
        public float fogNear = 40f;
        public float fogFar = 155f;
        [Tooltip("Fog closes in after dark; you cannot see as far by moonlight.")]
        public float nightFogFar = 78f;
        [Tooltip("Colour the sky band leans toward. Keeps shadowed sides from going flat.")]
        public Color skyBias = new Color(0.62f, 0.74f, 0.92f);
        [Tooltip("How much light the ground throws back up. Under one, always.")]
        [Range(0f, 1f)] public float groundBounce = 0.55f;

        // 0 is dawn, 0.5 is sunset, 1 is the next dawn.
        [Range(0f, 1f)] public float duskAt = 0.54f;
        [Range(0f, 1f)] public float darkAt = 0.62f;
        [Tooltip("Light comes back before the clock rolls over.")]
        [Range(0f, 1f)] public float dawnAt = 0.94f;

        public float Time01 { get; private set; }
        public bool IsDusk => Time01 >= duskAt && Time01 < dawnAt;
        public bool IsDark => Time01 >= darkAt && Time01 < dawnAt;

        /// <summary>0 in the small hours, 1 at midday. Drives fog, mood and photographs.</summary>
        public float Daylight
        {
            get
            {
                if (Time01 < duskAt) return Mathf.Clamp01(Mathf.InverseLerp(-0.06f, 0.14f, Time01));
                // Eased rather than linear: the light should go slowly at first, then
                // quickly, then linger - which is what a sunset actually does.
                if (Time01 < darkAt)
                    return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duskAt, darkAt, Time01));
                if (Time01 < dawnAt) return 0f;
                return Mathf.InverseLerp(dawnAt, 1f, Time01);
            }
        }

        /// <summary>How dark it is, 0 to 1. The inverse of daylight, kept for readability.</summary>
        public float Night => 1f - Daylight;

        /// <summary>
        /// Too dark to work by, and therefore also late enough to go to bed.
        ///
        /// One property, because these are the same fact and having two of them left a
        /// gap: photography went by the light level and sleeping went by IsDusk, whose
        /// window closes at dawnAt. Between the two there was a moment at half past four
        /// in the morning when it was too dark to take a photograph and too late to be
        /// allowed to sleep, which is a player standing in a black wood with nothing on
        /// offer and no way to end the night.
        /// </summary>
        public bool LightHasGone => Daylight <= 0.16f;

        /// <summary>
        /// Feeds the photo grade: golden hour beats noon beats dusk, and after dark you
        /// are not going to get anything worth keeping.
        /// </summary>
        public float LightQuality
        {
            get
            {
                if (IsDark) return 0.12f;
                float goldenMorning = 1f - Mathf.Abs(Time01 - 0.14f) / 0.12f;
                float goldenEvening = 1f - Mathf.Abs(Time01 - 0.50f) / 0.12f;
                float golden = Mathf.Max(goldenMorning, goldenEvening);
                return Mathf.Clamp01(Mathf.Max(0.55f, golden)) * Mathf.Lerp(0.2f, 1f, Daylight);
            }
        }

        /// <summary>Readable clock time. Dawn is six, so the whole circle is twenty-four hours.</summary>
        public string ClockText
        {
            get
            {
                float hours = Mathf.Repeat(6f + Time01 * 24f, 24f);
                int h = Mathf.FloorToInt(hours);
                int m = Mathf.FloorToInt((hours - h) * 6f) * 10;
                string suffix = h >= 12 ? "pm" : "am";
                int shown = h % 12;
                if (shown == 0) shown = 12;
                return shown + ":" + m.ToString("00") + " " + suffix;
            }
        }

        public event Action DuskFell;
        public event Action NightFell;
        /// <summary>Midnight came and went without anybody sleeping through it deliberately.</summary>
        public event Action DayRolled;

        bool _duskFired;
        bool _nightFired;

        void Awake()
        {
            Instance = this;
            Time01 = startTime;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Moves the clock. Used by sleeping, and by the editor's play probe.</summary>
        public void SetTime(float time01)
        {
            Time01 = Mathf.Repeat(time01, 1f);
            _duskFired = IsDusk;
            _nightFired = IsDark;
            Apply();
        }

        public void ResetToMorning()
        {
            Time01 = startTime;
            _duskFired = false;
            _nightFired = false;
        }

        void Update()
        {
            if (!paused && dayLengthSeconds > 0f)
            {
                float speed = IsDark ? nightSpeed : 1f;
                Time01 += UnityEngine.Time.deltaTime / dayLengthSeconds * speed;

                if (Time01 >= 1f)
                {
                    Time01 -= 1f;
                    _duskFired = false;
                    _nightFired = false;
                    DayRolled?.Invoke();
                }
            }

            Apply();

            if (!_duskFired && IsDusk) { _duskFired = true; DuskFell?.Invoke(); }
            if (!_nightFired && IsDark) { _nightFired = true; NightFell?.Invoke(); }
        }

        void Apply()
        {
            if (sun != null)
            {
                // One full rotation per day. Past sunset the light would be underground,
                // so it is turned back over as the moon rather than switched off - dark
                // with no direction at all reads as a rendering fault, not as night.
                float elevation = Time01 * 360f;

                // How much of the light is moonlight. This used to be a hard switch at
                // four percent daylight, which popped the scene brighter and bluer in a
                // single frame the moment the sun went out. Handing over across the last
                // of the dusk means the change is only ever something you notice
                // afterwards.
                float moonAmount = 1f - Mathf.Clamp01(Daylight / 0.12f);

                sun.transform.rotation = moonAmount > 0.5f
                    ? Quaternion.Euler(elevation - 180f, sunYaw + 180f, 0f)
                    : Quaternion.Euler(elevation, sunYaw, 0f);

                sun.color = Color.Lerp(sunColor.Evaluate(Time01), moonColor, moonAmount);
                sun.intensity = Mathf.Lerp(
                    Mathf.Max(0.02f, sunIntensity.Evaluate(Time01)), moonIntensity, moonAmount);
                sun.shadowStrength = Mathf.Lerp(0.22f, 0.62f, Daylight);
            }

            // The scene is lit with trilight ambient, and trilight ignores ambientLight
            // entirely - which is why night used to be a green afternoon however far the
            // sun was turned down. Drive all three bands from the one ramp instead.
            Color ambient = ambientColor.Evaluate(Time01);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(ambient, skyBias, 0.45f);
            RenderSettings.ambientEquatorColor = ambient;
            RenderSettings.ambientGroundColor = ambient * groundBounce;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fogColor = fogColor.Evaluate(Time01);
            RenderSettings.fogStartDistance = Mathf.Lerp(12f, fogNear, Daylight);
            RenderSettings.fogEndDistance = Mathf.Lerp(nightFogFar, fogFar, Daylight);
        }
    }
}
