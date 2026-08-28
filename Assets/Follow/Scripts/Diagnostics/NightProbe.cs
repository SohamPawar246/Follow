#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Walks the clock from afternoon into full dark, photographing the sky
    /// on the way, and checks that the night grade is actually being rendered and that
    /// the lens refuses to work once the light has gone.
    /// </summary>
    public class NightProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            var cycle = DayCycle.Instance;
            var player = PlayerMover.Instance;
            if (cycle == null || player == null) yield break;

            var cam = Camera.main;
            var data = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            _log.AppendLine("post-processing on the camera: "
                + (data != null ? data.renderPostProcessing.ToString() : "NO URP DATA"));

            NightMood mood = FindFirstObjectByType<NightMood>();
            Volume moodVolume = mood != null ? mood.GetComponent<Volume>() : null;

            cycle.paused = true;

            _log.AppendLine("");
            _log.AppendLine("time   daylight  nightGrade  tooDark  clock");
            foreach (float t in new[] { 0.45f, 0.55f, 0.62f, 0.68f, 0.75f, 0.85f })
            {
                cycle.SetTime(t);

                // Let the grade catch up - it is deliberately rate-limited.
                float settle = 0f;
                while (settle < 4f) { settle += Time.deltaTime; yield return null; }

                _log.AppendLine(t.ToString("0.00")
                    + "   " + cycle.Daylight.ToString("0.00")
                    + "      " + (moodVolume != null ? moodVolume.weight.ToString("0.00") : "-")
                    + "        " + Photography.TooDark(player)
                    + "     " + cycle.ClockText);

                ScreenCapture.CaptureScreenshot("Logs/night_" + t.ToString("0.00") + ".png", 1);
                yield return new WaitForSeconds(0.6f);
            }

            // And the exception: standing at a lit fire.
            var fire = Campfire.Instance;
            if (fire != null)
            {
                var state = Follow.Core.GameState.Instance;
                state.campfireBuilt = true;
                state.campfireFuel = 200f;
                yield return null;

                player.transform.position = fire.transform.position + new Vector3(2f, 0.2f, 0f);
                yield return new WaitForSeconds(0.4f);
                _log.AppendLine("");
                _log.AppendLine("at the lit fire, in the dark: tooDark = "
                    + Photography.TooDark(player)
                    + (Photography.TooDark(player) ? "  FIRE DOES NOT HELP" : "  OK"));

                player.transform.position = fire.transform.position + new Vector3(24f, 0.2f, 0f);
                yield return new WaitForSeconds(0.4f);
                _log.AppendLine("twenty-four metres away:  tooDark = " + Photography.TooDark(player)
                    + (Photography.TooDark(player) ? "  OK" : "  FIRE REACHES TOO FAR"));
            }

            cycle.paused = false;
            Debug.Log("NightProbe:\n" + _log);
        }
    }
}
#endif
