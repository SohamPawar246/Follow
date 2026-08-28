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
    /// Editor-only. Reproduces a blackout and then reports everything that could leave the
    /// game unresponsive afterwards: where the player ended up, whether the camp is still
    /// standing, whether anything is still holding movement or the clock.
    /// </summary>
    public class StuckProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            Snapshot("before");
            yield return Shot("60_before");

            // Build the fire so the state after the blackout matches the report.
            var state = GameState.Instance;
            state.AddSticks(6);
            state.campfireBuilt = true;
            state.campfireFuel = Campfire.Instance.maxFuel;
            yield return new WaitForSeconds(1.5f);
            Snapshot("fire lit");

            // Now run them dry.
            _log.AppendLine("--- forcing a blackout ---");
            state.hydration = 0f;
            yield return new WaitForSeconds(6f);

            Snapshot("after the blackout");
            yield return Shot("61_after");

            // And check that walking still works.
            var player = PlayerMover.Instance;
            Vector3 from = player.transform.position;
            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.enabled) cc.Move(new Vector3(2.5f, -6f, 0f) * Time.deltaTime);
                yield return null;
            }
            _log.AppendLine("  moved " + Vector3.Distance(from, player.transform.position).ToString("0.0")
                            + " m under its own steam");

            System.IO.File.WriteAllText("Logs/stuck_probe.txt", _log.ToString());
            Debug.Log("StuckProbe:\n" + _log);
        }

        void Snapshot(string moment)
        {
            _log.AppendLine("--- " + moment + " ---");

            var state = GameState.Instance;
            var player = PlayerMover.Instance;
            var camp = GameObject.Find("Camp");
            var fire = Campfire.Instance;

            _log.AppendLine("  timeScale " + Time.timeScale + ", modal " + UIModal.Any);

            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                _log.AppendLine("  player at " + player.transform.position
                    + ", mover enabled " + player.enabled
                    + ", controller enabled " + (cc != null && cc.enabled));
                _log.AppendLine("  ground under player = "
                    + WorldComposer.Height(player.transform.position.x, player.transform.position.z).ToString("0.00")
                    + ", slope " + WorldComposer.Slope(player.transform.position.x,
                        player.transform.position.z).ToString("0.00"));
            }

            if (camp != null)
            {
                _log.AppendLine("  camp at " + camp.transform.position
                    + ", " + camp.transform.childCount + " children");
                foreach (Transform child in camp.transform)
                    _log.AppendLine("    " + child.name + " active=" + child.gameObject.activeInHierarchy
                        + " at " + child.position);
            }
            else _log.AppendLine("  NO CAMP OBJECT");

            if (fire != null)
                _log.AppendLine("  fire built " + fire.IsBuilt + ", lit " + fire.IsLit
                    + ", warmth " + fire.Warmth.ToString("0.00"));

            if (state != null)
                _log.AppendLine("  day " + state.day + " sticks " + state.sticks + " food " + state.food
                    + " water " + state.hydration.ToString("0.00")
                    + " nourishment " + state.nourishment.ToString("0.00"));

            var survival = SurvivalSystem.Instance;
            if (survival != null) _log.AppendLine("  collapsing = " + survival.Collapsing);

            var sleep = SleepSystem.Instance;
            if (sleep != null) _log.AppendLine("  sleeping = " + sleep.Sleeping);

            var fishing = FishingGame.Instance;
            if (fishing != null) _log.AppendLine("  fishing busy = " + fishing.Busy);

            var photo = Photography.Instance;
            if (photo != null) _log.AppendLine("  photography = " + photo.State);

            var chunks = GameObject.Find("Chunks");
            _log.AppendLine("  chunks live = " + (chunks != null ? chunks.transform.childCount : 0));
        }

        IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot("Logs/probe_" + name + ".png", 1);
            yield return new WaitForSeconds(1f);
        }
    }
}
#endif
