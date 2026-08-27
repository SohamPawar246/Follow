using System;
using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// Dusk is the only deadline in the game, so the light has to be readable at a glance.
    /// Drives the sun, ambient, and fog from a single normalised clock.
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        public static DayCycle Instance { get; private set; }

        [Header("Clock")]
        [Tooltip("Real seconds for one full playable day, dawn to full dark.")]
        public float dayLengthSeconds = 480f;
        [Range(0f, 1f)] public float startTime = 0.12f;
        public bool paused;

        [Header("Sun")]
        public Light sun;
        public Gradient sunColor;
        public AnimationCurve sunIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float sunYaw = 40f;

        [Header("Atmosphere")]
        public Gradient ambientColor;
        public Gradient fogColor;
        public float fogNear = 22f;
        public float fogFar = 150f;

        [Range(0f, 1f)] public float duskAt = 0.74f;
        [Range(0f, 1f)] public float darkAt = 0.9f;

        /// <summary>0 at dawn, 1 at full dark.</summary>
        public float Time01 { get; private set; }
        public bool IsDusk => Time01 >= duskAt;
        public bool IsDark => Time01 >= darkAt;

        /// <summary>Feeds the photo grade: golden hour beats noon beats dusk (GDD).</summary>
        public float LightQuality
        {
            get
            {
                if (Time01 < 0.10f || Time01 > darkAt) return 0.25f;
                float goldenMorning = 1f - Mathf.Abs(Time01 - 0.20f) / 0.16f;
                float goldenEvening = 1f - Mathf.Abs(Time01 - 0.70f) / 0.16f;
                float golden = Mathf.Max(goldenMorning, goldenEvening);
                return Mathf.Clamp01(Mathf.Max(0.6f, golden));
            }
        }

        public event Action DuskFell;
        public event Action NightFell;

        bool _duskFired;
        bool _nightFired;

        void Awake()
        {
            Instance = this;
            Time01 = startTime;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void ResetToMorning()
        {
            Time01 = startTime;
            _duskFired = false;
            _nightFired = false;
        }

        void Update()
        {
            if (!paused && dayLengthSeconds > 0f)
                Time01 = Mathf.Clamp01(Time01 + UnityEngine.Time.deltaTime / dayLengthSeconds);

            Apply();

            if (!_duskFired && IsDusk) { _duskFired = true; DuskFell?.Invoke(); }
            if (!_nightFired && IsDark) { _nightFired = true; NightFell?.Invoke(); }
        }

        void Apply()
        {
            if (sun != null)
            {
                // Sweep from just above the eastern horizon to just below the western one.
                float elevation = Mathf.Lerp(4f, 176f, Time01);
                sun.transform.rotation = Quaternion.Euler(elevation, sunYaw, 0f);
                sun.color = sunColor.Evaluate(Time01);
                sun.intensity = sunIntensity.Evaluate(Time01);
            }

            RenderSettings.ambientLight = ambientColor.Evaluate(Time01);
            RenderSettings.fogColor = fogColor.Evaluate(Time01);
            RenderSettings.fogStartDistance = fogNear;
            RenderSettings.fogEndDistance = fogFar;
        }
    }
}
