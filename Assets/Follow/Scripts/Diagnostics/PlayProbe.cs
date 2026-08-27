#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using Follow.Core;
using Follow.Game;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Plays the first minute of a run on its own and writes down what it
    /// saw, with screenshots at the moments worth looking at. Exists so the game can be
    /// checked without a person having to sit and watch it.
    /// </summary>
    public class PlayProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        // No arming flag: entering play mode reloads the domain and would clear a static
        // one anyway. The probe runs because it is in the scene, and is deleted after.
        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);
            Note("morning");
            yield return Shot("01_morning");

            // Walk a little, so streaming and the pickups get exercised.
            var player = PlayerMover.Instance;
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                float t = 0f;
                while (t < 4f)
                {
                    t += Time.deltaTime;
                    if (cc != null) cc.Move(new Vector3(3f, -6f, 3f) * Time.deltaTime);
                    yield return null;
                }
            }
            Note("after walking");
            yield return Shot("02_walked");

            // Give the fire what it needs and light it.
            var state = GameState.Instance;
            if (state != null) state.AddSticks(8);
            yield return new WaitForSeconds(0.4f);

            if (player != null && Campfire.Instance != null)
            {
                var cc = player.GetComponent<CharacterController>();
                Vector3 to = Campfire.Instance.transform.position;
                float guard = 0f;
                while (guard < 8f && Vector3.Distance(player.transform.position, to) > 3f)
                {
                    guard += Time.deltaTime;
                    Vector3 dir = (to - player.transform.position).normalized;
                    if (cc != null) cc.Move((dir * 5f + Vector3.down * 6f) * Time.deltaTime);
                    yield return null;
                }
            }

            if (state != null && Campfire.Instance != null)
            {
                state.campfireBuilt = true;
                state.campfireFuel = Campfire.Instance.maxFuel;
            }
            yield return new WaitForSeconds(1.2f);
            Note("fire lit");
            yield return Shot("03_fire");

            // Wind the clock round to the middle of the night.
            var cycle = DayCycle.Instance;
            if (cycle != null)
            {
                cycle.paused = true;
                cycle.SetTime(0.72f);
            }
            yield return new WaitForSeconds(2.5f);
            Note("night");
            yield return Shot("04_night");

            System.IO.File.WriteAllText("Logs/play_probe.txt", _log.ToString());
            Debug.Log("PlayProbe finished:\n" + _log);
        }

        IEnumerator Shot(string name)
        {
            string path = "Logs/probe_" + name + ".png";
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSeconds(1.2f);
            _log.AppendLine("  captured " + path);
        }

        void Note(string moment)
        {
            var state = GameState.Instance;
            var cycle = DayCycle.Instance;
            var dog = Follow.Dog.DogBrain.Instance;
            var fire = Campfire.Instance;

            _log.AppendLine("--- " + moment + " ---");
            if (cycle != null)
                _log.AppendLine("  clock " + cycle.ClockText + "  t=" + cycle.Time01.ToString("0.000")
                    + "  daylight=" + cycle.Daylight.ToString("0.00")
                    + "  dusk=" + cycle.IsDusk + " dark=" + cycle.IsDark);
            if (state != null)
                _log.AppendLine("  day " + state.day + "  sticks=" + state.sticks + " food=" + state.food
                    + "  food bar=" + state.nourishment.ToString("0.00")
                    + "  water=" + state.hydration.ToString("0.00")
                    + "  energy=" + state.energy.ToString("0.00")
                    + "  bond=" + state.bond.ToString("0.00"));
            if (dog != null)
                _log.AppendLine("  dog " + dog.State + " at " + dog.transform.position
                    + " (" + dog.DistanceToPlayer.ToString("0.0") + " m away)");
            if (fire != null)
                _log.AppendLine("  fire built=" + fire.IsBuilt + " lit=" + fire.IsLit
                    + " warmth=" + fire.Warmth.ToString("0.00"));

            _log.AppendLine("  scent points = " + Follow.Dog.ScentPoint.Active.Count
                + ", photo subjects = " + PhotoSubject.Active.Count
                + ", flora = " + FindObjectsByType<FloraSpecimen>(FindObjectsSortMode.None).Length
                + ", pickups = " + FindObjectsByType<Pickup>(FindObjectsSortMode.None).Length);

            var streamer = WorldStreamer.Instance;
            if (streamer != null)
            {
                var chunks = GameObject.Find("Chunks");
                _log.AppendLine("  chunks live = " + (chunks != null ? chunks.transform.childCount : 0));
            }

            var player = PlayerMover.Instance;
            if (player != null)
                _log.AppendLine("  player at " + player.transform.position
                    + "  ground here = " + WorldComposer.Height(
                        player.transform.position.x, player.transform.position.z).ToString("0.00"));
        }
    }
}
#endif
