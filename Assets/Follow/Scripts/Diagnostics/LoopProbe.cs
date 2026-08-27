#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Follow.Core;
using Follow.Dog;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Plays the three verbs that were reported broken - fishing, feeding the
    /// dog, and the photograph sequence - by pressing the same keys a player would, through
    /// the input system. Anything it cannot do, a player cannot do either.
    /// </summary>
    public class LoopProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            yield return Fishing();
            yield return Feeding();
            yield return Shooting();
            Counts();

            System.IO.File.WriteAllText("Logs/loop_probe.txt", _log.ToString());
            Debug.Log("LoopProbe:\n" + _log);
        }

        // --- fishing ------------------------------------------------------------

        IEnumerator Fishing()
        {
            _log.AppendLine("--- fishing ---");

            var player = PlayerMover.Instance;
            var flat = new Vector2(player.transform.position.x, player.transform.position.z);
            if (!WorldComposer.NearestPond(flat, 260f, out var pond))
            { _log.AppendLine("  no pond found"); yield break; }

            var edge = pond.position + (flat - pond.position).normalized * (pond.radius + 2f);
            Teleport(player, edge);
            yield return new WaitForSeconds(2f);

            _log.AppendLine("  prompt: " + GameHud.Instance.PromptText);

            int foodBefore = GameState.Instance.food;
            yield return Press(Key.E);                       // cast

            // Wait for the bite, then strike. The caption is the only public signal.
            float waited = 0f;
            bool struck = false;
            while (waited < 12f)
            {
                waited += Time.deltaTime;
                string caption = FishingCaption();
                if (!struck && caption.StartsWith("NOW"))
                {
                    struck = true;
                    _log.AppendLine("  bite came at " + waited.ToString("0.0") + "s, striking");
                    yield return Press(Key.E);
                }
                if (caption.Contains("too soon")) { _log.AppendLine("  FAILED: too soon"); break; }
                if (caption.Contains("hold to raise")) break;
                yield return null;
            }

            if (!struck) { _log.AppendLine("  no bite inside 12s"); yield break; }

            // Play the fight badly on purpose but persistently, holding roughly half the time.
            float fight = 0f;
            while (fight < 20f && FishingGame.Instance.Busy)
            {
                fight += Time.deltaTime;
                bool hold = Mathf.Repeat(fight, 1.1f) < 0.55f;
                yield return Hold(Key.E, hold);
            }

            _log.AppendLine("  after the fight: caption '" + FishingCaption()
                            + "', food " + foodBefore + " -> " + GameState.Instance.food);
        }

        static string FishingCaption()
        {
            var field = typeof(FishingGame).GetField("_caption",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var label = field?.GetValue(FishingGame.Instance) as TMPro.TextMeshProUGUI;
            return label != null ? label.text : "";
        }

        // --- feeding -------------------------------------------------------------

        IEnumerator Feeding()
        {
            _log.AppendLine("--- feeding the dog ---");

            var state = GameState.Instance;
            state.AddFood(3);
            state.dogHunger = 0.7f;

            var dog = DogBrain.Instance;
            var player = PlayerMover.Instance;
            if (dog == null) { _log.AppendLine("  no dog"); yield break; }

            Teleport(player, new Vector2(dog.transform.position.x + 1.6f, dog.transform.position.z));
            yield return new WaitForSeconds(1.5f);

            _log.AppendLine("  prompt: " + GameHud.Instance.PromptText);

            float foodBefore = state.food;
            float hungerBefore = state.dogHunger;
            float bondBefore = state.bond;

            yield return Press(Key.G);
            yield return new WaitForSeconds(0.6f);

            _log.AppendLine("  food " + foodBefore + " -> " + state.food
                + ", hunger " + hungerBefore.ToString("0.00") + " -> " + state.dogHunger.ToString("0.00")
                + ", bond " + bondBefore.ToString("0.00") + " -> " + state.bond.ToString("0.00"));
        }

        // --- the shot ------------------------------------------------------------

        IEnumerator Shooting()
        {
            _log.AppendLine("--- the photograph ---");

            var subject = Plant();
            if (subject == null) { _log.AppendLine("  could not plant a subject"); yield break; }

            var photography = Photography.Instance;
            var method = typeof(Photography).GetMethod("Shoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            photography.StartCoroutine((IEnumerator)method.Invoke(photography, new object[] { subject }));

            var sequence = FindFirstObjectByType<ShotSequenceUI>();
            float started = Time.unscaledTime;

            // Wait, deliberately, for over a second before touching anything - the whole
            // complaint was that the row vanished before it could be read.
            yield return new WaitForSecondsRealtime(1.6f);
            _log.AppendLine("  after 1.6s the row is still up: " + (sequence.Step >= 0)
                            + " (step " + sequence.Step + " of " + sequence.Expected.Count + ")");

            int sent = 0;
            while (sequence.Step >= 0 && Time.unscaledTime - started < 25f)
            {
                int at = sequence.Step;
                if (at >= sequence.Expected.Count) break;
                yield return new WaitForSecondsRealtime(0.4f);
                if (sequence.Step != at) continue;

                yield return Press(ToKey(sequence.Expected[at]));
                sent++;
            }

            _log.AppendLine("  answered " + sent + " of " + sequence.Expected.Count
                            + " over " + (Time.unscaledTime - started).ToString("0.0") + "s");

            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot("Logs/probe_50_shot.png", 1);
            yield return new WaitForSecondsRealtime(1.5f);
            _log.AppendLine("  state = " + photography.State);
        }

        static Key ToKey(Key k) => k;

        void Counts()
        {
            int sticks = 0, forage = 0;
            foreach (var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (pickup.kind == PickupKind.Stick) sticks++; else forage++;

            _log.AppendLine("--- what is lying about ---");
            _log.AppendLine("  sticks " + sticks + ", forage " + forage);
        }

        // --- plumbing ------------------------------------------------------------

        static void Teleport(PlayerMover player, Vector2 to)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(to.x, WorldComposer.Height(to.x, to.y) + 0.4f, to.y);
            if (cc != null) cc.enabled = true;
        }

        /// <summary>
        /// Queues a press and lets Unity process it on its own schedule.
        ///
        /// Driving InputSystem.Update() by hand from inside a coroutine consumed the press
        /// before any MonoBehaviour.Update could see it, so anything listening in Update -
        /// which is most of the game - never noticed the key at all.
        /// </summary>
        static IEnumerator Press(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null) yield break;

            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;                      // processed at the top of the next frame
            yield return null;                      // and seen by everything that frame
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        static IEnumerator Hold(Key key, bool down)
        {
            var kb = Keyboard.current;
            if (kb == null) yield break;
            InputSystem.QueueStateEvent(kb, down ? new KeyboardState(key) : new KeyboardState());
            yield return null;
        }

        PhotoSubject Plant()
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

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && fauna.animator != null)
                animator.runtimeAnimatorController = fauna.animator;

            var subject = go.AddComponent<PhotoSubject>();
            subject.species = fauna;
            return subject;
        }
    }
}
#endif
