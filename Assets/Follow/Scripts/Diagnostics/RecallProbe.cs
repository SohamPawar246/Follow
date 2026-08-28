#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Video;
using Follow.Core;
using Follow.Dog;
using Follow.Game;
using Follow.UI;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Three questions: how often does she actually bark over a stretch of
    /// ordinary play, does a whistle move her, and is the studio logo where the intro
    /// will look for it.
    /// </summary>
    public class RecallProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();
        int _barks;
        float _firstBark = -1f;
        float _lastBark = -1f;
        float _shortestGap = 999f;

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2f);

            Logo();
            yield return Barking();
            yield return Recall();

            Debug.Log("RecallProbe:\n" + _log);
        }

        // --- the intro ------------------------------------------------------------

        void Logo()
        {
            _log.AppendLine("--- studio logo ---");

            var named = Resources.Load<VideoClip>(LogoIntro.ResourceName);
            var all = Resources.LoadAll<VideoClip>("");

            _log.AppendLine("  clips in Resources: " + (all == null ? 0 : all.Length));
            if (all != null)
                foreach (var c in all)
                    if (c != null)
                        _log.AppendLine("    \"" + c.name + "\"  "
                            + c.width + "x" + c.height
                            + "  " + c.length.ToString("0.0") + "s"
                            + "  audio tracks " + c.audioTrackCount);

            _log.AppendLine("  found by exact name: " + (named != null));
            _log.AppendLine("  the intro will play one: "
                + (all != null && all.Length > 0 ? "yes  OK" : "no - falls back to the card"));
        }

        // --- how noisy is she really ------------------------------------------------

        IEnumerator Barking()
        {
            _log.AppendLine("--- barking, over 25s of ordinary play ---");

            var dog = DogBrain.Instance;
            if (dog == null) yield break;

            dog.Barked += OnBark;

            float t = 0f;
            var seen = new System.Collections.Generic.Dictionary<DogState, float>();
            while (t < 25f)
            {
                float dt = Time.deltaTime;
                t += dt;
                seen.TryGetValue(dog.State, out float held);
                seen[dog.State] = held + dt;
                yield return null;
            }

            dog.Barked -= OnBark;

            _log.AppendLine("  barks: " + _barks + " in 25s");
            _log.AppendLine("  shortest gap between two: "
                + (_shortestGap > 900f ? "n/a" : _shortestGap.ToString("0.0") + "s")
                + "   (floor is " + dog.barkFloor.ToString("0.0") + "s)"
                + (_shortestGap >= dog.barkFloor - 0.2f ? "  OK" : "  FLOOR BREACHED"));

            _log.AppendLine("  time spent in each state:");
            foreach (var pair in seen)
                _log.AppendLine("    " + pair.Key + "  " + pair.Value.ToString("0.0") + "s");
        }

        void OnBark()
        {
            float now = Time.time;
            if (_lastBark >= 0f) _shortestGap = Mathf.Min(_shortestGap, now - _lastBark);
            if (_firstBark < 0f) _firstBark = now;
            _lastBark = now;
            _barks++;
        }

        // --- does the whistle do anything -------------------------------------------

        IEnumerator Recall()
        {
            _log.AppendLine("--- the whistle ---");

            var dog = DogBrain.Instance;
            var player = PlayerMover.Instance;
            var state = GameState.Instance;
            if (dog == null || player == null || state == null) yield break;

            // Wait until she is genuinely off doing something, which is when a whistle
            // is worth blowing and exactly when the old one did nothing.
            float wait = 0f;
            while (wait < 14f && dog.State != DogState.Range && dog.State != DogState.Scent)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            state.bond = 0.12f;                       // a fresh run's bond
            var before = dog.State;
            float distanceBefore = dog.DistanceToPlayer;

            dog.Whistle();
            _log.AppendLine("  whistled while she was " + before
                + ", " + distanceBefore.ToString("0.0") + " m away");
            _log.AppendLine("  recall window opened: " + dog.Recalled
                            + (dog.Recalled ? "  OK" : "  SHE IGNORED IT"));

            // Did she stay come, or turn round and leave again on the next frame?
            // Watch a little past the recall window, so leaving when it lapses reads as
            // the window ending rather than as her ignoring the whistle.
            float window = Mathf.Lerp(dog.recallByBond.x, dog.recallByBond.y, state.bond);
            float held = 0f;
            bool wandered = false;
            float closest = distanceBefore;
            while (held < window + 1f)
            {
                held += Time.deltaTime;
                closest = Mathf.Min(closest, dog.DistanceToPlayer);
                if (dog.State == DogState.Range || dog.State == DogState.Scent)
                { wandered = true; break; }
                yield return null;
            }

            _log.AppendLine("  recall window at this bond: " + window.ToString("0.0") + "s");
            _log.AppendLine("  state after " + held.ToString("0.0") + "s: " + dog.State
                            + (wandered && held < window - 0.5f
                               ? "  LEFT EARLY" : "  held the window  OK"));
            _log.AppendLine("  closed from " + distanceBefore.ToString("0.0")
                            + " m to " + closest.ToString("0.0") + " m"
                            + (closest < distanceBefore - 1f || closest < 4f ? "  OK" : "  DID NOT COME"));
        }
    }
}
#endif
