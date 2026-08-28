using System.Collections;
using UnityEngine;

namespace Follow.Core
{
    /// <summary>
    /// First scene in the build. Plays the studio logo, creates the persistent systems and
    /// hands over to the menu, so nothing else has to check whether they exist.
    ///
    /// The logo lives here rather than in a scene of its own: this scene is already first,
    /// already empty and already black, and a whole extra scene file plus a build-settings
    /// entry would be three more things to keep in step for no gain.
    /// </summary>
    public class Boot : MonoBehaviour
    {
        [Tooltip("Skips the studio logo. Useful while working on anything else.")]
        public bool showLogo = true;

        IEnumerator Start()
        {
            GameState.Ensure();
            var flow = SceneFlow.Ensure();
            flow.SnapToBlack();

            if (showLogo)
            {
                var intro = gameObject.AddComponent<LogoIntro>();
                yield return intro.Play();
                Destroy(intro);
            }

            flow.Go(SceneFlow.MainMenu);
        }
    }
}
