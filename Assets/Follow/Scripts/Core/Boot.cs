using UnityEngine;

namespace Follow.Core
{
    /// <summary>
    /// First scene in the build. Creates the persistent systems and hands straight over
    /// to the menu, so nothing else has to check whether they exist.
    /// </summary>
    public class Boot : MonoBehaviour
    {
        void Start()
        {
            GameState.Ensure();
            var flow = SceneFlow.Ensure();
            flow.SnapToBlack();
            flow.Go(SceneFlow.MainMenu);
        }
    }
}
