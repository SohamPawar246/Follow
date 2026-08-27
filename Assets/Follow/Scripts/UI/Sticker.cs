using System.Collections.Generic;
using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// Shape generator for the sticker look: fat squircles with heavy dark outlines,
    /// the way Cult of the Lamb and Stardew build their panels. Everything is generated
    /// as pure alpha so a single sprite can be tinted per element, and outline and fill
    /// are drawn as two stacked images rather than baked into one texture.
    ///
    /// Procedural on purpose - shipping raw Kenney art makes a jam entry look like every
    /// other jam entry, and the outline weight is the whole personality of this style.
    /// </summary>
    public static class Sticker
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// A superellipse rather than a circle-cornered rect. The flatter shoulder reads
        /// friendlier and is the difference between "dialog box" and "sticker".
        /// </summary>
        public static Sprite Squircle(int radius = 28, float exponent = 4.2f)
        {
            string key = "sq_" + radius + "_" + exponent.ToString("0.0");
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            int size = radius * 2 + 8;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    px[y * size + x] = Alpha(Coverage(x, y, size, size, radius, exponent));
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Finish(tex, key, radius + 2);
        }

        /// <summary>Pill / chip shape for counters and tags.</summary>
        public static Sprite Pill(int height = 48)
        {
            string key = "pill_" + height;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            int r = height / 2;
            int size = height;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = Alpha(Coverage(x, y, size, size, r, 2f));
            tex.SetPixels32(px);
            tex.Apply();
            return Finish(tex, key, r - 1);
        }

        public static Sprite Circle(int diameter = 96)
        {
            string key = "circ_" + diameter;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var tex = NewTex(diameter, diameter);
            var px = new Color32[diameter * diameter];
            float r = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * diameter + x] = Alpha(Mathf.Clamp01(r - d + 0.5f));
            }
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A ring segment, for the Stardew-style day arc. Angles are degrees measured
        /// counter-clockwise from east, so 180 to 0 sweeps across the top.
        /// </summary>
        public static Sprite Arc(int size, float thickness, float startDeg, float endDeg)
        {
            string key = "arc_" + size + "_" + thickness + "_" + startDeg + "_" + endDeg;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float outer = half - 1f;
            float inner = outer - thickness;

            float a0 = Mathf.Min(startDeg, endDeg) * Mathf.Deg2Rad;
            float a1 = Mathf.Max(startDeg, endDeg) * Mathf.Deg2Rad;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float band = Mathf.Min(r - inner, outer - r);
                    if (band <= -1f) { px[y * size + x] = Alpha(0f); continue; }

                    float ang = Mathf.Atan2(dy, dx);
                    if (ang < 0f) ang += Mathf.PI * 2f;
                    if (ang < a0 || ang > a1) { px[y * size + x] = Alpha(0f); continue; }

                    px[y * size + x] = Alpha(Mathf.Clamp01(band + 0.5f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();

            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Flat 1x1 white, for washes and scrims.</summary>
        /// <summary>
        /// A fat rounded triangle, point up. The compass markers need something that reads
        /// as a direction at twenty-four pixels, and a rotated square does not.
        /// </summary>
        public static Sprite Triangle(int size = 64)
        {
            string key = "tri_" + size;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var tex = NewTex(size, size);
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Normalised, origin at the base centre, apex at the top.
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size;

                // Half-width shrinks to nothing at the apex, with a little belly.
                float halfWidth = Mathf.Lerp(0.94f, 0.02f, Mathf.Pow(v, 0.85f));
                float inside = halfWidth - Mathf.Abs(u);
                // Feather by a pixel so the edge is not a staircase.
                float coverage = Mathf.Clamp01(inside * size * 0.5f + 0.5f);
                if (v < 0.06f) coverage *= Mathf.Clamp01(v / 0.06f * size * 0.1f);

                px[y * size + x] = Alpha(coverage);
            }

            tex.SetPixels32(px);
            tex.Apply();

            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Solid()
        {
            if (Cache.TryGetValue("solid", out var hit) && hit != null) return hit;
            var tex = NewTex(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            var s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            s.hideFlags = HideFlags.HideAndDontSave;
            Cache["solid"] = s;
            return s;
        }

        /// <summary>Radial darkening for the screen edges.</summary>
        public static Sprite Vignette(int size = 256, float inner = 0.34f, float power = 1.5f)
        {
            string key = "vig_" + size + "_" + inner.ToString("0.00") + "_" + power.ToString("0.0");
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                px[y * size + x] = Alpha(Mathf.Pow(Mathf.Clamp01((d - inner) / (1f - inner)), power));
            }
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        // --- internals ----------------------------------------------------------

        static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        static Color32 Alpha(float a) => new Color(1f, 1f, 1f, Mathf.Clamp01(a));

        /// <summary>Antialiased coverage of a superellipse-cornered rectangle.</summary>
        static float Coverage(int x, int y, int w, int h, float radius, float exponent)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float dx = 0f, dy = 0f;

            if (cx < radius) dx = radius - cx;
            else if (cx > w - radius) dx = cx - (w - radius);

            if (cy < radius) dy = radius - cy;
            else if (cy > h - radius) dy = cy - (h - radius);

            if (dx <= 0f && dy <= 0f) return 1f;

            // Superellipse: |x|^n + |y|^n = r^n. Higher n flattens the shoulder.
            float nx = dx / radius, ny = dy / radius;
            float e = Mathf.Pow(Mathf.Pow(nx, exponent) + Mathf.Pow(ny, exponent), 1f / exponent);
            return Mathf.Clamp01((1f - e) * radius + 0.5f);
        }

        static Sprite Finish(Texture2D tex, string key, int inset)
        {
            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(inset, inset, inset, inset));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
