using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Follow.UI;

namespace Follow.Core
{
    /// <summary>
    /// Scene transitions with a warm fade. Persistent, so any scene can ask for the next
    /// one without knowing how the fade works.
    /// </summary>
    public class SceneFlow : MonoBehaviour
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Story = "Story";
        public const string Game = "Game";

        public static SceneFlow Instance { get; private set; }

        public float fadeOut = 0.45f;
        public float fadeIn = 0.55f;

        CanvasGroup _group;
        Image _sheet;
        bool _busy;

        public static SceneFlow Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<SceneFlow>();
            if (existing != null) return existing;
            return new GameObject("SceneFlow").AddComponent<SceneFlow>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildSheet();
        }

        void BuildSheet()
        {
            var canvas = UIFactory.CreateCanvas("FadeCanvas", 9000);
            canvas.transform.SetParent(transform, false);

            _sheet = UIFactory.Solid("Sheet", canvas.transform, CozyTheme.Active.fade);
            UIFactory.Stretch(_sheet.rectTransform);
            _sheet.raycastTarget = false;

            _group = _sheet.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void Go(string sceneName)
        {
            if (_busy) return;
            StartCoroutine(Transition(sceneName));
        }

        /// <summary>Fades to black, loads, then fades back in once the new scene has had a frame to settle.</summary>
        IEnumerator Transition(string sceneName)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            _sheet.raycastTarget = true;

            yield return UITween.FadeGroup(_group, 1f, fadeOut);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            // One frame for the incoming scene to build its UI before we reveal it.
            yield return null;
            yield return null;

            yield return UITween.FadeGroup(_group, 0f, fadeIn);

            _group.blocksRaycasts = false;
            _sheet.raycastTarget = false;
            _busy = false;
        }

        /// <summary>Used by the boot scene, which should never flash its own emptiness.</summary>
        public void SnapToBlack()
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
        }
    }
}
