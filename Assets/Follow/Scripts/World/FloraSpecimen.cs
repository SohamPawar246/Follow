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
            _model.transform.localScale = Vector3.one;
            foreach (var c in _model.GetComponentsInChildren<Collider>()) Destroy(c);

            // Measure, then scale to a real height. The three source kits are authored at
            // wildly different sizes - Fern_1 alone imports nine metres across - so a bare
            // multiplier means something different for every model. Taking the multiplier
            // as metres and normalising to it is the only way the numbers on the species
            // assets mean anything: this fern was standing thirty-four metres wide, and
            // the bamboo was a five-metre disc eighty centimetres tall, which from
            // overhead is the black splat on the grass rather than a plant.
            // Largest dimension, exactly as the editor normalises the scattered props, so
            // the plant keeps its own proportions and only its overall size is decided.
            Vector3 size = Measure().size;
            float largest = Mathf.Max(0.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            float metres = Mathf.Max(0.3f, species.worldScale) * scaleBoost;
            _model.transform.localScale = Vector3.one * (metres / largest);

            Distinguish();
            Halo();
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

        Bounds Bounds() => Measure();

        /// <summary>The model's own extent, whatever scale it happens to be at.</summary>
        Bounds Measure()
        {
            var bounds = new Bounds(transform.position, Vector3.one);
            bool first = true;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>
        /// One tinted material per species, made once and shared by every specimen of it.
        ///
        /// This was a MaterialPropertyBlock, which is the cheap way to do it and cannot
        /// do the important half: a block can override <c>_EmissionColor</c> but it cannot
        /// enable the <c>_EMISSION</c> keyword, so the lift that was supposed to make a
        /// specimen glow silently did nothing and only the darkening multiply survived.
        /// Four materials for four plants is not a leak.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, Material[]> Tinted =
            new System.Collections.Generic.Dictionary<string, Material[]>();

        void Distinguish()
        {
            foreach (var r in _model.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = Variants(species, r.sharedMaterials);
        }

        static Material[] Variants(SpeciesData species, Material[] sources)
        {
            string key = species.id + "|" + sources.Length + "|"
                       + (sources.Length > 0 && sources[0] != null ? sources[0].name : "-");

            if (Tinted.TryGetValue(key, out var cached)) return cached;

            var made = new Material[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;

                var mat = new Material(sources[i]) { name = sources[i].name + "_" + species.id };

                // Barely any of the way to the tint. Base colour multiplies the model's
                // own texture, and the three source kits paint their leaves at wildly
                // different values, so a strong tint reads as a burnt patch on one model
                // and as the wrong colour entirely on another. The hue is carried by the
                // light below instead; this only warms the surface toward it.
                var wash = Color.Lerp(Color.white, species.tint, 0.22f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", wash);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", wash);

                made[i] = mat;
            }

            Tinted[key] = made;
            return made;
        }

        /// <summary>
        /// A small tinted light sitting inside the plant.
        ///
        /// This is the part that actually makes a specimen findable, and it is a light
        /// rather than a colour on the material for one plain reason: light adds, and a
        /// material tint multiplies. Multiplying is at the mercy of whatever the model's
        /// own texture happens to be - the same tint that made one plant glow turned the
        /// next one nearly black and a third an improbable purple. A light in the tint
        /// colour lifts every model the same way, whatever it is made of.
        /// </summary>
        void Halo()
        {
            var size = Bounds().size;

            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            // Above the plant, not inside it. The camera looks down, so the surfaces that
            // need lifting are the upper leaves - a light buried in the middle of a dense
            // bush lights the inside of it and nothing you can see.
            go.transform.localPosition = Vector3.up * (size.y + 0.5f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(species.tint, Color.white, 0.35f);
            light.intensity = 1.5f;
            light.range = Mathf.Max(4f, size.magnitude * 1.3f);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;

            go.AddComponent<Breathe>().light = light;
        }

        /// <summary>
        /// The glow rises and falls very slowly. A constant light reads as a bug in the
        /// scene; one that breathes reads as the plant being worth looking at.
        /// </summary>
        class Breathe : MonoBehaviour
        {
            public Light light;
            float _phase;

            void Start() => _phase = Random.value * 10f;

            void Update()
            {
                if (light == null) return;
                _phase += Time.deltaTime;
                light.intensity = 1.5f + Mathf.Sin(_phase * 1.1f) * 0.45f;
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
