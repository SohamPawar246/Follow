#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using Follow.Core;
using Follow.Game;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Takes the screenshots the presentation uses, from the current build
    /// rather than from whatever was lying in the Logs folder from three rounds ago.
    /// </summary>
    public class DeckShots : MonoBehaviour
    {
        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(3f);

            var player = PlayerMover.Instance;
            var cycle = DayCycle.Instance;
            var state = GameState.Instance;
            if (player == null || cycle == null) yield break;

            // Mid-morning, so the woods are lit the way the game usually looks.
            cycle.SetTime(0.22f);
            if (state != null) { state.sticks = 6; state.food = 4; }
            yield return new WaitForSeconds(1.5f);

            ScreenCapture.CaptureScreenshot("Logs/deck_woods.png", 1);
            yield return new WaitForSeconds(1.5f);

            // Stand on a bank so the water fills a good part of the frame.
            var p = player.transform.position;
            if (WorldComposer.NearestPond(new Vector2(p.x, p.z), 400f, out var pond))
            {
                Vector2 bank = pond.position + Vector2.left * (pond.radius + 3.5f);
                player.transform.position = new Vector3(
                    bank.x, WorldComposer.Height(bank.x, bank.y) + 0.3f, bank.y);

                // Face the water, so the camera looks across it.
                Vector3 toWater = new Vector3(pond.position.x, 0f, pond.position.y)
                                - player.transform.position;
                toWater.y = 0f;
                if (toWater.sqrMagnitude > 0.01f)
                    player.transform.rotation = Quaternion.LookRotation(toWater, Vector3.up);

                yield return new WaitForSeconds(3f);
                ScreenCapture.CaptureScreenshot("Logs/deck_pond.png", 1);
                yield return new WaitForSeconds(1.5f);
            }
            else Debug.LogWarning("DeckShots: no pond within 400 m");

            Debug.Log("DeckShots: done");
        }
    }
}
#endif
