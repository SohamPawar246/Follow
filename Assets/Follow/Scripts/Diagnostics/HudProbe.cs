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
