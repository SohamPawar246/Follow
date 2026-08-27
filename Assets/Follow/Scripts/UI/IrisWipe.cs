using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Follow.UI
{
    /// <summary>
    /// The circle that closes over the screen when you collapse, and opens again when you
    /// wake up at camp.
    ///
    /// Drawn as a mesh rather than a scaled sprite, because the shape needed is a hole:
    /// everything outside a shrinking circle has to stay opaque out to the screen corners,
    /// and no amount of scaling a texture gives you that. A ring of triangles between an
    /// inner circle and an outer rectangle does, in about forty lines.
    /// </summary>
    public class IrisWipe : MaskableGraphic
    {
        [Range(0f, 1f)] public float openness = 1f;
        public int segments = 72;

        /// <summary>Where the hole sits, in this rect's local space. Follows the player.</summary>
        public Vector2 focus = Vector2.zero;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = GetPixelAdjustedRect();
            // The hole has to be able to clear the furthest corner from the focus.
            float reach = 0f;
            foreach (var corner in new[]
            {
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)
            })
                reach = Mathf.Max(reach, Vector2.Distance(corner, focus));

            float radius = reach * Mathf.Clamp01(openness);
            if (radius >= reach - 0.5f) return;   // fully open: draw nothing at all

            var tint = color;

            // The outer boundary is the rect, walked in the same angular order as the
            // inner circle so the two can be stitched with a triangle strip.
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;

                Vector2 inner0 = focus + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                Vector2 inner1 = focus + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                Vector2 outer0 = focus + RayToRect(a0, rect, focus);
                Vector2 outer1 = focus + RayToRect(a1, rect, focus);

                int b = vh.currentVertCount;
                vh.AddVert(inner0, tint, Vector2.zero);
                vh.AddVert(inner1, tint, Vector2.zero);
                vh.AddVert(outer1, tint, Vector2.zero);
                vh.AddVert(outer0, tint, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
                vh.AddTriangle(b, b + 2, b + 3);
            }
        }

        /// <summary>How far a ray at this angle travels before it leaves the rect.</summary>
        static Vector2 RayToRect(float angle, Rect rect, Vector2 from)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float tx = dir.x > 0f ? (rect.xMax - from.x) / dir.x
                     : dir.x < 0f ? (rect.xMin - from.x) / dir.x : float.MaxValue;
            float ty = dir.y > 0f ? (rect.yMax - from.y) / dir.y
                     : dir.y < 0f ? (rect.yMin - from.y) / dir.y : float.MaxValue;
            return dir * Mathf.Min(tx, ty);
        }

        public void SetOpenness(float value)
        {
            openness = Mathf.Clamp01(value);
            SetVerticesDirty();
        }

        /// <summary>Closes to nothing, runs the handover, then opens again.</summary>
        public IEnumerator Blink(float closeSeconds, float holdSeconds, float openSeconds,
            System.Action atTheDark = null)
        {
            yield return Sweep(1f, 0f, closeSeconds);
            atTheDark?.Invoke();
            if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Sweep(0f, 1f, openSeconds);
        }

        public IEnumerator Sweep(float from, float to, float seconds)
        {
            float t = 0f;
            SetOpenness(from);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                // Eased, so the last sliver of light lingers the way it should.
                SetOpenness(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetOpenness(to);
        }

        /// <summary>Builds one on its own canvas, above everything except the scene fade.</summary>
        public static IrisWipe Create(Transform parent, Color color)
        {
            var canvas = UIFactory.CreateCanvas("IrisCanvas", 800);
            canvas.transform.SetParent(parent, false);

            var rt = UIFactory.Stretch(UIFactory.Rect("Iris", canvas.transform));
            var iris = rt.gameObject.AddComponent<IrisWipe>();
            iris.color = color;
            iris.raycastTarget = false;
            iris.SetOpenness(1f);
            return iris;
        }
    }
}
