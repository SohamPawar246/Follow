using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Follow.Core;
using Follow.Dog;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Runs the dog for a stretch and records what it decided to do. Editor-only:
    /// proves the state machine and scent detection actually fire without a human
    /// watching the Game view.
    /// </summary>
    public class DogProbe : MonoBehaviour
    {
        public float seconds = 26f;
        public float bond = 0.55f;

        readonly StringBuilder _log = new StringBuilder();
        float _elapsed;

        IEnumerator Start()
        {
            var state = GameState.Ensure();
            state.bond = bond;
            state.dogEnergy = 1f;

            var dog = DogBrain.Instance;
            if (dog == null) { Write("NO DOG IN SCENE"); yield break; }

            _log.AppendLine("bond=" + bond + "  rangeRadius=" + dog.RangeRadius.ToString("0.0"));
            _log.AppendLine("scent points in world: " + ScentPoint.Active.Count);

            dog.StateChanged += (from, to) =>
                _log.AppendLine(_elapsed.ToString("00.0") + "s  " + from + " -> " + to
                                + "   dist=" + dog.DistanceToPlayer.ToString("0.0"));
            dog.Pointed += p =>
                _log.AppendLine(_elapsed.ToString("00.0") + "s  POINTED AT "
                                + (p.species != null ? p.species.commonName : "?"));
            dog.Barked += () => _log.AppendLine(_elapsed.ToString("00.0") + "s  bark");

            float sample = 0f;
            while (_elapsed < seconds)
            {
                _elapsed += Time.deltaTime;
                sample += Time.deltaTime;
                if (sample >= 4f)
                {
                    sample = 0f;
                    _log.AppendLine(_elapsed.ToString("00.0") + "s  [" + dog.State
                                    + "] speed=" + dog.Speed.ToString("0.00")
                                    + " gait=" + dog.Gait.ToString("0.00")
                                    + " dist=" + dog.DistanceToPlayer.ToString("0.0"));
                }
                yield return null;
            }

            _log.AppendLine("final state: " + dog.State);
            _log.AppendLine("moved total: " + dog.transform.position);
            Write(_log.ToString());

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        static void Write(string text)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), "Logs/dog.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text);
        }
    }
}
