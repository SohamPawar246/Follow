#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using Follow.Core;
using Follow.Dog;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Checks the specific things that were reported broken, one at a time,
    /// and writes down what it found. Screenshots only where a picture settles it.
    /// </summary>
    public class RegressionProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            Line("--- firewood on the ground ---");
            int sticksNear = 0;
            foreach (var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (pickup.kind == PickupKind.Stick) sticksNear++;
            Line("  sticks in the loaded world = " + sticksNear);
            Line("  nearest stick = " + NearestStick().ToString("0.0") + " m");

            Line("--- audio ---");
            var sound = Soundscape.Instance;
            if (sound == null) Line("  NO SOUNDSCAPE");
            else
            {
                int playing = 0;
                foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                    if (source.isPlaying) playing++;
                Line("  audio sources playing = " + playing);
            }

            yield return Shot("30_start");

            // Walk to a pond and see whether the game offers to fish.
            Line("--- fishing ---");
            var player = PlayerMover.Instance;
            if (player != null &&
                WorldComposer.NearestPond(new Vector2(player.transform.position.x,
                                                      player.transform.position.z), 220f, out var pond))
            {
                var edge = pond.position - (pond.position - new Vector2(player.transform.position.x,
                    player.transform.position.z)).normalized * (pond.radius + 2f);
                Teleport(player, new Vector3(edge.x, 0f, edge.y));
                Line("  stood at the water's edge, pond radius " + pond.radius.ToString("0.0"));
                yield return new WaitForSeconds(2.5f);

                Line("  photography busy = " + (Photography.Instance != null && Photography.Instance.Busy));
                Line("  fishing prompt shown = " + GameHud.Instance.PromptText);
                yield return Shot("31_pond");
            }
            else Line("  no pond within 220 m");

            // The clock has to roll the date over on its own.
            Line("--- the day rolling over ---");
            var state = GameState.Instance;
            int before = state.day;
            DayCycle.Instance.SetTime(0.985f);
            yield return new WaitForSeconds(6f);
            Line("  day went from " + before + " to " + state.day);

            // And the dog has to stay out of the water.
            Line("--- the dog and the water ---");
            var dog = DogBrain.Instance;
            float wettest = 0f;
            for (int i = 0; i < 240; i++)
            {
                if (dog != null) wettest = Mathf.Max(wettest, HowWet(dog.transform.position));
                yield return null;
            }
            Line("  deepest the dog got into a pond = " + wettest.ToString("0.00")
                 + " (0 means she stayed dry)");

            System.IO.File.WriteAllText("Logs/regression.txt", _log.ToString());
            Debug.Log("RegressionProbe:\n" + _log);
        }

        static void Teleport(PlayerMover player, Vector3 to)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(to.x, WorldComposer.Height(to.x, to.z) + 0.4f, to.z);
            if (cc != null) cc.enabled = true;
        }

        static float NearestStick()
        {
            var player = PlayerMover.Instance;
            if (player == null) return -1f;

            float best = 9999f;
            foreach (var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (pickup.kind != PickupKind.Stick) continue;
                best = Mathf.Min(best, Vector3.Distance(player.transform.position, pickup.transform.position));
            }
            return best;
        }

        /// <summary>How far inside a pond a point is, in metres. Zero when clear of them.</summary>
        static float HowWet(Vector3 point)
        {
            var flat = new Vector2(point.x, point.z);
            var near = WorldComposer.LandmarksNear(flat, 60f);
            float worst = 0f;
            for (int i = 0; i < near.Count; i++)
            {
                if (near[i].kind != WorldComposer.LandmarkKind.Pond) continue;
                worst = Mathf.Max(worst, near[i].radius - Vector2.Distance(flat, near[i].position));
            }
            return Mathf.Max(0f, worst);
        }

        IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot("Logs/probe_" + name + ".png", 1);
            yield return new WaitForSeconds(1f);
        }

        void Line(string text) => _log.AppendLine(text);
    }
}
#endif
