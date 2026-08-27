using System.Collections;
using System.IO;
using UnityEngine;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Captures the game view a few seconds into play so UI work can be checked without
    /// a human at the keyboard. Editor-only convenience; never ships in a build.
    /// </summary>
    public class ScreenshotProbe : MonoBehaviour
    {
        public string outputPath = "Logs/shot.png";
        public float delay = 2.5f;
        public int superSize = 1;
        public bool exitPlayModeAfter = true;

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(delay);

            string full = Path.Combine(Directory.GetCurrentDirectory(), outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            ScreenCapture.CaptureScreenshot(full, superSize);

            // CaptureScreenshot is asynchronous; give it frames to land on disk.
            for (int i = 0; i < 12 && !File.Exists(full); i++) yield return new WaitForSecondsRealtime(0.25f);
            yield return new WaitForSecondsRealtime(0.5f);

            Debug.Log("ScreenshotProbe wrote " + full);

#if UNITY_EDITOR
            if (exitPlayModeAfter) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
