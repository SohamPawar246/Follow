using UnityEngine;

namespace Follow.World
{
    /// <summary>
    /// Dresses a particle system's renderer at load, rather than at bake time.
    ///
    /// <see cref="ParticleArt"/> builds its two materials with
    /// <c>HideFlags.HideAndDontSave</c>, which is right for something generated at runtime
    /// and shared - but it means the material cannot be written into a scene. Anything
    /// that called Dress from an editor script got a reference that was dead by the time
    /// the scene was loaded again, and Unity draws a missing material as magenta. That is
    /// what put the pink squares all over the menu.
    ///
    /// So the scene stores this component instead of a material, and the material is made
    /// fresh on the machine that is going to draw it.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleDress : MonoBehaviour
    {
        [Tooltip("Additive for anything that glows; alpha for anything that hides what is behind it.")]
        public bool additive = true;

        void Awake() => ParticleArt.Dress(GetComponent<ParticleSystem>(), additive);
    }
}
