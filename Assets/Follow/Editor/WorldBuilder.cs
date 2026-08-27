using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Follow.Game;
using Follow.World;

namespace Follow.EditorTools
{
    /// <summary>
    /// Bakes the world's content and wires the scene that streams it.
    ///
    /// Nothing here runs while the game is playing. Every model that will be scattered is
    /// turned into a prefab with its wind material and its collider already attached, so
    /// the streamer's job at runtime is a plain Instantiate - no material creation, no
    /// collider fitting, no asset database, on the frame you are walking.
    /// </summary>
    public static class WorldBuilder
    {
        const string MegaKit = "Assets/Stylized Nature MegaKit[Standard]/FBX (Unity)";
        const string KayKit = "Assets/KayKit_Forest_Nature_Pack_1.0_FREE/KayKit_Forest_Nature_Pack_1.0_FREE/Assets/fbx(unity)";
        const string Kenney = "Assets/kenney_nature-kit/Models/FBX format";
        const string Root = "Assets/Follow";
        const string Prefabs = Root + "/Prefabs/World";

        enum Solidity { None, Trunk, Rock }

        [MenuItem("Follow/Build The World", priority = 19)]
        public static void BuildAll()
        {
            var palette = BuildPalette();
            WireScene(palette);
            AssetDatabase.SaveAssets();

            int models = palette.layers.Sum(l => l.prefabs.Count);
            Debug.Log("World built: " + palette.layers.Count + " scatter layers over "
                      + models + " baked prefabs. The forest now streams and has no edge.");
        }

        // --- the palette --------------------------------------------------------------

        static WorldPalette BuildPalette()
        {
            FollowBuildUtils.EnsureFolder(Prefabs);
            FollowBuildUtils.EnsureFolder(Root + "/Resources");

            string path = Root + "/Resources/WorldPalette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<WorldPalette>(path);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<WorldPalette>();
                AssetDatabase.CreateAsset(palette, path);
            }
            palette.layers.Clear();

            var wind = new Dictionary<Texture, Material>();

            // Canopy first: these use the coarse grid and block everything under them.
            Layer(palette, wind, "Pine", ScatterRule.Pine, true, Solidity.Trunk,
                new Vector2(7f, 12f), 1f, Load(MegaKit, "Pine"));

            Layer(palette, wind, "Broadleaf", ScatterRule.Broadleaf, true, Solidity.Trunk,
                new Vector2(6f, 10f), 1f, Load(KayKit, "Tree_1", "Tree_2", "Tree_3", "Tree_4"));

            Layer(palette, wind, "AccentTree", ScatterRule.AccentTree, true, Solidity.Trunk,
                new Vector2(6f, 9f), 0.32f, Load(MegaKit, "CommonTree", "TwistedTree"));

            Layer(palette, wind, "DeadTree", ScatterRule.DeadTree, true, Solidity.Trunk,
                new Vector2(6f, 10f), 0.9f,
                Load(MegaKit, "DeadTree").Concat(Load(KayKit, "Tree_Bare")).ToList());

            // Detail, in the order it gets asked. Waterside wins near a pond, rocks win on
            // a slope, grass mops up whatever is left.
            // The "_Big" variants of these are chest-high palms. At full scale they read
            // as a jungle set dressing the player walks behind, so the big ones are out
            // and what is left is planted small.
            Layer(palette, wind, "Waterside", ScatterRule.Waterside, false, Solidity.None,
                new Vector2(0.7f, 1.4f), 0.7f,
                Load(MegaKit, "Plant_1", "Plant_7", "Fern_")
                    .Where(g => !g.name.EndsWith("_Big"))
                    .Concat(Load(KayKit, "Grass_2")).ToList());

            Layer(palette, wind, "Rocks", ScatterRule.Rock, false, Solidity.Rock,
                new Vector2(0.5f, 2.4f), 0.5f, Load(KayKit, "Rock_"));

            // These sit early in the detail order so they get first refusal on every
            // candidate point, which at ten percent buried the forest floor in firewood.
            // Two hundred sticks in view is not abundance, it is litter.
            Layer(palette, wind, "Firewood", ScatterRule.Firewood, false, Solidity.None,
                new Vector2(1.0f, 1.5f), 0.07f, Pickups(PickupKind.Stick));

            Layer(palette, wind, "Forage", ScatterRule.Forage, false, Solidity.None,
                new Vector2(0.45f, 0.7f), 0.06f, Pickups(PickupKind.Forage));

            Layer(palette, wind, "Understory", ScatterRule.Understory, false, Solidity.None,
                new Vector2(0.9f, 1.9f), 0.62f,
                Load(MegaKit, "Bush_").Concat(Load(KayKit, "Bush_")).ToList());

            Layer(palette, wind, "Ferns", ScatterRule.Fern, false, Solidity.None,
                new Vector2(0.4f, 0.8f), 0.5f,
                Load(MegaKit, "Clover", "Fern_").Where(g => !g.name.EndsWith("_Big")).ToList());

            Layer(palette, wind, "Flowers", ScatterRule.Flower, false, Solidity.None,
                new Vector2(0.45f, 0.8f), 0.28f, Load(MegaKit, "Petal", "Flower_"));

            Layer(palette, wind, "Grass", ScatterRule.Grass, false, Solidity.None,
                new Vector2(0.45f, 0.85f), 0.55f, Load(KayKit, "Grass_1", "Grass_2"));

            Layer(palette, wind, "Pebbles", ScatterRule.Pebble, false, Solidity.None,
                new Vector2(0.2f, 0.5f), 0.3f, Load(MegaKit, "Pebble"));

            palette.groundMaterial = GroundMaterial();
            palette.waterMaterial = WaterMaterial();

            palette.campfireStones = Kenny("campfire_stones");
            palette.campfireLogs = Kenny("campfire_logs");
            palette.tent = Kenny("tent_smallOpen");
            palette.logStack = Kenny("log_stack");
            palette.stump = Kenny("stump_round");

            EditorUtility.SetDirty(palette);
            WorldPalette.Active = palette;
            return palette;
        }

        static void Layer(WorldPalette palette, Dictionary<Texture, Material> wind, string name,
            ScatterRule rule, bool canopy, Solidity solidity, Vector2 scale, float chance,
            List<GameObject> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                Debug.LogWarning("World: no models found for " + name);
                return;
            }

            var layer = new ScatterLayer
            {
                name = name,
                rule = rule,
                canopy = canopy,
                scale = scale,
                chance = chance
            };

            foreach (var source in sources)
            {
                var baked = Bake(source, wind, solidity, canopy || solidity != Solidity.None);
                if (baked != null) layer.prefabs.Add(baked);
            }
            palette.layers.Add(layer);
        }

        /// <summary>
        /// One source model becomes one prefab with everything already on it. Rebuilt every
        /// time so a change to the wind settings actually reaches the world.
        /// </summary>
        static GameObject Bake(GameObject source, Dictionary<Texture, Material> wind,
            Solidity solidity, bool casts)
        {
            if (source == null) return null;
            string path = Prefabs + "/" + source.name + ".prefab";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) return null;
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance = Normalise(instance);

            // Foliage sways; rock and firewood do not, and a swaying boulder is a bug.
            if (solidity != Solidity.Rock) ApplyWind(instance, wind);

            foreach (var c in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            AddCollider(instance, solidity);
            if (solidity != Solidity.Rock) AddCrown(instance, solidity == Solidity.Trunk);

            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // Small ground detail casting shadows costs a lot and reads as noise.
                r.receiveShadows = true;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>
        /// Wraps a model so that its largest dimension is exactly one unit.
        ///
        /// Three kits at three different authoring scales cannot share one multiplier. A
        /// MegaKit fern is nine metres across as imported and a KayKit rock is half a
        /// metre; the same "scale 0.5" made one a palm tree and the other a pebble. After
        /// this, a layer's scale range is simply the size in metres that layer should be,
        /// which is a number a person can reason about.
        /// </summary>
        static GameObject Normalise(GameObject instance)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }

            float largest = first ? 1f : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest < 0.0001f) largest = 1f;

            var root = new GameObject(instance.name);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localScale = Vector3.one / largest;
            // Sit the model on the root's origin rather than wherever its pivot happened
            // to be, so nothing floats or sinks when it is planted.
            if (!first)
                instance.transform.localPosition =
                    new Vector3(0f, -(bounds.min.y - instance.transform.position.y) / largest, 0f);

            return root;
        }

        /// <summary>
        /// Firewood and forage are the same models, plus the component that makes them
        /// worth walking over. Baked separately so the scatter layers stay dumb.
        /// </summary>
        static List<GameObject> Pickups(PickupKind kind)
        {
            var sources = kind == PickupKind.Stick
                ? Kenny("log", "log_large")
                : Load(Kenney, "mushroom_").Concat(Load(MegaKit, "Mushroom")).ToList();

            var made = new List<GameObject>();
            foreach (var source in sources)
            {
                if (source == null) continue;
                string path = Prefabs + "/Pickup_" + kind + "_" + source.name + ".prefab";

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (instance == null) continue;
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                foreach (var c in instance.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c);

                // Kenney's kit ships without materials, so an unpainted log arrives as a
                // flat pink block. The MegaKit mushrooms come with their own colours.
                if (kind == PickupKind.Stick)
                    Paint(instance, new Color(0.44f, 0.31f, 0.20f), "M_Firewood");

                instance = Normalise(instance);

                var pickup = instance.AddComponent<Pickup>();
                pickup.kind = kind;
                pickup.amount = 1;

                made.Add(PrefabUtility.SaveAsPrefabAsset(instance, path));
                Object.DestroyImmediate(instance);
            }
            return made;
        }

        /// <summary>
        /// Kenney's nature kit ships geometry with no textures and no usable materials, so
        /// anything taken from it arrives untinted - which is why the firewood was showing
        /// up as flat pink blocks. Give each prop one shared lit material in a sane colour.
        /// </summary>
        static void Paint(GameObject instance, Color color, string materialName)
        {
            var mat = LoadOrCreate(materialName,
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(mat);

            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        static List<GameObject> Kenny(params string[] names)
        {
            var found = new List<GameObject>();
            foreach (var name in names)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Kenney + "/" + name + ".fbx");
                if (go != null) found.Add(go);
            }
            return found;
        }

        static GameObject Kenny(string name) => Kenny(new[] { name }).FirstOrDefault();

        static List<GameObject> Load(string folder, params string[] prefixes)
        {
            var found = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(folder)) return found;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(p);
                if (!prefixes.Any(pre => name.StartsWith(pre, System.StringComparison.OrdinalIgnoreCase)))
                    continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null && !found.Any(f => f.name == go.name)) found.Add(go);
            }
            return found;
        }

        // --- materials -----------------------------------------------------------------

        static Material GroundMaterial()
        {
            var shader = Shader.Find("Follow/StylizedGround");
            var mat = LoadOrCreate("M_ForestFloor", shader ?? Shader.Find("Universal Render Pipeline/Lit"));
            if (shader != null)
            {
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_GrainColor", new Color(0.20f, 0.27f, 0.14f));
                mat.SetFloat("_GrainStrength", 0.18f);
                mat.SetFloat("_GrainScale", 1.4f);
                mat.SetFloat("_AmbientBoost", 0.06f);
                mat.SetFloat("_SpecStrength", 0.05f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material WaterMaterial()
        {
            var shader = Shader.Find("Follow/StylizedWater");
            var mat = LoadOrCreate("M_Pond", shader ?? Shader.Find("Universal Render Pipeline/Lit"));
            if (shader != null)
            {
                mat.SetColor("_ShallowColor", new Color(0.48f, 0.82f, 0.76f, 0.72f));
                mat.SetColor("_DeepColor", new Color(0.09f, 0.28f, 0.40f, 0.95f));
                mat.SetColor("_FoamColor", new Color(0.94f, 0.99f, 0.98f, 1f));
                mat.SetFloat("_DepthRange", 2.6f);
                mat.SetFloat("_FoamWidth", 0.42f);
                mat.SetFloat("_RippleScale", 6.5f);
                mat.SetFloat("_RippleSpeed", 0.4f);
                mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreate(string name, Shader shader)
        {
            FollowBuildUtils.EnsureFolder(Root + "/Materials");
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            return mat;
        }

        static Material WindMaterialFor(Material source, Dictionary<Texture, Material> cache)
        {
            var shader = Shader.Find("Follow/FoliageWind");
            if (shader == null || source == null) return source;

            Texture tex = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            if (tex == null && source.HasProperty("_MainTex")) tex = source.GetTexture("_MainTex");

            var key = tex != null ? tex : (Texture)Texture2D.whiteTexture;
            if (cache.TryGetValue(key, out var made) && made != null) return made;

            var mat = LoadOrCreate("M_Wind_" + (tex != null ? tex.name : "Untextured"), shader);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            // Untextured kits (Kenney) carry their colour on the source material.
            else if (source.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (tex != null) mat.SetColor("_BaseColor", Color.white);

            mat.SetFloat("_WindStrength", 0.12f);
            mat.SetFloat("_WindSpeed", 1.1f);
            mat.SetFloat("_WindScale", 0.2f);
            mat.SetFloat("_WindHeightMask", 1.7f);
            mat.SetFloat("_RimStrength", 0.28f);
            mat.SetFloat("_AmbientBoost", 0.12f);
            EditorUtility.SetDirty(mat);

            cache[key] = mat;
            return mat;
        }

        static void ApplyWind(GameObject instance, Dictionary<Texture, Material> cache)
        {
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = WindMaterialFor(mats[i], cache);
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// A trigger around the leafy part, on its own layer, for the camera to sweep
        /// against. Sweeping the trunk alone misses the canopy that is actually in the way,
        /// because the leaves hang nowhere near the stem - and a waist-high bush standing
        /// between the camera and the player hides just as much as a pine does, so this is
        /// not only for trees.
        /// </summary>
        static void AddCrown(GameObject instance, bool tall)
        {
            var crown = new GameObject("Crown");
            crown.transform.SetParent(instance.transform, false);
            // Normalised units: the crown of a tree sits high, a bush is its own middle.
            crown.transform.localPosition = new Vector3(0f, tall ? 0.68f : 0.4f, 0f);
            crown.layer = FollowBuildUtils.Layer("Canopy");

            var sphere = crown.AddComponent<SphereCollider>();
            sphere.radius = tall ? 0.34f : 0.45f;
            sphere.isTrigger = true;
        }

        /// <summary>Trunks get a capsule, rocks a fitted box, foliage nothing at all.</summary>
        static void AddCollider(GameObject instance, Solidity solidity)
        {
            if (solidity == Solidity.Trunk)
            {
                // In normalised space the model is one unit tall, and the trunk is a
                // slender column up the middle of it.
                var capsule = instance.AddComponent<CapsuleCollider>();
                capsule.radius = 0.06f;
                capsule.height = 0.9f;
                capsule.center = new Vector3(0f, 0.45f, 0f);
                return;
            }
            if (solidity != Solidity.Rock) return;

            var bounds = new Bounds();
            bool first = true;
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            // Normalised units: a rock under a third of its own footprint in height is
            // flat enough to walk over.
            if (first || bounds.size.y < 0.3f) return;

            var box = instance.AddComponent<BoxCollider>();
            box.center = instance.transform.InverseTransformPoint(bounds.center);
            var scale = instance.transform.lossyScale;
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(0.01f, scale.x),
                bounds.size.y / Mathf.Max(0.01f, scale.y),
                bounds.size.z / Mathf.Max(0.01f, scale.z)) * 0.82f;
        }

        // --- the scene -------------------------------------------------------------------

        static void WireScene(WorldPalette palette)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager
                .OpenScene("Assets/Follow/Scenes/Game.unity",
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

            // The baked forest and its edge are gone for good.
            foreach (var name in new[] { "Forest", "Ground_PLACEHOLDER", "ScentPoints", "World" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            var world = new GameObject("World");
            world.AddComponent<CanopyFade>();
            var streamer = world.AddComponent<WorldStreamer>();
            streamer.palette = palette;
            world.AddComponent<ScentField>();
            world.AddComponent<FloraField>();
            world.AddComponent<AtmosphereFX>();

            BuildCamp(palette);
            Atmosphere();
            PostStack(world.transform);
            NightVolume(world.transform);
            Systems();
            TuneDayCycle();

            foreach (var name in new[] { "Player", "Dog" })
            {
                var actor = GameObject.Find(name);
                if (actor == null) continue;
                if (actor.GetComponent<GroundGuard>() == null) actor.AddComponent<GroundGuard>();
                var p = actor.transform.position;
                actor.transform.position = new Vector3(p.x, WorldComposer.Height(p.x, p.z) + 0.2f, p.z);
            }

            var rig = Object.FindFirstObjectByType<TopDownCamera>();
            if (rig != null)
            {
                // Closer and slightly narrower than before. At 24 metres the surveyor was
                // a thumbnail and the dog was a smudge; this is the distance at which you
                // can read what both of them are doing.
                rig.distance = 15.5f;
                rig.pitch = 47f;
                rig.fieldOfView = 38f;
                rig.heightOffset = 1.5f;
            }

            var hud = GameObject.Find("GameHud");
            if (hud != null)
            {
                if (hud.GetComponent<Follow.UI.PauseMenu>() == null) hud.AddComponent<Follow.UI.PauseMenu>();

                // The edge markers are gone. They were a map by another name, and the
                // whole point of this forest is that you learn it by walking it, so the
                // component was deleted outright - this only sweeps up the stale instance
                // a previously-saved scene is still carrying.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hud);

                if (hud.GetComponent<Follow.UI.Tutorial>() == null) hud.AddComponent<Follow.UI.Tutorial>();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// The gameplay systems, on one object. Each builds its own interface at runtime,
        /// so there is nothing here to wire by hand and nothing to forget.
        /// </summary>
        static void Systems()
        {
            var old = GameObject.Find("Systems");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("Systems");
            go.AddComponent<Soundscape>();
            go.AddComponent<SurvivalSystem>();
            go.AddComponent<SleepSystem>();
            go.AddComponent<FishingGame>();
            go.AddComponent<Photography>();
        }

        /// <summary>A second volume that only has an opinion after dark.</summary>
        static void NightVolume(Transform parent)
        {
            var old = GameObject.Find("NightMood");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("NightMood");
            go.transform.SetParent(parent, false);
            go.AddComponent<Volume>();
            go.AddComponent<NightMood>();
        }

        /// <summary>
        /// The clock is a full twenty-four hours now, so the ramps that were authored for
        /// a dawn-to-dusk sweep have to be re-laid across the whole circle - including the
        /// blue half that did not used to exist.
        /// </summary>
        static void TuneDayCycle()
        {
            var cycle = Object.FindFirstObjectByType<DayCycle>();
            if (cycle == null) return;

            cycle.dayLengthSeconds = 340f;
            cycle.nightSpeed = 2.6f;
            cycle.startTime = 0.10f;
            cycle.duskAt = 0.54f;
            cycle.darkAt = 0.62f;
            cycle.dawnAt = 0.94f;
            cycle.fogNear = 40f;
            cycle.fogFar = 155f;
            cycle.nightFogFar = 74f;
            cycle.moonColor = new Color(0.52f, 0.64f, 1f);
            cycle.moonIntensity = 0.34f;

            cycle.sunColor = Ramp(
                (0.00f, new Color(1.00f, 0.62f, 0.38f)),
                (0.10f, new Color(1.00f, 0.86f, 0.66f)),
                (0.28f, new Color(1.00f, 0.98f, 0.93f)),
                (0.46f, new Color(1.00f, 0.84f, 0.58f)),
                (0.55f, new Color(1.00f, 0.55f, 0.30f)),
                (1.00f, new Color(1.00f, 0.62f, 0.38f)));

            // The night half is the part that matters here: a properly cold blue, not a
            // dimmed version of the afternoon.
            cycle.ambientColor = Ramp(
                (0.00f, new Color(0.34f, 0.34f, 0.42f)),
                (0.14f, new Color(0.54f, 0.56f, 0.55f)),
                (0.40f, new Color(0.56f, 0.57f, 0.53f)),
                (0.55f, new Color(0.44f, 0.36f, 0.34f)),
                (0.66f, new Color(0.10f, 0.14f, 0.28f)),
                (0.88f, new Color(0.11f, 0.15f, 0.30f)),
                (1.00f, new Color(0.34f, 0.34f, 0.42f)));

            cycle.fogColor = Ramp(
                (0.00f, new Color(0.66f, 0.68f, 0.76f)),
                (0.16f, new Color(0.80f, 0.84f, 0.82f)),
                (0.44f, new Color(0.82f, 0.82f, 0.76f)),
                (0.56f, new Color(0.88f, 0.60f, 0.40f)),
                (0.68f, new Color(0.07f, 0.11f, 0.24f)),
                (0.90f, new Color(0.09f, 0.13f, 0.28f)),
                (1.00f, new Color(0.66f, 0.68f, 0.76f)));

            cycle.sunIntensity = new AnimationCurve(
                new Keyframe(0.00f, 0.15f),
                new Keyframe(0.12f, 0.95f),
                new Keyframe(0.28f, 1.20f),
                new Keyframe(0.48f, 0.85f),
                new Keyframe(0.56f, 0.35f),
                new Keyframe(0.61f, 0.00f),
                new Keyframe(0.93f, 0.00f),
                new Keyframe(1.00f, 0.20f));
        }

        static Gradient Ramp(params (float t, Color c)[] stops)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[stops.Length];
            for (int i = 0; i < stops.Length; i++) keys[i] = new GradientColorKey(stops[i].c, stops[i].t);
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static void BuildCamp(WorldPalette palette)
        {
            var old = GameObject.Find("Camp");
            if (old != null) Object.DestroyImmediate(old);

            var camp = new GameObject("Camp");
            camp.transform.position = new Vector3(0f, WorldComposer.Height(0f, 0f), 0f);

            var fire = camp.AddComponent<Campfire>();
            fire.stonesModel = PaintedCopy(palette.campfireStones, new Color(0.52f, 0.52f, 0.50f), "M_FireStones");
            fire.logsModel = PaintedCopy(palette.campfireLogs, new Color(0.40f, 0.27f, 0.17f), "M_FireLogs");
            fire.woodpileModel = PaintedCopy(palette.logStack, new Color(0.44f, 0.31f, 0.20f), "M_Woodpile");

            // Two things that say somebody lives here, placed by hand rather than
            // scattered, and big enough to read from the camera height.
            Place(palette.tent, camp.transform, new Vector3(-5.2f, 0f, 2.4f), 118f, 2.2f,
                new Color(0.78f, 0.66f, 0.45f), "M_Tent");
            Place(palette.stump, camp.transform, new Vector3(3.4f, 0f, 2.8f), 20f, 1.6f,
                new Color(0.46f, 0.33f, 0.22f), "M_Stump");
            Place(palette.stump, camp.transform, new Vector3(-2.6f, 0f, -3.6f), 200f, 1.4f,
                new Color(0.46f, 0.33f, 0.22f), "M_Stump");
        }

        static void Place(GameObject prefab, Transform parent, Vector3 offset, float yaw,
            float scale, Color color, string materialName)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            var at = parent.position + offset;
            go.transform.position = new Vector3(at.x, WorldComposer.Height(at.x, at.z), at.z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            Paint(go, color, materialName);
        }

        /// <summary>A prefab of a Kenney prop with a real material on it, saved once.</summary>
        static GameObject PaintedCopy(GameObject source, Color color, string materialName)
        {
            if (source == null) return null;
            string path = Prefabs + "/" + source.name + "_Painted.prefab";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            foreach (var c in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            Paint(instance, color, materialName);

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static void Atmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.76f, 0.86f, 0.94f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.70f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.24f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.75f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 155f;

            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.intensity = 1.9f;
                sun.color = new Color(1f, 0.96f, 0.86f);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.62f;
            }
        }

        static void PostStack(Transform parent)
        {
            FollowBuildUtils.EnsureFolder(Root + "/Settings");
            string path = Root + "/Settings/ForestVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Add<Bloom>(profile, b => { b.active = true; b.threshold.Override(0.85f);
                b.intensity.Override(0.9f); b.scatter.Override(0.72f);
                b.tint.Override(new Color(1f, 0.95f, 0.85f)); });

            Add<ColorAdjustments>(profile, c => { c.active = true; c.postExposure.Override(0.1f);
                c.contrast.Override(11f); c.saturation.Override(10f);
                c.colorFilter.Override(new Color(1f, 0.99f, 0.95f)); });

            Add<ShadowsMidtonesHighlights>(profile, s => { s.active = true;
                s.shadows.Override(new Vector4(0.92f, 0.98f, 1.12f, 0f));
                s.highlights.Override(new Vector4(1.10f, 1.02f, 0.90f, 0f)); });

            Add<Vignette>(profile, v => { v.active = true; v.intensity.Override(0.26f);
                v.smoothness.Override(0.5f); v.color.Override(new Color(0.12f, 0.10f, 0.08f)); });

            Add<FilmGrain>(profile, f => { f.active = true; f.intensity.Override(0.13f);
                f.response.Override(0.8f); });

            EditorUtility.SetDirty(profile);

            var existing = Object.FindFirstObjectByType<Volume>();
            var go = existing != null ? existing.gameObject : new GameObject("PostVolume");
            go.transform.SetParent(parent, false);
            var volume = existing != null ? existing : go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        static void Add<T>(VolumeProfile profile, System.Action<T> configure) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component)) component = profile.Add<T>(true);
            configure(component);
        }
    }
}
