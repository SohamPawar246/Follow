#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using Follow.Core;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Walks the clock through the small hours and asks, at each step, the
    /// only question that matters: is there anything the player can actually do?
    ///
    /// Reported for a player standing away from camp, which is where somebody caught out
    /// by nightfall will be.
    /// </summary>
    public class SmallHoursProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            var cycle = DayCycle.Instance;
            var player = PlayerMover.Instance;
            var state = GameState.Instance;
            if (cycle == null || player == null || state == null) yield break;

            // Somebody out in the wood, with wood in their pack and a fire not yet lit.
            state.sticks = 5;
            state.campfireBuilt = true;
            state.campfireFuel = 0f;

            var fire = Campfire.Instance;
            if (fire != null)
                player.transform.position = fire.transform.position + new Vector3(30f, 0.3f, 0f);

            cycle.paused = true;
            yield return null;

            _log.AppendLine("standing 30 m from an unlit fire, 5 sticks, nothing in view");
            _log.AppendLine("");
            _log.AppendLine("Time01  clock     daylight  tooDark  sleepOffered  prompt");

            foreach (float t in new[] { 0.70f, 0.80f, 0.90f, 0.94f, 0.96f, 0.98f, 0.02f })
            {
                cycle.SetTime(t);

                float settle = 0f;
                while (settle < 0.4f) { settle += Time.deltaTime; yield return null; }

                _log.AppendLine(t.ToString("0.00")
                    + "    " + cycle.ClockText.PadRight(9)
                    + " " + cycle.Daylight.ToString("0.00").PadRight(9)
                    + " " + Photography.TooDark(player).ToString().PadRight(8)
                    + " " + cycle.IsDusk.ToString().PadRight(13)
                    + " \"" + (GameHud.Instance != null ? GameHud.Instance.PromptText : "?") + "\"");
            }

            // And can they still walk?
            cycle.SetTime(0.90f);
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine("");
            _log.AppendLine("movement frozen at 3am: " + player.Frozen);

            Vector3 from = player.transform.position;
            float moved = 0f;
            var cc = player.GetComponent<CharacterController>();
            while (moved < 1.2f)
            {
                moved += Time.deltaTime;
                if (cc != null && cc.enabled && !player.Frozen)
                    cc.Move(new Vector3(3f, -6f, 0f) * Time.deltaTime);
                yield return null;
            }
            _log.AppendLine("walked " + Vector3.Distance(from, player.transform.position).ToString("0.0")
                + " m  " + (Vector3.Distance(from, player.transform.position) > 1f ? "OK" : "STUCK"));

            cycle.paused = false;
            Debug.Log("SmallHoursProbe:\n" + _log);
        }
    }
}
#endif
