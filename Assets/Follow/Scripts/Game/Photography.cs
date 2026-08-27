using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;
using Follow.Data;
using Follow.UI;

namespace Follow.Game
{
    /// <summary>
    /// The verb the whole game is built around.
    ///
    /// Raising the lens finds the nearest subject, holds it still, and puts a short row of
    /// arrows in the air above it. Get the order right and the shutter opens on a clean
    /// frame; miss and the shot is spoiled in proportion - one fumble is a slightly soft
    /// picture, four is a smear. The photograph itself is a real render of the real scene,
    /// so the album is a record of where you actually were and what the light was doing.
    ///
    /// Light is not a modifier bolted on: a shot after dark is genuinely dark, because the
    /// camera renders the same world you are standing in.
    /// </summary>
    public class Photography : MonoBehaviour
    {
        public static Photography Instance { get; private set; }

        public enum Mode { Idle, Aiming, Shooting, Reviewing }

        [Header("Reach")]
        public float range = 22f;

        [Header("Photo")]
        public int photoWidth = 420;
        public int photoHeight = 280;
        [Tooltip("Narrower than the game camera, so the subject actually fills the frame.")]
        public float photoFieldOfView = 22f;

        public Mode State { get; private set; } = Mode.Idle;
        public PhotoSubject Target { get; private set; }

        /// <summary>Actually taking a shot, as opposed to merely having one lined up.</summary>
        public bool Busy => State == Mode.Shooting || State == Mode.Reviewing;

        /// <summary>Shutter releases this session, kept or discarded. The tutorial reads it.</summary>
        public int ShotsTaken { get; private set; }

        ShotSequenceUI _sequence;
        PhotoReviewUI _review;
        Camera _lens;
        GameState _state;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _state = GameState.Ensure();
            _sequence = ShotSequenceUI.Create(transform);
            _review = PhotoReviewUI.Create(transform);
        }

        void Update()
        {
            if (State == Mode.Shooting || State == Mode.Reviewing) return;

            var player = PlayerMover.Instance;
            if (player == null) return;
            if (Follow.UI.UIModal.Any) { GameHud.Instance?.HidePrompt(this); return; }
            if (FishingGame.Instance != null && FishingGame.Instance.Busy) return;
            if (SleepSystem.Instance != null && SleepSystem.Instance.Sleeping) return;

            Target = PhotoSubject.Best(player.transform.position, player.transform.forward, range);

            var hud = GameHud.Instance;
            if (Target == null)
            {
                if (State == Mode.Aiming) State = Mode.Idle;
                hud?.HidePrompt(this);
                return;
            }

            State = Mode.Aiming;
            hud?.ShowPrompt(this, "F   photograph the "
                + Target.species.commonName.ToLowerInvariant(), 1);

            bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (pressed) StartCoroutine(Shoot(Target));
        }

        // --- the shot ------------------------------------------------------------

        IEnumerator Shoot(PhotoSubject subject)
        {
            State = Mode.Shooting;
            GameHud.Instance?.HidePrompt(this);

            subject.SetCalm(true);
            FacePlayerAt(subject);

            // Arrow keys are also the walk keys. Nobody should wander out of their own
            // shot because the minigame and the movement share a keyboard.
            var mover = PlayerMover.Instance;
            bool moverWas = mover != null && mover.enabled;

            // Harder subjects want a longer sequence and less time to think.
            float rarity = Mathf.Clamp01(subject.species.rarity);
            int steps = 3 + Mathf.RoundToInt(rarity * 3f);
            float perStep = Mathf.Lerp(2.2f, 1.4f, rarity);

            int misses = 0;
            if (mover != null) mover.enabled = false;
            yield return new WaitForSeconds(0.3f);          // let the turn finish first
            yield return _sequence.Run(subject, steps, perStep, result => misses = result);
            if (mover != null) mover.enabled = moverWas;

            var photo = Capture(subject);
            Spoil(photo, misses, steps);
            ShotsTaken++;
            float score = Grade(subject, misses);

            subject.MarkPhotographed();
            State = Mode.Reviewing;

            bool keep = true;
            yield return _review.Show(subject.species, photo, score, misses, k => keep = k);

            if (keep)
            {
                bool improved = _state.album.Record(subject.species.id, score, photo, _state.day);
                GameHud.Instance?.OnSpeciesLogged(subject.species);
                if (!improved) GameHud.Instance?.Say("you already had a better one");
                _state.AddBond(0.03f + score * 0.04f);
            }
            else
            {
                GameHud.Instance?.Say("discarded");
            }

            State = Mode.Idle;
        }

        /// <summary>Turns the surveyor to face what they are shooting. You cannot photograph sideways.</summary>
        void FacePlayerAt(PhotoSubject subject)
        {
            var player = PlayerMover.Instance;
            if (player == null) return;
            Vector3 to = subject.AimPoint - player.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            player.FaceTowards(to);
        }

        /// <summary>
        /// The score. Framing is not asked for separately - the sequence IS the steadiness
        /// of your hands, and light and distance are the two things a real photograph of a
        /// wild animal actually turns on.
        /// </summary>
        float Grade(PhotoSubject subject, int misses)
        {
            float rarity = Mathf.Clamp01(subject.species.rarity);
            int steps = 3 + Mathf.RoundToInt(rarity * 3f);

            float steadiness = 1f - Mathf.Clamp01(misses / (float)steps) * 0.92f;

            var cycle = DayCycle.Instance;
            float light = cycle != null ? cycle.LightQuality : 1f;

            var player = PlayerMover.Instance;
            float distance = player != null
                ? Vector3.Distance(player.transform.position, subject.AimPoint) : 8f;
            // Close is better, but standing on top of it is not - there is a sweet spot.
            float framing = 1f - Mathf.Abs(distance - 6.5f) / 18f;

            // Steadiness carries it. Light and framing can lift a good shot or take the
            // shine off one, but they should never rescue a fumbled one.
            float score = steadiness * (0.62f + light * 0.24f + Mathf.Clamp01(framing) * 0.14f);
            // A rare animal photographed at all is worth something.
            return Mathf.Clamp01(score * Mathf.Lerp(0.95f, 1.1f, rarity));
        }

        // --- the image -----------------------------------------------------------

        /// <summary>
        /// Renders the actual scene from a second camera aimed at the subject, then spoils
        /// it by hand according to how the sequence went.
        /// </summary>
        Texture2D Capture(PhotoSubject subject)
        {
            EnsureLens();
            if (_lens == null) return null;

            // From where the surveyor is standing, not from where the game camera hangs.
            // Borrowing the camera's position gave every entry in the album the same
            // overhead angle, which reads as a satellite pass rather than a photograph.
            var player = PlayerMover.Instance;
            Vector3 from = player != null
                ? player.transform.position + Vector3.up * 1.55f
                : subject.AimPoint + Vector3.up * 2f - Vector3.forward * 6f;

            // Aim at the middle of the animal rather than at whatever point the subject
            // nominated. A flowering specimen's aim sits high on the plant, and pointing
            // there from eye level tipped the lens into the sky - which is why some album
            // entries came back as a plain dark rectangle.
            Vector3 look = SubjectCentre(subject);

            Vector3 toSubject = look - from;
            float rise = Mathf.Atan2(toSubject.y, new Vector2(toSubject.x, toSubject.z).magnitude)
                       * Mathf.Rad2Deg;
            if (rise > 8f)
            {
                // Level it off. There is nothing worth photographing above the treeline.
                float flat = new Vector2(toSubject.x, toSubject.z).magnitude;
                toSubject.y = flat * Mathf.Tan(8f * Mathf.Deg2Rad);
            }

            _lens.transform.position = from;
            _lens.transform.rotation = Quaternion.LookRotation(toSubject, Vector3.up);

            // Zoom so the subject fills a consistent share of the frame however far away
            // it is. A fixed field of view made a distant dove a speck and a nearby
            // mithun a wall of black.
            float distance = Vector3.Distance(from, look);
            float size = Mathf.Max(0.6f, SubjectHeight(subject));
            float wanted = 2f * Mathf.Atan(size / (0.42f * 2f * distance)) * Mathf.Rad2Deg;
            _lens.fieldOfView = Mathf.Clamp(wanted, 12f, 46f);

            // The photographer is not in their own photograph.
            var hidden = Hide(player != null ? player.gameObject : null);

            var rt = RenderTexture.GetTemporary(photoWidth, photoHeight, 24,
                RenderTextureFormat.ARGB32);
            _lens.targetTexture = rt;
            _lens.enabled = true;
            _lens.Render();
            _lens.enabled = false;
            _lens.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var photo = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
            photo.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var r in hidden) if (r != null) r.enabled = true;

            Develop(photo);
            photo.name = subject.species.id;
            photo.wrapMode = TextureWrapMode.Clamp;
            return photo;
        }

        /// <summary>The visible extent of the thing, ignoring its motes and sparkles.</summary>
        static Bounds SubjectBounds(PhotoSubject subject)
        {
            var bounds = new Bounds(subject.AimPoint, Vector3.one * 0.6f);
            bool first = true;
            foreach (var r in subject.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        static Vector3 SubjectCentre(PhotoSubject subject) => SubjectBounds(subject).center;

        /// <summary>
        /// How tall the thing is. The field of view being set is a vertical one, so using
        /// the longest dimension made a long low animal frame as though it were small.
        /// </summary>
        static float SubjectHeight(PhotoSubject subject)
        {
            var size = SubjectBounds(subject).size;
            // A little wider than the body, so nothing is cropped at the edges.
            return Mathf.Max(size.y, Mathf.Max(size.x, size.z) * 0.55f);
        }

        static readonly List<Renderer> Hidden = new List<Renderer>();

        static List<Renderer> Hide(GameObject go)
        {
            Hidden.Clear();
            if (go == null) return Hidden;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                Hidden.Add(r);
            }
            return Hidden;
        }

        /// <summary>
        /// Develops the raw render into something that reads as a print: a warm lift in
        /// the shadows, a little extra contrast, and a soft vignette. Straight framebuffer
        /// output looks like a screenshot, which is exactly what it must not look like.
        /// </summary>
        static void Develop(Texture2D photo)
        {
            var pixels = photo.GetPixels();
            int w = photo.width, h = photo.height;
            var centre = new Vector2(w * 0.5f, h * 0.5f);
            float radius = centre.magnitude;

            // A print pulled from a dark frame. Night shots should read as night, not as a
            // black rectangle you cannot tell apart from any other black rectangle, so the
            // exposure is lifted toward legibility and then stopped well short of daylight.
            float mean = 0f;
            for (int i = 0; i < pixels.Length; i++)
                mean += pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
            mean /= pixels.Length;

            float lift = mean < 0.24f ? Mathf.Clamp(0.24f / Mathf.Max(0.02f, mean), 1f, 3.2f) : 1f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var c = pixels[i];
                c.r *= lift; c.g *= lift; c.b *= lift;

                // Warm the highlights, cool and lift the shadows very slightly.
                float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                c.r = Mathf.Lerp(c.r * 1.05f + 0.02f, c.r, luma);
                c.b = Mathf.Lerp(c.b * 0.96f + 0.03f, c.b, luma);

                // A gentle S-curve. Film does not have a straight response.
                c.r = Contrast(c.r); c.g = Contrast(c.g); c.b = Contrast(c.b);

                float edge = Vector2.Distance(new Vector2(x, y), centre) / radius;
                float vignette = 1f - Mathf.SmoothStep(0.62f, 1.05f, edge) * 0.42f;
                c.r *= vignette; c.g *= vignette; c.b *= vignette;

                pixels[i] = c;
            }

            photo.SetPixels(pixels);
            photo.Apply();
        }

        static float Contrast(float v) => Mathf.Clamp01(v * v * (3f - 2f * v) * 0.22f + v * 0.78f);

        void EnsureLens()
        {
            if (_lens != null) return;
            var main = Camera.main;
            if (main == null) return;

            var go = new GameObject("PhotoLens");
            go.transform.SetParent(transform, false);
            _lens = go.AddComponent<Camera>();
            _lens.CopyFrom(main);
            _lens.enabled = false;
            _lens.targetTexture = null;
            // No audio listener, and never the one the game is drawn with.
            _lens.tag = "Untagged";
            _lens.depth = main.depth - 10;
        }

        /// <summary>
        /// Spoils a clean render in proportion to the fumbles: the scanline shear of a
        /// jogged camera, and the colour draining out of a shot that was not held.
        /// </summary>
        public static void Spoil(Texture2D photo, int misses, int steps)
        {
            if (photo == null || misses <= 0) return;

            // Squared, so one miss out of five is a barely-soft print and four out of
            // five is a smear. Linear made a single slip look like a disaster.
            float ruin = Mathf.Clamp01(misses / Mathf.Max(1f, steps));
            ruin *= ruin;
            if (ruin < 0.02f) return;

            var pixels = photo.GetPixels();
            int w = photo.width, h = photo.height;
            var copy = (Color[])pixels.Clone();

            var rng = new System.Random(misses * 7717 + w);
            float shear = ruin * 9f;

            for (int y = 0; y < h; y++)
            {
                // Rows slide sideways by a smooth wobble plus a little grit, which is what
                // a hand-held shot at a slow shutter actually looks like.
                float wave = Mathf.Sin(y * 0.09f) * shear
                           + (float)(rng.NextDouble() - 0.5) * shear * 0.6f;
                int offset = Mathf.RoundToInt(wave);
                if (offset == 0) continue;

                for (int x = 0; x < w; x++)
                {
                    int source = Mathf.Clamp(x + offset, 0, w - 1);
                    pixels[y * w + x] = copy[y * w + source];
                }
            }

            // Then a soft blur and a desaturation, both scaled by the same ruin.
            if (ruin > 0.3f)
            {
                var blurred = (Color[])pixels.Clone();
                for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    Color sum = blurred[y * w + x] * 4f
                              + blurred[y * w + x - 1] + blurred[y * w + x + 1]
                              + blurred[(y - 1) * w + x] + blurred[(y + 1) * w + x];
                    pixels[y * w + x] = Color.Lerp(pixels[y * w + x], sum / 8f, (ruin - 0.3f) * 1.4f);
                }
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                float grey = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
                pixels[i] = Color.Lerp(pixels[i], new Color(grey, grey, grey), ruin * 0.45f);
            }

            photo.SetPixels(pixels);
            photo.Apply();
        }
    }
}
