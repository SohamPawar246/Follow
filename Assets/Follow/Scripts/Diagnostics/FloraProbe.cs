#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using Follow.Data;
using Follow.Game;
using Follow.World;

namespace Follow.Diagnostics
{
    /// <summary>
    /// Editor-only. Lines every flora specimen up in front of the player and photographs
    /// the row, so the tinting can be judged by looking at it rather than by reasoning
    /// about property blocks.
    /// </summary>
    public class FloraProbe : MonoBehaviour
    {
        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);

            var player = PlayerMover.Instance;
            var library = SpeciesLibrary.Active;
            if (player == null || library == null) yield break;

            var log = new StringBuilder();
            Vector3 origin = player.transform.position;

            int i = 0;
            foreach (var species in library.species)
            {
                if (species == null || species.kind != SpeciesKind.Flora) continue;
                if (species.modelPrefab == null) continue;

                Vector3 at = origin + new Vector3((i - 1.5f) * 4.5f, 0f, 6f);
                at.y = WorldComposer.Height(at.x, at.z);

                var specimen = FloraSpecimen.Spawn(species, at, null);
                log.AppendLine(species.commonName + " at x+" + ((i - 1.5f) * 4.5f).ToString("0.0"));

                if (specimen != null)
                    foreach (var r in specimen.GetComponentsInChildren<MeshRenderer>())
                    {
                        var block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        log.AppendLine("   " + r.name
                            + "  shader " + r.sharedMaterial.shader.name
                            + "  matBase " + r.sharedMaterial.GetColor("_BaseColor")
                            + "  blockBase " + (block.isEmpty ? "(empty)"
                                : block.GetColor("_BaseColor").ToString())
                            + "  enabled " + r.enabled
                            + "  bounds " + r.bounds.size.ToString("0.0")
                            + "  map " + (r.sharedMaterial.HasProperty("_BaseMap")
                                ? (r.sharedMaterial.GetTexture("_BaseMap") != null
                                   ? r.sharedMaterial.GetTexture("_BaseMap").name : "NONE") : "n/a")
                            + "  emissive " + (r.sharedMaterial.HasProperty("_EmissionColor")
                                ? r.sharedMaterial.GetColor("_EmissionColor").ToString() : "n/a")
                            + "  keywords [" + string.Join(",", r.sharedMaterial.shaderKeywords) + "]");
                    }
                i++;
            }

            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot("Logs/flora_row.png", 1);
            yield return new WaitForSeconds(1.5f);

            Debug.Log("FloraProbe:\n" + log);
        }
    }
}
#endif
