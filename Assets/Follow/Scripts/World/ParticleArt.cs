using UnityEngine;
using UnityEngine.Rendering;

namespace Follow.World
{
    /// <summary>
    /// The two materials every particle in the game uses.
    ///
    /// Built once and shared, because the shape is generated in the shader rather than
    /// sampled from a texture - there is nothing per-system to vary except the blend mode.
    /// Additive for anything that glows (pollen, embers, flame, fireflies) and alpha for
    /// anything that hides what is behind it (smoke).
    /// </summary>
    public static class ParticleArt
    {
        static Material _additive;
        static Material _alpha;

        public static Material Additive => _additive != null
            ? _additive
            : _additive = Build(BlendMode.SrcAlpha, BlendMode.One, 0.12f, 0.6f);

        public static Material Alpha => _alpha != null
            ? _alpha
            : _alpha = Build(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 0.05f, 0.85f);

        static Material Build(BlendMode source, BlendMode destination, float core, float softness)
        {
            var shader = Shader.Find("Follow/SoftParticle");
            if (shader == null)
            {
                // Sprites/Default is always present and is at least alpha-blended, which
                // is a far better failure than the opaque squares the stock URP particle
                // shader gives you when it is configured from script.
                shader = Shader.Find("Sprites/Default");
                return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetFloat("_SrcBlend", (float)source);
            mat.SetFloat("_DstBlend", (float)destination);
            mat.SetFloat("_Core", core);
            mat.SetFloat("_Softness", softness);
            mat.renderQueue = 3100;
            return mat;
        }

        /// <summary>Wires a system's renderer up for view-facing soft billboards.</summary>
        public static void Dress(ParticleSystem ps, bool additive)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = additive ? Additive : Alpha;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0f;
        }
    }
}
