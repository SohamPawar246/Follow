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
    /// Editor-only. A short pass over everything that has ever broken, run after a round
    /// of changes to confirm none of them broke it again. It asserts nothing; it prints,
    /// and a human reads the column of OKs.
    /// </summary>
    public class SmokeProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(3f);

            var state = GameState.Instance;
            var player = PlayerMover.Instance;
            var dog = DogBrain.Instance;
            var cycle = DayCycle.Instance;

            Line("game state", state != null);
            Line("player", player != null);
            Line("dog", dog != null);
            Line("day cycle", cycle != null);
            Line("watchdog installed", Watchdog.Instance != null);
            Line("hud", GameHud.Instance != null);
            Line("photography", Photography.Instance != null);
            Line("fishing", FishingGame.Instance != null);
            Line("sleep", SleepSystem.Instance != null);
            Line("campfire", Campfire.Instance != null);

            if (player == null || dog == null || cycle == null) { Dump(); yield break; }

            // The world is under the player rather than the player under the world.
            float ground = WorldComposer.Height(player.transform.position.x,
                                                player.transform.position.z);
            Line("player standing on the height field",
                Mathf.Abs(player.transform.position.y - ground) < 3f,
                "y " + player.transform.position.y.ToString("0.0")
                + " vs ground " + ground.ToString("0.0"));

            // Things that should be alive in the wood.
            Line("scent points alive", ScentPoint.Active.Count > 0,
                ScentPoint.Active.Count.ToString());
            Line("particles have materials", ParticlesDressed());
            Line("post-processing on", PostProcessing());

            var wildlife = FindFirstObjectByType<Wildlife>();
            Line("wildlife system", wildlife != null);

            // Movement.
            Line("movement free at the start", !player.Frozen);
            Vector3 from = player.transform.position;
            float t = 0f;
            var cc = player.GetComponent<CharacterController>();
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                if (cc != null && cc.enabled && !player.Frozen)
                    cc.Move(new Vector3(3f, -6f, 0f) * Time.deltaTime);
                yield return null;
            }
            Line("player can walk", Vector3.Distance(from, player.transform.position) > 1f,
                Vector3.Distance(from, player.transform.position).ToString("0.0") + " m");

            // The whistle.
            bool answered = dog.Whistle();
            yield return new WaitForSeconds(0.3f);
            Line("whistle answers", answered && dog.Recalled);

            // The clock runs.
            float before = cycle.Time01;
            yield return new WaitForSeconds(1.5f);
            Line("clock is running", !Mathf.Approximately(before, cycle.Time01));

            // And the watchdog undoes a wedged modal.
            UIModal.Push();
            Line("modal counter raised by hand", UIModal.Any);
            float waited = 0f;
            while (UIModal.Any && waited < 12f) { waited += Time.deltaTime; yield return null; }
            Line("watchdog cleared it", !UIModal.Any,
                "after " + waited.ToString("0.0") + "s");

            Dump();
        }

        static bool ParticlesDressed()
        {
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial == null) return false;
            }
            return true;
        }

        static bool PostProcessing()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            return data != null && data.renderPostProcessing;
        }

        void Line(string what, bool ok, string detail = null)
        {
            _log.AppendLine((ok ? "  OK    " : "  FAIL  ") + what
                + (detail != null ? "   (" + detail + ")" : ""));
        }

        void Dump() => Debug.Log("SmokeProbe:\n" + _log);
    }
}
#endif
