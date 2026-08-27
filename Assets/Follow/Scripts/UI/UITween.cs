using System;
using System.Collections;
using UnityEngine;

namespace Follow.UI
{
    /// <summary>
    /// Small easing helpers. Panels settle rather than appear, which is most of the
    /// difference between a cozy interface and a functional one.
    /// </summary>
    public static class UITween
    {
        /// <summary>Ease-out-back: overshoots slightly, then settles.</summary>
        public static float Settle(float t, float overshoot = 1.7f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        public static IEnumerator RiseIn(RectTransform rt, CanvasGroup group, float duration, float rise = 26f, float delay = 0f)
        {
            if (rt == null) yield break;
            Vector2 target = rt.anchoredPosition;
            Vector2 from = target - new Vector2(0f, rise);

            rt.anchoredPosition = from;
            if (group != null) group.alpha = 0f;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                float e = Settle(t, 1.35f);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
                if (group != null) group.alpha = EaseOut(Mathf.Clamp01(t * 1.6f));
                yield return null;
            }
            rt.anchoredPosition = target;
            if (group != null) group.alpha = 1f;
        }

        public static IEnumerator FadeGroup(CanvasGroup group, float to, float duration, Action onDone = null)
        {
            if (group == null) { onDone?.Invoke(); yield break; }
            float from = group.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                group.alpha = Mathf.Lerp(from, to, EaseInOut(t));
                yield return null;
            }
            group.alpha = to;
            onDone?.Invoke();
        }

        public static IEnumerator ScaleTo(Transform t, Vector3 to, float duration)
        {
            if (t == null) yield break;
            Vector3 from = t.localScale;
            float p = 0f;
            while (p < 1f)
            {
                p += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                t.localScale = Vector3.LerpUnclamped(from, to, Settle(p, 2.2f));
                yield return null;
            }
            t.localScale = to;
        }
    }
}
