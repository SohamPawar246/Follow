#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Follow.Core;
using Follow.Data;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Plays the photograph minigame properly - it reads the row that was
    /// drawn and presses the actual arrow keys through the input system, so this tests the
    /// same path a player's fingers take rather than calling the scoring directly.
    ///
    /// It runs the sequence twice: once perfectly, once fumbling half of it, so the two
    /// resulting prints can be compared side by side.
    /// </summary>
    public class ShotProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            yield return Shoot(true, "40_perfect");
            yield return new WaitForSeconds(1.5f);
            Dismiss();

            yield return new WaitForSeconds(1f);
            yield return Shoot(false, "41_fumbled");
            yield return new WaitForSeconds(1.5f);
            Dismiss();

            System.IO.File.WriteAllText("Logs/shot_probe.txt", _log.ToString());
            Debug.Log("ShotProbe:\n" + _log);
        }

        IEnumerator Shoot(bool playWell, string shotName)
        {
            var subject = Plant();
            if (subject == null) { _log.AppendLine("could not plant a subject"); yield break; }

            var photography = Photography.Instance;
            var method = typeof(Photography).GetMethod("Shoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            photography.StartCoroutine((IEnumerator)method.Invoke(photography, new object[] { subject }));

            var sequence = FindFirstObjectByType<ShotSequenceUI>();
            _log.AppendLine("--- " + (playWell ? "played well" : "fumbled half") + " ---");

            float started = Time.unscaledTime;
            int answered = 0;
            int lastStep = -1;

            // Wait for the row, then answer each arrow as it lights up.
            while (Time.unscaledTime - started < 30f)
            {
                if (sequence.Step >= 0 && sequence.Step != lastStep)
                {
                    lastStep = sequence.Step;
                    // A beat, so the answer lands inside the window rather than on its edge.
                    yield return new WaitForSecondsRealtime(0.35f);
                    if (sequence.Step != lastStep) continue;

                    var want = sequence.Expected[lastStep];
                    bool deliberatelyWrong = !playWell && (lastStep % 2 == 1);
                    var send = deliberatelyWrong ? Wrong(want) : want;

                    yield return Press(send);
                    answered++;
                    _log.AppendLine("  step " + lastStep + ": wanted " + want + ", sent " + send);
                }

                if (photography.State == Photography.Mode.Reviewing) break;
                yield return null;
            }

            _log.AppendLine("  answered " + answered + " of " + sequence.Expected.Count
                            + " in " + (Time.unscaledTime - started).ToString("0.0") + "s");
            _log.AppendLine("  state = " + photography.State);

            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot("Logs/probe_" + shotName + ".png", 1);
            yield return new WaitForSecondsRealtime(1.2f);
        }

        static Key Wrong(Key right) => right == Key.UpArrow ? Key.DownArrow : Key.UpArrow;

        /// <summary>Presses and releases a key through the input system, as a keyboard would.</summary>
        static IEnumerator Press(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null) yield break;

            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            InputSystem.Update();
            yield return null;

            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        PhotoSubject Plant()
        {
            var player = PlayerMover.Instance;
            var library = SpeciesLibrary.Active;
            if (player == null || library == null) return null;

            SpeciesData fauna = null;
            foreach (var s in library.species)
                if (s != null && s.kind == SpeciesKind.Fauna && s.modelPrefab != null) { fauna = s; break; }
            if (fauna == null) return null;

            Vector3 at = player.transform.position + player.transform.forward * 6.5f;
            at.y = WorldComposer.Height(at.x, at.z);

            var go = Instantiate(fauna.modelPrefab, at, Quaternion.identity);
            go.transform.localScale = Vector3.one * fauna.worldScale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && fauna.animator != null)
                animator.runtimeAnimatorController = fauna.animator;

            var subject = go.AddComponent<PhotoSubject>();
            subject.species = fauna;
            return subject;
        }

        /// <summary>Answers the keep-or-discard card so the next shot can start.</summary>
        static void Dismiss()
        {
            var review = FindFirstObjectByType<PhotoReviewUI>();
            if (review == null) return;
            var field = typeof(PhotoReviewUI).GetField("_answer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(review, true);
        }
    }
}
#endif
