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
    /// Editor-only. Drives each interface the game can put on screen and photographs it,
    /// so the minigames can be checked without somebody sitting at the keyboard pressing
    /// arrow keys in the right order.
    ///
    /// Nothing here presses anything: each sequence is started and then allowed to time
    /// out on its own, which is a legitimate outcome of every one of them.
    /// </summary>
    public class SystemsProbe : MonoBehaviour
    {
        readonly StringBuilder _log = new StringBuilder();

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2f);

            yield return Woods();
            yield return Shot("20_woods");

            yield return Pause();
            yield return Shot("21_pause");
            PauseMenu.Instance?.Close();
            yield return new WaitForSeconds(0.5f);

            yield return Options();
            yield return Shot("22_options");
            PauseMenu.Instance?.Close();
            yield return new WaitForSeconds(0.5f);

            yield return Journal();
            yield return Shot("23_journal");
            GameHud.Instance?.ToggleJournal();
            yield return new WaitForSeconds(0.6f);

            yield return Viewfinder();
            yield return Shot("24_viewfinder");
            yield return new WaitForSeconds(7f);
            yield return Shot("25_review");

            System.IO.File.WriteAllText("Logs/systems_probe.txt", _log.ToString());
            Debug.Log("SystemsProbe finished:\n" + _log);
        }

        /// <summary>Walk somewhere the composer says is properly wooded and look at it.</summary>
        IEnumerator Woods()
        {
            Note("woods");
            var player = PlayerMover.Instance;
            if (player == null) yield break;

            Vector2 best = Vector2.zero;
            float bestDensity = 0f;
            for (int i = 0; i < 400; i++)
            {
                float a = i * 0.618f * Mathf.PI * 2f;
                float r = 40f + (i % 40) * 3f;
                var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!WorldComposer.IsWalkable(at.x, at.y)) continue;
                float d = WorldComposer.Density(at.x, at.y);
                if (d <= bestDensity) continue;
                bestDensity = d;
                best = at;
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position =
                new Vector3(best.x, WorldComposer.Height(best.x, best.y) + 0.4f, best.y);
            if (cc != null) cc.enabled = true;

            _log.AppendLine("  stood at " + best + " where density is " + bestDensity.ToString("0.00"));
            // Give the streamer time to fill in around the new position.
            yield return new WaitForSeconds(3.5f);
        }

        IEnumerator Pause()
        {
            Note("pause");
            PauseMenu.Instance?.Open();
            yield return new WaitForSecondsRealtime(1f);
        }

        IEnumerator Options()
        {
            Note("options");
            var menu = PauseMenu.Instance;
            if (menu == null) yield break;
            menu.Open();
            yield return new WaitForSecondsRealtime(0.4f);

            var method = typeof(PauseMenu).GetMethod("ShowOptions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null) { _log.AppendLine("  could not reach ShowOptions"); yield break; }
            method.Invoke(menu, new object[] { true });
            yield return new WaitForSecondsRealtime(0.8f);
        }

        IEnumerator Journal()
        {
            Note("journal");
            var state = GameState.Instance;
            var library = Follow.Data.SpeciesLibrary.Active;
            if (state != null && library != null)
                foreach (var species in library.species)
                {
                    if (species == null) continue;
                    state.album.Record(species.id, 0.72f, Swatch(species.tint), 1);
                    break;
                }

            GameHud.Instance?.ToggleJournal();
            yield return new WaitForSeconds(1.4f);
        }

        /// <summary>A stand-in photograph, so the album page is not testing an empty frame.</summary>
        static Texture2D Swatch(Color tint)
        {
            var tex = new Texture2D(64, 42);
            var px = new Color[64 * 42];
            for (int i = 0; i < px.Length; i++) px[i] = tint * Random.Range(0.7f, 1.1f);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        IEnumerator Viewfinder()
        {
            Note("viewfinder");

            var player = PlayerMover.Instance;
            var library = Follow.Data.SpeciesLibrary.Active;
            if (player == null || library == null) yield break;

            Follow.Data.SpeciesData fauna = null;
            foreach (var s in library.species)
                if (s != null && s.kind == Follow.Data.SpeciesKind.Fauna && s.modelPrefab != null)
                { fauna = s; break; }
            if (fauna == null) yield break;

            Vector3 at = player.transform.position + player.transform.forward * 6f;
            at.y = WorldComposer.Height(at.x, at.z);

            var go = Instantiate(fauna.modelPrefab, at, Quaternion.identity);
            go.transform.localScale = Vector3.one * fauna.worldScale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && fauna.animator != null)
                animator.runtimeAnimatorController = fauna.animator;

            var subject = go.AddComponent<PhotoSubject>();
            subject.species = fauna;
            _log.AppendLine("  planted a " + fauna.commonName + " at " + at);

            yield return new WaitForSeconds(0.6f);

            var photography = Photography.Instance;
            if (photography == null) { _log.AppendLine("  NO PHOTOGRAPHY SYSTEM"); yield break; }

            var method = typeof(Photography).GetMethod("Shoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null) { _log.AppendLine("  could not reach Shoot"); yield break; }
            photography.StartCoroutine((IEnumerator)method.Invoke(photography, new object[] { subject }));

            yield return new WaitForSeconds(1.8f);
            _log.AppendLine("  photography state = " + photography.State);
        }

        IEnumerator Shot(string name)
        {
            string path = "Logs/probe_" + name + ".png";
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(1f);
            _log.AppendLine("  captured " + path);
        }

        void Note(string moment)
        {
            var state = GameState.Instance;
            var cycle = DayCycle.Instance;
            _log.AppendLine("--- " + moment + " ---");
            if (cycle != null) _log.AppendLine("  " + cycle.ClockText + " day " + (state != null ? state.day : 0));
            if (state != null)
                _log.AppendLine("  sticks=" + state.sticks + " food=" + state.food
                    + " album=" + state.album.Count + " bond=" + state.bond.ToString("0.00"));
            _log.AppendLine("  modal open = " + UIModal.Any);

            var fade = Object.FindFirstObjectByType<CanopyFade>();
            if (fade != null) _log.AppendLine("  canopy being thinned = " + fade.Fading);
        }
    }
}
#endif
