using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Follow.Core;
using Follow.Game;

namespace Follow.World
{
    /// <summary>
    /// The fire at the centre of the map, and the only thing in the game you build.
    ///
    /// Before it exists there is a marked plot with a plus over it, so the middle of camp
    /// reads as unfinished rather than empty. Once lit it burns the sticks you and the dog
    /// bring back, and it is the reason firewood is worth carrying: the fire is the light,
    /// the warmth and the thing the dog sleeps beside.
    ///
    /// Everything the fire knows lives on <see cref="GameState"/>, so it survives the day.
    /// </summary>
    public class Campfire : MonoBehaviour
    {
        public static Campfire Instance { get; private set; }

        [Header("Cost")]
        public int buildCost = 4;
        [Tooltip("Seconds of burn one stick buys.")]
        public float secondsPerStick = 30f;
        public float maxFuel = 300f;

        [Header("Reach")]
        public float interactRadius = 4.5f;

        [Header("Art")]
        public GameObject stonesModel;
        public GameObject logsModel;
        public GameObject woodpileModel;

        GameObject _plot;
        GameObject _built;
        Light _light;
        ParticleSystem _flame;
        ParticleSystem _embers;
        ParticleSystem _smoke;
        Transform _woodpile;
        AudioSource _crackle;

        GameState _state;
        float _flicker;
        bool _wasLit;
        bool _warnedLow;

        public bool IsBuilt => _state != null && _state.campfireBuilt;
        public bool IsLit => IsBuilt && _state != null && _state.campfireFuel > 0f;

        /// <summary>0 to 1. Drives the flame, the light, the smoke and how safe night feels.</summary>
        public float Warmth => _state == null ? 0f : Mathf.Clamp01(_state.campfireFuel / Mathf.Max(1f, maxFuel));

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _state = GameState.Ensure();
            WorldStreamer.Drop(transform, 0f);

            BuildPlot();
            if (_state.campfireBuilt) Raise();
            Refresh();
        }

        // --- the unbuilt plot ------------------------------------------------------

        /// <summary>
        /// A dashed square with a plus floating over it. Drawn from quads rather than
        /// taken from an asset, because the marker has to sit flat on uneven ground and a
        /// decal projector is a lot of machinery for twelve rectangles.
        /// </summary>
        void BuildPlot()
        {
            _plot = new GameObject("BuildPlot");
            _plot.transform.SetParent(transform, false);
            // The camera looks down the world diagonal, so a mark laid out on the world
            // axes reads as a diamond and a plus reads as a multiplication sign. Turning
            // the whole plot to meet the camera is what makes it a square and a plus.
            _plot.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var mat = MarkerMaterial();
            const float half = 3.1f;
            const int perSide = 4;

            for (int side = 0; side < 4; side++)
            {
                Vector3 from = side switch
                {
                    0 => new Vector3(-half, 0f, -half),
                    1 => new Vector3(half, 0f, -half),
                    2 => new Vector3(half, 0f, half),
                    _ => new Vector3(-half, 0f, half)
                };
                Vector3 to = side switch
                {
                    0 => new Vector3(half, 0f, -half),
                    1 => new Vector3(half, 0f, half),
                    2 => new Vector3(-half, 0f, half),
                    _ => new Vector3(-half, 0f, -half)
                };

                for (int i = 0; i < perSide; i++)
                {
                    // Gaps between the dashes are what make it read as "not built yet".
                    float t = (i + 0.5f) / perSide;
                    Vector3 at = Vector3.Lerp(from, to, t);
                    var dash = Dash(mat, _plot.transform);
                    dash.localPosition = Ground(at);
                    dash.localRotation = Quaternion.LookRotation(Vector3.up, to - from);
                    // Fat dashes with real gaps. Hairlines on grass are invisible from
                    // fifteen metres up, which is the only distance anybody sees this from.
                    dash.localScale = new Vector3(0.5f, (to - from).magnitude / perSide * 0.62f, 1f);
                }
            }

            // The plus, painted flat on the ground inside the square. It used to hover
            // and turn to face the camera, which made it read as an interface element
            // floating in the world rather than as a mark somebody left on the site.
            var plus = new GameObject("Plus").transform;
            plus.SetParent(_plot.transform, false);
            plus.localPosition = Ground(Vector3.zero) + Vector3.up * 0.02f;
            plus.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat; the plot supplies the yaw

            var bar = Dash(mat, plus);
            bar.localPosition = Vector3.zero;
            bar.localRotation = Quaternion.identity;
            bar.localScale = new Vector3(1.5f, 0.34f, 1f);

            var post = Dash(mat, plus);
            // A hair above the bar; two coplanar quads would z-fight through each other.
            post.localPosition = new Vector3(0f, 0f, -0.01f);
            post.localRotation = Quaternion.identity;
            post.localScale = new Vector3(0.34f, 1.5f, 1f);

            _plusPivot = plus;
        }

        Transform _plusPivot;

        Vector3 Ground(Vector3 local)
        {
            Vector3 world = transform.position + local;
            return new Vector3(local.x,
                WorldComposer.Height(world.x, world.z) - transform.position.y + 0.06f, local.z);
        }

        static Transform Dash(Material mat, Transform parent)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Dash";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        static Material _marker;

        static Material MarkerMaterial()
        {
            if (_marker != null) return _marker;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            _marker = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var honey = new Color(1f, 0.78f, 0.28f, 1f);
            if (_marker.HasProperty("_BaseColor")) _marker.SetColor("_BaseColor", honey);
            _marker.color = honey;
            return _marker;
        }

        // --- the built fire ---------------------------------------------------------

        void Raise()
        {
            if (_built != null) return;

            _built = new GameObject("Fire");
            _built.transform.SetParent(transform, false);

            if (stonesModel != null)
            {
                var stones = Instantiate(stonesModel, _built.transform);
                stones.transform.localPosition = Vector3.zero;
                stones.transform.localScale = Vector3.one * 1.9f;
                foreach (var c in stones.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (logsModel != null)
            {
                var logs = Instantiate(logsModel, _built.transform);
                logs.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                logs.transform.localScale = Vector3.one * 1.6f;
                foreach (var c in logs.GetComponentsInChildren<Collider>()) Destroy(c);
            }

            // The woodpile beside the fire IS the stick counter. A number in the corner
            // never made anyone feel like they were running low.
            if (woodpileModel != null)
            {
                var pile = Instantiate(woodpileModel, _built.transform);
                pile.transform.localPosition = new Vector3(1.9f, 0f, -1.1f);
                pile.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
                foreach (var c in pile.GetComponentsInChildren<Collider>()) Destroy(c);
                _woodpile = pile.transform;
            }

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(_built.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.62f, 0.28f);
            _light.range = 22f;
            _light.shadows = LightShadows.Soft;
            _light.shadowStrength = 0.4f;

            _flame = Flame();
            _embers = Embers();
            _smoke = Smoke();

            _crackle = _built.AddComponent<AudioSource>();
            _crackle.spatialBlend = 1f;
            _crackle.loop = true;
            _crackle.volume = 0.35f;
            _crackle.minDistance = 3f;
            _crackle.maxDistance = 26f;
            _crackle.rolloffMode = AudioRolloffMode.Linear;
        }

        ParticleSystem MakeSystem(string name, Color start, Color end, bool additive = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_built.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ParticleArt.Dress(ps, additive);

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(start, end);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return ps;
        }

        ParticleSystem Flame()
        {
            var ps = MakeSystem("Flame", new Color(1f, 0.82f, 0.35f, 0.95f),
                                          new Color(1f, 0.45f, 0.15f, 0.85f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 120;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.32f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(1f);
            return ps;
        }

        ParticleSystem Embers()
        {
            var ps = MakeSystem("Embers", new Color(1f, 0.75f, 0.35f, 1f),
                                           new Color(1f, 0.45f, 0.2f, 1f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 90;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 26f;
            shape.radius = 0.45f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.75f;
            noise.frequency = 0.7f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(0.25f);
            return ps;
        }

        ParticleSystem Smoke()
        {
            var ps = MakeSystem("Smoke", new Color(0.72f, 0.70f, 0.66f, 0.32f),
                                          new Color(0.52f, 0.50f, 0.48f, 0.24f), false);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 90;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.3f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));

            // Drifts downwind as it rises, so the column leans instead of standing.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.45f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(0.3f);
            return ps;
        }

        static ParticleSystem.MinMaxGradient Fade(float holdUntil)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(holdUntil * 0.4f + 0.08f)),
                    new GradientAlphaKey(0.9f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(g);
        }

        // --- running ------------------------------------------------------------------

        void Update()
        {
            if (_state == null) return;
            float dt = Time.deltaTime;

            Burn(dt);
            Refresh();
            Animate(dt);
            Interact();
        }

        void Burn(float dt)
        {
            if (!IsBuilt || _state.campfireFuel <= 0f) return;

            // Night eats fuel faster, which is what makes the evening's woodpile a decision.
            var cycle = DayCycle.Instance;
            float rate = cycle != null && cycle.IsDusk ? 1.5f : 1f;
            _state.campfireFuel = Mathf.Max(0f, _state.campfireFuel - dt * rate);

            if (_state.campfireFuel < 40f && !_warnedLow)
            {
                _warnedLow = true;
                Follow.UI.GameHud.Instance?.Say("the fire is burning low");
            }
            if (_state.campfireFuel > 60f) _warnedLow = false;

            if (_state.campfireFuel <= 0f)
                Follow.UI.GameHud.Instance?.Say("the fire has gone out");
        }

        void Refresh()
        {
            bool built = IsBuilt;
            if (_plot != null && _plot.activeSelf == built) _plot.SetActive(!built);   // built means the plot is done with
            if (!built) return;

            if (_built == null) Raise();

            float warmth = Warmth;
            bool lit = warmth > 0f;

            SetRate(_flame, lit ? Mathf.Lerp(24f, 60f, warmth) : 0f);
            SetRate(_embers, lit ? Mathf.Lerp(6f, 20f, warmth) : 0f);
            // Smoke outlives the flame: embers keep smoking for a while after it dies,
            // so the column thinning out is the warning the fire is failing.
            SetRate(_smoke, Mathf.Lerp(4f, 22f, warmth));

            if (_crackle != null)
            {
                if (lit && !_crackle.isPlaying && _crackle.clip != null) _crackle.Play();
                if (!lit && _crackle.isPlaying) _crackle.Stop();
                _crackle.volume = 0.15f + warmth * 0.3f;
            }

            if (_woodpile != null)
            {
                // The pile grows with what you are carrying, up to a full stack.
                float pile = Mathf.Clamp01(_state.sticks / 12f);
                float s = Mathf.Lerp(0.35f, 1.15f, pile);
                _woodpile.localScale = Vector3.one * s;
                _woodpile.gameObject.SetActive(_state.sticks > 0);
            }

            if (_wasLit != lit)
            {
                _wasLit = lit;
                if (lit) Follow.UI.GameHud.Instance?.Say("the fire is lit");
            }
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        void Animate(float dt)
        {
            if (_plusPivot != null && _plot != null && _plot.activeSelf)
            {
                // Flat on the ground, so it only breathes rather than moving.
                float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.07f;
                _plusPivot.localScale = Vector3.one * pulse;
            }

            if (_light == null) return;
            float warmth = Warmth;
            _flicker = Mathf.Lerp(_flicker, Random.Range(0.86f, 1.14f), dt * 9f);
            _light.intensity = Mathf.Lerp(0f, 4.4f, warmth) * _flicker;
            _light.range = Mathf.Lerp(8f, 24f, warmth);
        }

        // --- the player -----------------------------------------------------------------

        void Interact()
        {
            var player = PlayerMover.Instance;
            var hud = Follow.UI.GameHud.Instance;
            if (player == null || hud == null) return;

            float d = Vector3.Distance(player.transform.position, transform.position);
            if (d > interactRadius) { hud.HidePrompt(this); return; }

            bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

            if (!IsBuilt)
            {
                bool can = _state.sticks >= buildCost;

                // Nothing to say until you are actually carrying wood. Telling somebody
                // they need four more sticks before they have picked up their first one
                // is not a hint, it is a second tutorial talking over the first.
                if (_state.sticks <= 0) { hud.HidePrompt(this); return; }

                hud.ShowPrompt(this, can
                    ? "E   build the fire   (" + buildCost + " sticks)"
                    : "gather " + (buildCost - _state.sticks) + " more sticks for a fire", 3);
                if (pressed && can)
                {
                    _state.sticks -= buildCost;
                    _state.campfireBuilt = true;
                    _state.campfireFuel = secondsPerStick * buildCost;
                    Raise();
                    Refresh();
                    hud.HidePrompt(this);
                }
                return;
            }

            if (_state.sticks <= 0)
            {
                hud.ShowPrompt(this, IsLit ? "the fire is burning"
                    : "the fire is out - it needs sticks", 3);
                return;
            }

            hud.ShowPrompt(this, "E   feed the fire   (" + _state.sticks + " sticks)", 3);
            if (!pressed) return;

            int spend = Mathf.Min(_state.sticks,
                Mathf.CeilToInt((maxFuel - _state.campfireFuel) / secondsPerStick));
            if (spend <= 0) { hud.Say("the fire is already roaring"); return; }

            _state.sticks -= spend;
            _state.campfireFuel = Mathf.Min(maxFuel, _state.campfireFuel + spend * secondsPerStick);
            hud.Say(spend == 1 ? "a stick on the fire" : spend + " sticks on the fire");
        }
    }
}
