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
    /// Editor-only. Reproduces the freeze: photograph a flowering specimen, then sit on the
    /// review card for long enough that the field which stocks the forest would have
    /// retired it underneath, and see whether the player can still walk afterwards.
    ///
    /// Runs it twice - once on flora, once on an animal the scent field is also retiring -
    /// because both routes destroyed the subject mid-shot.
    /// </summary>
    public class FreezeProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            yield return Attempt("flora", true);
            yield return new WaitForSeconds(1.5f);
            yield return Attempt("fauna", false);

            System.IO.File.WriteAllText("Logs/freeze_probe.txt", _log.ToString());
            Debug.Log("FreezeProbe:\n" + _log);
        }

        IEnumerator Attempt(string what, bool flora)
        {
            _log.AppendLine("--- photographing " + what + ", then stalling ---");

            var subject = flora ? PlantFlora() : PlantFauna();
            if (subject == null) { _log.AppendLine("  could not place a subject"); yield break; }

            var subjectObject = subject.gameObject;
            var photography = Photography.Instance;
            var method = typeof(Photography).GetMethod("Shoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            photography.StartCoroutine((IEnumerator)method.Invoke(photography, new object[] { subject }));

            // Photograph the row itself partway through, to check it is not sitting on
            // top of the counters in the top left corner.
            yield return new WaitForSeconds(1.4f);
            ScreenCapture.CaptureScreenshot("Logs/probe_70_" + what + ".png", 1);
            yield return new WaitForSeconds(1f);

            // Let the rest of the sequence time out rather than answering it.
            float wait = 0f;
            while (photography.State != Photography.Mode.Reviewing && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            _log.AppendLine("  reached the review card after " + wait.ToString("0.0") + "s");

            // Stall. The fields review every one and a half to two seconds, so eight is
            // several chances for them to destroy the thing this shot came from.
            yield return new WaitForSeconds(8f);
            _log.AppendLine("  subject object still alive after stalling: " + (subjectObject != null));

            Answer(true);
            yield return new WaitForSeconds(1.5f);

            _log.AppendLine("  photography state = " + photography.State);
            _log.AppendLine("  album now holds " + GameState.Instance.album.Count);

            var player = PlayerMover.Instance;
            _log.AppendLine("  movement frozen = " + player.Frozen);

            Vector3 from = player.transform.position;
            float t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.enabled && !player.Frozen)
                    cc.Move(new Vector3(3f, -6f, 0f) * Time.deltaTime);
                yield return null;
            }
            float moved = Vector3.Distance(from, player.transform.position);
            _log.AppendLine("  walked " + moved.ToString("0.0") + " m afterwards"
                            + (moved > 1f ? "  OK" : "  STUCK"));
        }

        static void Answer(bool keep)
        {
            var review = Object.FindFirstObjectByType<PhotoReviewUI>();
            if (review == null) return;
            var field = typeof(PhotoReviewUI).GetField("_answer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(review, keep);
        }

        PhotoSubject PlantFauna()
        {
            var player = PlayerMover.Instance;
            var library = Follow.Data.SpeciesLibrary.Active;
            if (player == null || library == null) return null;

            Follow.Data.SpeciesData fauna = null;
            foreach (var s in library.species)
                if (s != null && s.kind == Follow.Data.SpeciesKind.Fauna && s.modelPrefab != null)
                { fauna = s; break; }
            if (fauna == null) return null;

            Vector3 at = player.transform.position + player.transform.forward * 6.5f;
            at.y = WorldComposer.Height(at.x, at.z);

            var go = Instantiate(fauna.modelPrefab, at, Quaternion.identity);
            go.transform.localScale = Vector3.one * fauna.worldScale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            var subject = go.AddComponent<PhotoSubject>();
            subject.species = fauna;
            return subject;
        }

        /// <summary>A real specimen, made by the same field that will try to retire it.</summary>
        PhotoSubject PlantFlora()
        {
            var player = PlayerMover.Instance;
            var library = Follow.Data.SpeciesLibrary.Active;
            if (player == null || library == null) return null;

            Follow.Data.SpeciesData plant = null;
            foreach (var s in library.species)
                if (s != null && s.kind == Follow.Data.SpeciesKind.Flora && s.modelPrefab != null)
                { plant = s; break; }
            if (plant == null) return null;

            // Behind and to the left of the player, so it lands high on screen - which is
            // exactly where the arrow row used to collide with the counters.
            Vector3 at = player.transform.position + new Vector3(-7f, 0f, 7f);
            at.y = WorldComposer.Height(at.x, at.z);

            var field = Object.FindFirstObjectByType<FloraField>();
            var root = field != null ? field.transform : null;
            var specimen = FloraSpecimen.Spawn(plant, at, root);
            return specimen != null ? specimen.GetComponent<PhotoSubject>() : null;
        }
    }
}
#endif
