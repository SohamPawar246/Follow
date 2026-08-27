using System;
using UnityEngine;

namespace Follow.Core
{
    /// <summary>
    /// The player's preferences, in one place and remembered between sessions.
    ///
    /// This used to live as three lambdas inside the main menu, which meant the in-game
    /// options could not show the same values and nothing survived quitting. Every setting
    /// applies itself on write, so callers change the property and stop thinking about it.
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "follow.volume";
        const string FullscreenKey = "follow.fullscreen";
        const string ReducedMotionKey = "follow.reducedMotion";
        const string ZoomKey = "follow.zoom";

        public static event Action Changed;

        static bool _loaded;

        static float _volume = 0.8f;
        static bool _fullscreen = true;
        static bool _reducedMotion;
        static float _zoom = 1f;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            _fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            _reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
            _zoom = PlayerPrefs.GetFloat(ZoomKey, 1f);

            AudioListener.volume = _volume;
            Raise();
        }

        public static float Volume
        {
            get { Load(); return _volume; }
            set
            {
                Load();
                _volume = Mathf.Clamp01(value);
                AudioListener.volume = _volume;
                PlayerPrefs.SetFloat(VolumeKey, _volume);
                Raise();
            }
        }

        public static bool Fullscreen
        {
            get { Load(); return _fullscreen; }
            set
            {
                Load();
                _fullscreen = value;
                Screen.fullScreen = value;
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Raise();
            }
        }

        /// <summary>Damps the interface animations for anyone who finds them distracting.</summary>
        public static bool ReducedMotion
        {
            get { Load(); return _reducedMotion; }
            set
            {
                Load();
                _reducedMotion = value;
                PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
                Raise();
            }
        }

        /// <summary>
        /// How close the camera sits, as a multiplier on the rig's distance. Exists
        /// because how big the surveyor and the dog read on screen is a taste question,
        /// and the honest answer is to let the player set it.
        /// </summary>
        public static float Zoom
        {
            get { Load(); return _zoom; }
            set
            {
                Load();
                _zoom = Mathf.Clamp(value, 0.7f, 1.45f);
                PlayerPrefs.SetFloat(ZoomKey, _zoom);
                Raise();
            }
        }

        /// <summary>Zoom expressed 0 to 1, for a slider that reads left-close right-far.</summary>
        public static float Zoom01
        {
            get => Mathf.InverseLerp(0.7f, 1.45f, Zoom);
            set => Zoom = Mathf.Lerp(0.7f, 1.45f, Mathf.Clamp01(value));
        }

        static void Raise()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
