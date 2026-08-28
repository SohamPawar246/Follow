#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using Follow.Core;
using Follow.Game;
using Follow.UI;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Starves the player at night and watches whether the blackout hands
    /// control back. The collapse is the one sequence that takes the screen away from the
    /// player entirely, so it is the one that strands them if it goes wrong.
    /// </summary>
    public class CollapseProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            var state = GameState.Instance;
            var player = PlayerMover.Instance;
            var cycle = DayCycle.Instance;
            if (state == null || player == null) yield break;

            // Deep night, well away from camp, and out of food - which is exactly the
            // corner the player was in.
            if (cycle != null) cycle.SetTime(0.88f);
            int dayBefore = state.day;

            _log.AppendLine("day " + dayBefore + ", nourishment forced to zero at 2am");
            state.nourishment = 0f;

            float waited = 0f;
            bool everFrozen = false;
            while (waited < 22f)
            {
                waited += Time.deltaTime;
                if (player.Frozen) everFrozen = true;
                yield return null;
            }

            _log.AppendLine("  movement was taken at some point: " + everFrozen);
            _log.AppendLine("  movement frozen now: " + player.Frozen
                + (player.Frozen ? "  STILL HELD" : "  OK"));
            _log.AppendLine("  timeScale: " + Time.timeScale
                + (Mathf.Approximately(Time.timeScale, 1f) ? "  OK" : "  NOT RUNNING"));
            _log.AppendLine("  a modal is up: " + UIModal.Any
                + (UIModal.Any ? "  BLOCKING" : "  OK"));
            _log.AppendLine("  day is now " + state.day + " (was " + dayBefore + ")");
            _log.AppendLine("  nourishment " + state.nourishment.ToString("0.00")
                + "  hydration " + state.hydration.ToString("0.00")
                + "  energy " + state.energy.ToString("0.00"));
            _log.AppendLine("  clock " + (cycle != null ? cycle.ClockText : "?"));
            _log.AppendLine("  prompt: \"" + (GameHud.Instance != null
                ? GameHud.Instance.PromptText : "?") + "\"");

            // Can they walk away from it?
            Vector3 from = player.transform.position;
            float t = 0f;
            var cc = player.GetComponent<CharacterController>();
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                if (cc != null && cc.enabled && !player.Frozen)
                    cc.Move(new Vector3(3f, -6f, 0f) * Time.deltaTime);
                yield return null;
            }
            float moved = Vector3.Distance(from, player.transform.position);
            _log.AppendLine("  walked " + moved.ToString("0.0") + " m afterwards"
                + (moved > 1f ? "  OK" : "  STUCK"));

            ScreenCapture.CaptureScreenshot("Logs/after_collapse.png", 1);
            yield return new WaitForSeconds(1f);

            Debug.Log("CollapseProbe:\n" + _log);
        }
    }
}
#endif
