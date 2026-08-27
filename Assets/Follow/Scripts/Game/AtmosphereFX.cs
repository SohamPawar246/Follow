using UnityEngine;

namespace Follow.Game
{
    /// <summary>
    /// Airborne life: pollen in the sun, leaves on the wind, fireflies after dark.
    ///
    /// All three systems ride the camera rather than the world, so a handful of particles
    /// fills the whole screen no matter where the player walks. Density and colour follow
    /// the day cycle, which is what stops it reading as a screensaver.
    /// </summary>
    public class AtmosphereFX : MonoBehaviour
    {
        [Header("Counts")]
        public int pollenCount = 90;
        public int leafCount = 26;
        [Tooltip("Deliberately sparse. A cloud of them reads as static, not as fireflies.")]
        public int fireflyCount = 16;

        [Header("Firefly glow")]
        [Tooltip("Real point lights that drift with the swarm, so the dark has warm holes in it.")]
        public int glowLights = 5;
        public float glowRange = 5.5f;
        public float glowIntensity = 1.5f;

        [Header("Volume around the camera focus")]
        public float radius = 26f;
        public float height = 14f;

        ParticleSystem _pollen;
        ParticleSystem _leaves;
        ParticleSystem _fireflies;
        Transform _focus;

        Light[] _glow;
        Vector3[] _glowHome;
        float[] _glowPhase;

        void Start()
        {
            var player = PlayerMover.Instance;
            _focus = player != null ? player.transform : transform;

            _pollen = BuildPollen();
            _leaves = BuildLeaves();
            _fireflies = BuildFireflies();
            BuildGlow();
        }

        void LateUpdate()
        {
            if (_focus == null)
            {
                var player = PlayerMover.Instance;
                if (player == null) return;
                _focus = player.transform;
            }

            // Keep the emitters centred on the player so the effect is always on screen.
            Vector3 centre = _focus.position + Vector3.up * height * 0.35f;
            if (_pollen != null) _pollen.transform.position = centre;
            if (_leaves != null) _leaves.transform.position = centre + Vector3.up * height * 0.4f;
            if (_fireflies != null) _fireflies.transform.position = centre;

            var cycle = DayCycle.Instance;
            if (cycle == null) return;

            // Pollen belongs to daylight, fireflies to the dark. Crossfade between them
            // rather than switching, so dusk has both for a moment.
            float day = cycle.Daylight;
            float night = cycle.Night;

            SetRate(_pollen, pollenCount * day);
            SetRate(_leaves, leafCount * Mathf.Lerp(0.4f, 1f, day));
            SetRate(_fireflies, fireflyCount * night);

            DriftGlow(centre, night);
        }

        /// <summary>
        /// A handful of small warm lights that wander with the swarm. Additive sprites
        /// alone give you dots on a black screen; these make the dots feel like they are
        /// in the world, because the leaves under them actually brighten.
        /// </summary>
        void BuildGlow()
        {
            _glow = new Light[Mathf.Max(0, glowLights)];
            _glowHome = new Vector3[_glow.Length];
            _glowPhase = new float[_glow.Length];

            for (int i = 0; i < _glow.Length; i++)
            {
                var go = new GameObject("FireflyGlow");
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.78f, 1f, 0.55f);
                light.range = glowRange;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                _glow[i] = light;
                _glowPhase[i] = Random.value * 100f;
                _glowHome[i] = Random.insideUnitSphere;
            }
        }

        void DriftGlow(Vector3 centre, float night)
        {
            if (_glow == null) return;

            for (int i = 0; i < _glow.Length; i++)
            {
                var light = _glow[i];
                if (light == null) continue;

                float p = _glowPhase[i];
                var wander = new Vector3(
                    Mathf.PerlinNoise(p, Time.time * 0.14f) - 0.5f,
                    Mathf.PerlinNoise(p + 9f, Time.time * 0.1f) * 0.35f,
                    Mathf.PerlinNoise(p + 21f, Time.time * 0.12f) - 0.5f);

                light.transform.position = centre
                    + new Vector3(_glowHome[i].x, 0f, _glowHome[i].z) * radius * 0.55f
                    + wander * 14f + Vector3.up * 0.6f;

                // Each one breathes on its own clock, so they never pulse together.
                float blink = 0.45f + 0.55f * Mathf.PerlinNoise(p + 3f, Time.time * 0.9f);
                light.intensity = glowIntensity * night * blink;
                light.enabled = light.intensity > 0.02f;
            }
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        // --- systems ------------------------------------------------------------------

        ParticleSystem Create(string name, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            Follow.World.ParticleArt.Dress(ps, additive);
            return ps;
        }

        ParticleSystem BuildPollen()
        {
            var ps = Create("Pollen", true);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.97f, 0.75f, 0.7f), new Color(1f, 1f, 0.9f, 0.35f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.gravityModifier = -0.006f;   // drifts gently upward

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, height, radius * 2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.12f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeInOut();
            return ps;
        }

        ParticleSystem BuildLeaves()
        {
            var ps = Create("Leaves", false);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.72f, 0.32f, 0.9f), new Color(0.6f, 0.75f, 0.35f, 0.85f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.055f;
            main.maxParticles = 120;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 2f, radius * 2f);

            // Tumbling, not falling straight: leaves rotate and get pushed sideways.
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes have to be the same kind of curve, or the module refuses the
            // assignment and Unity logs about it every single frame.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.9f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeInOut();
            return ps;
        }

        ParticleSystem BuildFireflies()
        {
            var ps = Create("Fireflies", true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 1f, 0.45f, 1f), new Color(1f, 0.95f, 0.5f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;
            main.gravityModifier = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 1.7f, height * 0.35f, radius * 1.7f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.1f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.5f;

            // The blink is what makes them read as fireflies rather than sparks.
            var blink = ps.colorOverLifetime;
            blink.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.15f, 0.3f), new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0.2f, 0.68f), new GradientAlphaKey(0.9f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                });
            blink.color = new ParticleSystem.MinMaxGradient(gradient);
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeInOut()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
