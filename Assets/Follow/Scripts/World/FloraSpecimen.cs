using UnityEngine;
using Follow.Data;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// A plant worth photographing, as opposed to the several thousand that are not.
    ///
    /// The forest scatters bushes and ferns everywhere, which is what makes it a forest and
    /// exactly what makes a survey target invisible in it. A specimen is the same model
    /// standing taller, in its own colour, with light coming off it and motes drifting
    /// around it. You can pick one out across a clearing, which is the whole point: flora
    /// is the half of the survey the dog cannot help you with, so it has to be findable
    /// by eye.
    /// </summary>
    public class FloraSpecimen : MonoBehaviour
    {
        public SpeciesData species;

        [Tooltip("How much bigger than the ambient planting of the same model.")]
        public float scaleBoost = 1.75f;

        GameObject _model;

        public static FloraSpecimen Spawn(SpeciesData species, Vector3 at, Transform parent)
        {
            if (species == null || species.modelPrefab == null) return null;

            var go = new GameObject("Flora_" + species.id);
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var specimen = go.AddComponent<FloraSpecimen>();
            specimen.species = species;
            specimen.Build();
            return specimen;
        }

        void Build()
        {
            _model = Instantiate(species.modelPrefab, transform);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localScale = Vector3.one * Mathf.Max(0.01f, species.worldScale) * scaleBoost;
            foreach (var c in _model.GetComponentsInChildren<Collider>()) Destroy(c);

            Distinguish();
            Sparkle();

            // The lens does not care what kind of thing this is, only where to point.
            var subject = gameObject.AddComponent<PhotoSubject>();
            subject.species = species;
            subject.wariness = 0f;

            var aim = new GameObject("Aim").transform;
            aim.SetParent(transform, false);
            aim.localPosition = Vector3.up * Bounds().size.y * 0.6f;
            subject.aimAt = aim;
        }

        Bounds Bounds()
        {
            var bounds = new Bounds(transform.position, Vector3.one);
            bool first = true;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>
        /// Colour and light, per instance. A property block rather than a material copy,
        /// so a hundred specimens over a session do not leak a hundred materials.
        /// </summary>
        void Distinguish()
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in _model.GetComponentsInChildren<MeshRenderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", species.tint);
                // The wind shader's rim is doing the work here: a warm edge that reads as
                // "this one is lit differently" from any angle.
                block.SetColor("_RimColor", Color.Lerp(species.tint, Color.white, 0.6f));
                block.SetFloat("_RimStrength", 1.35f);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>A few slow motes, so it catches the eye from outside the fog line.</summary>
        void Sparkle()
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.5f;

            var ps = go.AddComponent<ParticleSystem>();
            ParticleArt.Dress(ps, true);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(species.tint, Color.white, 0.55f),
                Color.Lerp(species.tint, Color.white, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.012f;
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.9f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.3f),
                    new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f)
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

    }
}
