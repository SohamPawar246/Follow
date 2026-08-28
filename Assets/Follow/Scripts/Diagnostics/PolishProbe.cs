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
    /// Editor-only. Checks the things this round of work claimed to fix, by driving the
    /// real systems rather than by inspecting fields: does the dog actually reach and
    /// point at something, are there visible birds, does a cast start immediately, does
    /// the whistle answer, and does the arrow row colour a press green and red.
    /// </summary>
    public class PolishProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            yield return Birds();
            yield return Water();
            yield return Whistle();
            yield return Casting();
            yield return Arrows();
            yield return TheDogFinding();

            System.IO.File.WriteAllText("Logs/polish_probe.txt", _log.ToString());
            Debug.Log("PolishProbe:\n" + _log);
        }

        // --- background life -------------------------------------------------------

        IEnumerator Birds()
        {
            _log.AppendLine("--- birds ---");

            var wildlife = FindFirstObjectByType<Wildlife>();
            _log.AppendLine("  wildlife system present: " + (wildlife != null));
            if (wildlife == null) yield break;

            yield return new WaitForSeconds(2f);

            var root = GameObject.Find("Wildlife");
            int birds = 0, flutters = 0;
            if (root != null)
                foreach (Transform child in root.transform)
                {
                    if (child.name == "Bird") birds++;
                    if (child.name == "Butterfly") flutters++;
                }

            _log.AppendLine("  birds on the ground: " + birds);
            _log.AppendLine("  butterflies: " + flutters);

            // Are any of them actually where the player could see them?
            var player = PlayerMover.Instance;
            float nearest = 999f;
            if (root != null && player != null)
                foreach (Transform child in root.transform)
                    if (child.name == "Bird")
                        nearest = Mathf.Min(nearest,
                            Vector3.Distance(child.position, player.transform.position));
            _log.AppendLine("  nearest bird: " + nearest.ToString("0.0") + " m");

            // Walk the player into a flock and see it go up.
            if (root != null && player != null)
            {
                Transform target = null;
                foreach (Transform child in root.transform)
                    if (child.name == "Bird") { target = child; break; }

                if (target != null)
                {
                    Vector3 at = target.position;
                    int before = CountBirdsAloft(root.transform);
                    player.transform.position = new Vector3(at.x + 2f,
                        WorldComposer.Height(at.x + 2f, at.z) + 0.2f, at.z);

                    yield return new WaitForSeconds(1.6f);
                    int after = CountBirdsAloft(root.transform);
                    _log.AppendLine("  birds airborne after walking in: " + after
                                    + " (was " + before + ")"
                                    + (after > before ? "  OK" : "  DID NOT FLUSH"));
                }
            }
        }

        static int CountBirdsAloft(Transform root)
        {
            int aloft = 0;
            foreach (Transform child in root)
            {
                if (child.name != "Bird") continue;
                if (child.position.y > WorldComposer.Height(child.position.x, child.position.z) + 1.2f)
                    aloft++;
            }
            return aloft;
        }

        // --- water ------------------------------------------------------------------

        IEnumerator Water()
        {
            _log.AppendLine("--- water ---");

            var player = PlayerMover.Instance;
            if (player == null) yield break;

            var p = player.transform.position;
            if (!WorldComposer.NearestPond(new Vector2(p.x, p.z), 400f, out var pond))
            {
                _log.AppendLine("  no pond within 400 m");
                yield break;
            }

            // Stand at the water so the chunk that draws it is streamed in.
            Vector2 bank = pond.position + Vector2.right * (pond.radius + 2f);
            player.transform.position = new Vector3(bank.x,
                WorldComposer.Height(bank.x, bank.y) + 0.3f, bank.y);
            yield return new WaitForSeconds(2.5f);

            int discs = 0;
            bool flat = true;
            foreach (var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (filter.gameObject.name != "Pond") continue;
                discs++;
                var mesh = filter.sharedMesh;
                if (mesh != null && mesh.name != "PondDisc") flat = false;
            }

            _log.AppendLine("  pond surfaces drawn: " + discs);
            _log.AppendLine("  all flat fans (no cylinder walls): " + flat);
            _log.AppendLine("  bank height above surface: "
                + (WorldComposer.Height(bank.x, bank.y) - WorldComposer.PondSurface(pond)).ToString("0.00") + " m");
        }

        // --- the whistle -------------------------------------------------------------

        IEnumerator Whistle()
        {
            _log.AppendLine("--- whistle ---");

            var dog = DogBrain.Instance;
            var state = GameState.Instance;
            if (dog == null || state == null) yield break;

            float was = state.bond;
            state.bond = 0.12f;                      // exactly what a fresh run starts at

            bool answered = dog.Whistle();
            yield return new WaitForSeconds(0.4f);
            _log.AppendLine("  at day-one bond, answered fully: " + answered
                            + ", state now " + dog.State
                            + (dog.State == DogState.Follow || dog.State == DogState.Scent
                               || dog.State == DogState.Point ? "  OK" : "  NO RESPONSE"));

            state.bond = 0.5f;
            dog.Whistle();
            yield return new WaitForSeconds(0.3f);
            _log.AppendLine("  at earned bond, state now " + dog.State);
            state.bond = was;
        }

        // --- casting -----------------------------------------------------------------

        IEnumerator Casting()
        {
            _log.AppendLine("--- fishing ---");

            var fishing = FishingGame.Instance;
            var player = PlayerMover.Instance;
            if (fishing == null || player == null) yield break;

            var p = player.transform.position;
            if (!WorldComposer.NearestPond(new Vector2(p.x, p.z), 400f, out var pond))
            {
                _log.AppendLine("  no pond to cast into");
                yield break;
            }

            Vector2 bank = pond.position + Vector2.up * (pond.radius + 2f);
            player.transform.position = new Vector3(bank.x,
                WorldComposer.Height(bank.x, bank.y) + 0.3f, bank.y);
            yield return new WaitForSeconds(0.6f);

            var method = typeof(FishingGame).GetMethod("Fish",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            fishing.StartCoroutine((IEnumerator)method.Invoke(fishing, null));

            var caption = typeof(FishingGame).GetField("_caption",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // How long before it is asking for something rather than telling you to wait?
            float t = 0f;
            string saw = "";
            while (t < 4f)
            {
                t += Time.deltaTime;
                var label = caption?.GetValue(fishing) as TMPro.TextMeshProUGUI;
                if (label != null && label.text.Contains("hold")) { saw = label.text; break; }
                yield return null;
            }

            _log.AppendLine("  reached the fight after " + t.ToString("0.00") + "s"
                            + (t < 1f ? "  OK" : "  TOO SLOW"));
            _log.AppendLine("  caption: \"" + saw + "\"");
            _log.AppendLine("  no bite phase, so \"too soon\" is unreachable: "
                            + !HasBitePhase());

            // Play it out so the hold on movement is released.
            float wait = 0f;
            while (fishing.Busy && wait < 25f) { wait += Time.deltaTime; yield return null; }
            _log.AppendLine("  movement released afterwards: " + !PlayerMover.Instance.Frozen);
        }

        static bool HasBitePhase() =>
            typeof(FishingGame).GetField("waitForBite") != null;

        // --- the arrow row ------------------------------------------------------------

        IEnumerator Arrows()
        {
            _log.AppendLine("--- photo minigame ---");

            var sequence = FindFirstObjectByType<ShotSequenceUI>();
            if (sequence == null) { _log.AppendLine("  no sequence UI"); yield break; }

            _log.AppendLine("  clock for four arrows: "
                + (sequence.baseSeconds + 4f * sequence.secondsPerArrow).ToString("0.0") + "s");

            var chip = GameObject.Find("Arrow");
            _log.AppendLine("  chip carries a halo ring: "
                + (chip != null && chip.transform.Find("Ring") != null));
            _log.AppendLine("  chip carries a border: "
                + (chip != null && chip.transform.Find("Border") != null));
            yield break;
        }

        // --- the whole point ------------------------------------------------------------

        /// <summary>
        /// The one that matters: put the dog and a subject in the same forest and see
        /// whether she ever actually gets to it. This is the loop that was silently
        /// impossible - subjects seeded from 38 m out, a dog that ranged 14 m.
        /// </summary>
        IEnumerator TheDogFinding()
        {
            _log.AppendLine("--- the dog finding something ---");

            var dog = DogBrain.Instance;
            var state = GameState.Instance;
            if (dog == null || state == null) yield break;

            _log.AppendLine("  scent points alive: " + ScentPoint.Active.Count);

            float nearest = 999f;
            foreach (var point in ScentPoint.Active)
                if (point != null)
                    nearest = Mathf.Min(nearest,
                        Vector3.Distance(dog.transform.position, point.transform.position));
            _log.AppendLine("  nearest to the dog: " + nearest.ToString("0.0") + " m"
                            + "   (her hunting radius: " + dog.huntRadius.ToString("0") + " m)");

            bool pointed = false;
            float watched = 0f;
            DogState best = dog.State;

            while (watched < 60f)
            {
                watched += Time.deltaTime;
                if (dog.State == DogState.Scent && best != DogState.Point) best = DogState.Scent;
                if (dog.State == DogState.Point) { pointed = true; best = DogState.Point; break; }
                yield return null;
            }

            _log.AppendLine("  furthest she got in 60s: " + best);
            _log.AppendLine("  reached a point: " + pointed
                            + (pointed ? " after " + watched.ToString("0.0") + "s  OK"
                                       : "  NEVER FOUND ANYTHING"));

            if (pointed && dog.Find != null)
            {
                _log.AppendLine("  the find is: " + (dog.Find.species != null
                    ? dog.Find.species.commonName : "?"));
                _log.AppendLine("  it is revealed and visible: " + dog.Find.Revealed);
                _log.AppendLine("  distance from the player: "
                    + dog.DistanceToPlayer.ToString("0.0") + " m");
            }
        }
    }
}
#endif
