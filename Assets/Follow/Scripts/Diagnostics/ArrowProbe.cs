#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Follow.Game;
using Follow.UI;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Runs one real arrow sequence, answering the first key correctly and
    /// the second deliberately wrong, then reads the colour actually on each chip's border
    /// image. Presses go in as genuine input events, so this exercises the same path a
    /// player's keyboard does rather than calling the handler directly.
    /// </summary>
    public class ArrowProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(3f);

            var sequence = FindFirstObjectByType<ShotSequenceUI>();
            var player = PlayerMover.Instance;
            if (sequence == null || player == null)
            {
                Debug.Log("ArrowProbe: nothing to test");
                yield break;
            }

            // A subject to point the lens at, right in front of the player.
            var subject = Plant();
            if (subject == null) { Debug.Log("ArrowProbe: no subject"); yield break; }

            int misses = -1;
            sequence.StartCoroutine(sequence.Run(subject, 4, 0f, m => misses = m));

            // Wait for the row to exist and the clock to start.
            yield return new WaitForSeconds(0.75f);

            var expected = new List<Key>(sequence.Expected);
            _log.AppendLine("row drawn: " + string.Join(" ", expected));

            // First one right.
            yield return Press(expected[0]);
            yield return new WaitForSeconds(0.25f);

            // Second one deliberately wrong.
            yield return Press(Opposite(expected[1]));
            yield return new WaitForSeconds(0.3f);

            ScreenCapture.CaptureScreenshot("Logs/arrow_feedback.png", 1);
            _log.AppendLine(Read(sequence));

            // Let the rest of the clock run out.
            yield return new WaitForSeconds(6f);
            _log.AppendLine("misses reported: " + misses + " (expected 3)");

            Debug.Log("ArrowProbe:\n" + _log);
        }

        /// <summary>Reads the colour genuinely on each chip right now.</summary>
        string Read(ShotSequenceUI sequence)
        {
            var field = typeof(ShotSequenceUI).GetField("_chips",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var chips = field.GetValue(sequence) as IEnumerable;

            var text = new StringBuilder();
            int i = 0;
            foreach (var chip in chips)
            {
                var type = chip.GetType();
                var root = type.GetField("root").GetValue(chip) as RectTransform;
                var border = type.GetField("border").GetValue(chip) as Image;
                var ring = type.GetField("ring").GetValue(chip) as Image;
                var verdict = type.GetField("verdict").GetValue(chip);

                Color c = border.color;
                string named = c.g > 0.6f && c.r < 0.5f ? "GREEN"
                             : c.r > 0.7f && c.g < 0.4f ? "RED"
                             : "neutral";

                text.AppendLine("  arrow " + i + ": " + verdict
                    + "  border " + named
                    + " (" + c.r.ToString("0.00") + ", " + c.g.ToString("0.00")
                    + ", " + c.b.ToString("0.00") + ")"
                    + "  halo alpha " + ring.color.a.ToString("0.00")
                    + "  scale " + root.localScale.x.ToString("0.00"));
                i++;
            }
            return text.ToString();
        }

        static Key Opposite(Key key) => key switch
        {
            Key.UpArrow => Key.DownArrow,
            Key.DownArrow => Key.UpArrow,
            Key.LeftArrow => Key.RightArrow,
            _ => Key.LeftArrow
        };

        static IEnumerator Press(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null) yield break;

            // Queue it and let the next natural input update deliver it. Pumping
            // InputSystem.Update() by hand here burns the press: the flag is raised and
            // cleared again before the sequence coroutine, which runs after Update, ever
            // gets a frame in which to read it.
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        PhotoSubject Plant()
        {
            var player = PlayerMover.Instance;
            var library = Follow.Data.SpeciesLibrary.Active;
            if (player == null || library == null) return null;

            Follow.Data.SpeciesData plant = null;
            foreach (var s in library.species)
                if (s != null && s.kind == Follow.Data.SpeciesKind.Flora && s.modelPrefab != null)
                { plant = s; break; }
            if (plant == null) return null;

            Vector3 at = player.transform.position + player.transform.forward * 5f;
            at.y = WorldComposer.Height(at.x, at.z);

            var specimen = FloraSpecimen.Spawn(plant, at, null);
            return specimen != null ? specimen.GetComponent<PhotoSubject>() : null;
        }
    }
}
#endif
