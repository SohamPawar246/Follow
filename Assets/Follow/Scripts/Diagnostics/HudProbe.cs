using System.Collections;
using System.IO;
using UnityEngine;
using Follow.Core;
using Follow.Data;
using Follow.UI;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Seeds a plausible mid-run state, photographs the HUD, opens the journal and
    /// photographs that too. Editor-only; lets UI work be checked without a human.
    /// </summary>
    public class HudProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var state = GameState.Ensure();
            state.day = 3;
            state.sticks = 7;
            state.food = 2;
            state.energy = 0.62f;
            state.dogHunger = 0.45f;

            // Pretend two of today's targets are already in the album.
            var library = SpeciesLibrary.Active;
            if (library != null)
            {
                var list = library.BuildSurveyList(state.day, 3, 2);
                for (int i = 0; i < Mathf.Min(2, list.Count); i++)
                    state.album.Record(list[i].id, 0.72f, null, state.day);
            }

            // Decisive test: log every renderer on the player, then strip the silhouette
            // overlays and see whether the character comes back.
            var player = GameObject.Find("Player");
            if (player != null)
            {
                Note("player pos=" + player.transform.position + " scale=" + player.transform.lossyScale);
                foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Note("  SMR " + smr.gameObject.name
                        + " enabled=" + smr.enabled
                        + " mat=" + (smr.sharedMaterial != null ? smr.sharedMaterial.shader.name : "NULL")
                        + " bounds=" + smr.bounds.size
                        + " centre=" + smr.bounds.center);
                foreach (var mr in player.GetComponentsInChildren<MeshRenderer>(true))
                    Note("  MR  " + mr.gameObject.name + " enabled=" + mr.enabled
                        + " mat=" + (mr.sharedMaterial != null ? mr.sharedMaterial.shader.name : "NULL"));

                int overlays = 0;
                foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.gameObject.name.EndsWith("_XRay")) overlays++;
                Note("silhouette overlays LEFT ENABLED: " + overlays);
            }
            else Note("NO PLAYER FOUND");

            var cam = Camera.main;
            Note("camera pos=" + (cam != null ? cam.transform.position.ToString() : "NULL"));

            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("Logs/hud.png");

            var hud = GameHud.Instance;
            if (hud != null) hud.ToggleJournal();

            // Let the crossing-off animation finish before capturing.
            yield return new WaitForSecondsRealtime(3.2f);
            yield return Shot("Logs/journal.png");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        static void Note(string line)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), "Logs/probe.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.AppendAllText(full, line + System.Environment.NewLine);
        }

        IEnumerator Shot(string relative)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            ScreenCapture.CaptureScreenshot(full);
            for (int i = 0; i < 14 && !File.Exists(full); i++) yield return new WaitForSecondsRealtime(0.25f);
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
