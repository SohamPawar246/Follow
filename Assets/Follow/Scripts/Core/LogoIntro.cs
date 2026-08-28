using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Follow.UI;

namespace Follow.Core
{
    /// <summary>
    /// The studio logo, before anything else.
    ///
    /// It plays a video if there is one to play and a drawn card if there is not, and in
    /// both cases it is skippable and it hands over to the menu when it is done. The
    /// fallback matters: an intro that hard-depends on an asset file is an intro that
    /// breaks the whole game the day the file is renamed, and this is the very first thing
    /// the build runs.
    ///
    /// Drop a video at <c>Assets/Follow/Resources/StudioLogo.mp4</c> and it is picked up
    /// with no further wiring - Resources is checked by name, so the extension can be
    /// anything Unity imports as a VideoClip. A file in StreamingAssets works too, which
    /// is the route to take for anything large enough that you would rather not have it
    /// inside the build's Resources.
    /// </summary>
    public class LogoIntro : MonoBehaviour
    {
        [Tooltip("Assigned in the inspector, or left empty to look in Resources.")]
        public VideoClip clip;

        [Tooltip("Seconds the drawn card holds for when there is no video.")]
        public float cardSeconds = 2.6f;

        [Tooltip("Name drawn on the fallback card. Left empty, only the mark is drawn - "
               + "which is the right default, because inventing a studio name for "
               + "somebody is worse than showing none.")]
        public string studioName = "";

        [Tooltip("Longest the intro may run before it gives up and moves on.")]
        public float patience = 20f;

        public const string ResourceName = "StudioLogo";
        public const string StreamingName = "StudioLogo.mp4";

        Canvas _canvas;
        RectTransform _root;
        bool _skipped;

        /// <summary>Plays whatever it can find, then returns. Never throws, never hangs.</summary>
        public IEnumerator Play()
        {
            Build();

            // WebGL cannot play a VideoClip asset at all - it only understands a URL -
            // so in a browser the streaming copy is tried first and the drawn card is the
            // honest fallback. Everywhere else the imported clip is simpler and safer.
            bool browser = Application.platform == RuntimePlatform.WebGLPlayer;
            string streaming = Application.streamingAssetsPath + "/" + StreamingName;

            var found = clip != null ? clip : FromResources();

            if (browser)
            {
                if (HasStreamingCopy(streaming)) yield return Video(null, streaming);
                else yield return Card();
            }
            else if (found != null) yield return Video(found, null);
            else if (HasStreamingCopy(streaming)) yield return Video(null, streaming);
            else yield return Card();

            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        /// <summary>
        /// Whether a StreamingAssets copy exists. There is no real filesystem under
        /// WebGL, so the check is skipped there and the player is left to report a
        /// missing file through its error callback.
        /// </summary>
        static bool HasStreamingCopy(string path)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return true;
            try { return File.Exists(path); }
            catch { return false; }
        }

        /// <summary>
        /// The logo clip, by preferred name or by simply being the video in Resources.
        ///
        /// Insisting on an exact filename is a trap: a logo arrives named after whatever
        /// exported it and the intro silently falls back to the drawn card, which looks
        /// exactly like the video not working. If there is one video in Resources it is
        /// obviously the one that was put there to play.
        /// </summary>
        static VideoClip FromResources()
        {
            var named = Resources.Load<VideoClip>(ResourceName);
            if (named != null) return named;

            var all = Resources.LoadAll<VideoClip>("");
            if (all == null || all.Length == 0) return null;

            // More than one is ambiguous, so prefer anything that names itself.
            foreach (var candidate in all)
            {
                if (candidate == null) continue;
                string name = candidate.name.ToLowerInvariant();
                if (name.Contains("logo") || name.Contains("studio") || name.Contains("intro"))
                    return candidate;
            }
            return all[0];
        }

        void Build()
        {
            // Above the scene-transition sheet, which the boot scene snaps to full black
            // before this runs. Underneath it the logo would play behind a black curtain.
            _canvas = UIFactory.CreateCanvas("LogoCanvas", 9500);
            _canvas.transform.SetParent(transform, false);
            _root = UIFactory.Stretch(UIFactory.Rect("Logo", _canvas.transform));

            // Black behind everything. The logo sits on its own, not on whatever the menu
            // scene happened to leave on screen.
            var backdrop = UIFactory.Solid("Backdrop", _root, Color.black);
            UIFactory.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var skip = UIFactory.Label("Skip", _root, "press any key", 20,
                new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Right, true);
            UIFactory.Anchor(skip.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 40f),
                new Vector2(300f, 32f));
            skip.rectTransform.pivot = new Vector2(1f, 0f);
        }

        // --- video ---------------------------------------------------------------------

        IEnumerator Video(VideoClip asset, string url)
        {
            var target = new RenderTexture(1920, 1080, 0) { name = "LogoTarget" };

            var host = new GameObject("VideoPlayer");
            host.transform.SetParent(transform, false);
            var player = host.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.SetDirectAudioVolume(0, 0.8f);

            if (asset != null) { player.source = VideoSource.VideoClip; player.clip = asset; }
            else { player.source = VideoSource.Url; player.url = url; }

            // Fit inside the screen rather than filling it, so a logo authored at any
            // aspect keeps its shape instead of being stretched across an ultrawide.
            var image = new GameObject("Frame").AddComponent<RawImage>();
            image.transform.SetParent(_root, false);
            image.texture = target;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            UIFactory.Stretch(rect);
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            bool failed = false;
            player.errorReceived += (source, message) =>
            {
                Debug.LogWarning("Logo video failed: " + message);
                failed = true;
            };

            player.Prepare();
            float waited = 0f;
            while (!player.isPrepared && !failed && waited < 6f && !Skipped())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (failed || !player.isPrepared)
            {
                // A missing codec should cost the player nothing at all.
                Destroy(host);
                Destroy(image.gameObject);
                target.Release();
                Destroy(target);
                if (!_skipped) yield return Card();
                yield break;
            }

            if (fitter != null && player.width > 0 && player.height > 0)
                fitter.aspectRatio = player.width / (float)player.height;

            player.Play();

            float elapsed = 0f;
            while (elapsed < patience && !Skipped())
            {
                elapsed += Time.unscaledDeltaTime;
                // isPlaying goes false at the end; the first frames need a grace period
                // before that is a reliable signal.
                if (elapsed > 0.4f && !player.isPlaying) break;
                yield return null;
            }

            yield return FadeOut(image, 0.4f);

            player.Stop();
            Destroy(host);
            target.Release();
            Destroy(target);
        }

        // --- the drawn fallback -----------------------------------------------------------

        /// <summary>
        /// What you get with no video: the studio name drawn out of the same parts the
        /// rest of the game is drawn from, with a paw print for a full stop.
        /// </summary>
        IEnumerator Card()
        {
            var T = CozyTheme.Active;

            var holder = UIFactory.Rect("Card", _root);
            UIFactory.Anchor(holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 320f));
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            var group = UIFactory.Group(holder);
            group.alpha = 0f;

            var mark = UIFactory.Shape("Mark", holder, T.Dot, T.honey, Image.Type.Simple);
            UIFactory.Anchor(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 66f),
                new Vector2(74f, 74f));
            mark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            mark.raycastTarget = false;

            for (int i = 0; i < 4; i++)
            {
                float[] xs = { -40f, -14f, 14f, 40f };
                float[] ys = { 108f, 122f, 122f, 108f };
                var toe = UIFactory.Shape("Toe", holder, T.Dot, T.honey, Image.Type.Simple);
                UIFactory.Anchor(toe.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(xs[i], ys[i]), new Vector2(24f, 30f));
                toe.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                toe.raycastTarget = false;
            }

            if (!string.IsNullOrWhiteSpace(studioName))
            {
                var name = UIFactory.Label("Name", holder, studioName.ToUpperInvariant(), 54,
                    T.cream, TextAlignmentOptions.Center, true);
                UIFactory.Anchor(name.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -30f), new Vector2(880f, 72f));
                name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                name.characterSpacing = 12f;
            }

            var line = UIFactory.Shape("Rule", holder, T.Chip, new Color(1f, 1f, 1f, 0.25f));
            UIFactory.Anchor(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -86f),
                new Vector2(0f, 3f));
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            line.raycastTarget = false;

            float t = 0f;
            while (t < cardSeconds && !Skipped())
            {
                t += Time.unscaledDeltaTime;

                // In over half a second, out over the last third, and the rule draws
                // itself across underneath as the name settles.
                group.alpha = Mathf.Min(t / 0.6f, Mathf.Clamp01((cardSeconds - t) / 0.5f));
                holder.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Mathf.Min(1f, t / 0.9f));
                line.rectTransform.sizeDelta = new Vector2(
                    Mathf.Lerp(0f, 420f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.8f))), 3f);
                yield return null;
            }

            group.alpha = 0f;
        }

        // --- skipping ----------------------------------------------------------------------

        IEnumerator FadeOut(Graphic graphic, float seconds)
        {
            float t = 0f;
            Color from = graphic.color;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                graphic.color = new Color(from.r, from.g, from.b, 1f - t / seconds);
                yield return null;
            }
        }

        bool Skipped()
        {
            if (_skipped) return true;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;

            _skipped = (kb != null && kb.anyKey.wasPressedThisFrame)
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            return _skipped;
        }
    }
}
