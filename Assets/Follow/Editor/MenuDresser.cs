using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Follow.UI;
using Follow.World;

namespace Follow.EditorTools
{
    /// <summary>
    /// Dresses the menu scene with a real corner of the forest.
    ///
    /// The menu was a flat green plane under fog, which is the one screen every player
    /// sees before they have any reason to be generous about it. This plants a camp
    /// clearing off to the right - the side the buttons leave empty - lights it at the
    /// end of the afternoon, and lets the existing camera drift around it.
    ///
    /// Separate from <c>Follow/Build Everything</c> on purpose: that rebuilds every scene
    /// from scratch, including the game, and this is the only one that needs the work.
    /// </summary>
    public static class MenuDresser
    {
        const string ScenePath = "Assets/Follow/Scenes/MainMenu.unity";

        [MenuItem("Follow/Dress The Menu", priority = 20)]
        public static void Dress()
        {
            var palette = Resources.Load<WorldPalette>("WorldPalette");
            if (palette == null)
            {
                Debug.LogError("No WorldPalette. Run Follow/Build The World first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var old = GameObject.Find("Vignette");
            if (old != null) Object.DestroyImmediate(old);
            var flat = GameObject.Find("Ground_PLACEHOLDER");
            if (flat != null) Object.DestroyImmediate(flat);

            var root = new GameObject("Vignette").transform;

            Ground(root, palette);
            Trees(root, palette);
            Undergrowth(root, palette);
            Camp(root, palette);
            Fireflies(root);

            Lighting();
            CameraRig();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Menu dressed: " + root.childCount + " groups of scenery.");
        }

        // --- the ground ------------------------------------------------------------

        /// <summary>
        /// A real piece of the height field rather than a plane, so the clearing rolls the
        /// way the game's ground does and the same material lights it the same way.
        /// </summary>
        static void Ground(Transform parent, WorldPalette palette)
        {
            const int quads = 40;
            const float size = 120f;
            var origin = new Vector2(240f, 190f);   // a quiet spot, well away from camp

            var vertices = new Vector3[(quads + 1) * (quads + 1)];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[quads * quads * 6];

            for (int z = 0, i = 0; z <= quads; z++)
            for (int x = 0; x <= quads; x++, i++)
            {
                float fx = (x / (float)quads - 0.5f) * size;
                float fz = (z / (float)quads - 0.5f) * size;
                float wx = origin.x + fx;
                float wz = origin.y + fz;

                vertices[i] = new Vector3(fx, WorldComposer.Height(wx, wz), fz);
                uvs[i] = new Vector2(x / (float)quads, z / (float)quads);

                float lush = Mathf.PerlinNoise(wx * 0.03f, wz * 0.03f);
                colors[i] = Color.Lerp(new Color(0.42f, 0.52f, 0.30f),
                                       new Color(0.52f, 0.60f, 0.34f), lush);
            }

            for (int z = 0, t = 0; z < quads; z++)
            for (int x = 0; x < quads; x++)
            {
                int a = z * (quads + 1) + x;
                triangles[t++] = a;
                triangles[t++] = a + quads + 1;
                triangles[t++] = a + 1;
                triangles[t++] = a + 1;
                triangles[t++] = a + quads + 1;
                triangles[t++] = a + quads + 2;
            }

            var mesh = new Mesh { name = "MenuGround" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, "Assets/Follow/Settings/MenuGround.asset");

            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = palette.groundMaterial;
        }

        // --- planting ------------------------------------------------------------

        /// <summary>
        /// A dense wall of canopy across the back and down the left, thinning to open
        /// ground where the camp sits. The left is where the buttons go, so the trees
        /// there are only ever silhouette behind them.
        /// </summary>
        static void Trees(Transform parent, WorldPalette palette)
        {
            var canopy = palette.Canopy.SelectMany(l => l.prefabs).Where(p => p != null).ToList();
            if (canopy.Count == 0) return;

            var group = new GameObject("Trees").transform;
            group.SetParent(parent, false);

            var rng = new System.Random(70142);

            for (int i = 0; i < 46; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = 16f + (float)rng.NextDouble() * 26f;
                var at = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Keep the middle clear so the camp has somewhere to be.
                if (at.magnitude < 15f) continue;

                Plant(group, canopy[rng.Next(canopy.Count)], at,
                    Mathf.Lerp(6.5f, 11f, (float)rng.NextDouble()), rng);
            }
        }

        static void Undergrowth(Transform parent, WorldPalette palette)
        {
            var detail = palette.Detail
                .Where(l => l.rule != ScatterRule.Firewood && l.rule != ScatterRule.Forage)
                .SelectMany(l => l.prefabs).Where(p => p != null).ToList();
            if (detail.Count == 0) return;

            var group = new GameObject("Undergrowth").transform;
            group.SetParent(parent, false);

            var rng = new System.Random(3391);

            for (int i = 0; i < 190; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = 7f + (float)rng.NextDouble() * 26f;
                var at = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                Plant(group, detail[rng.Next(detail.Count)], at,
                    Mathf.Lerp(0.45f, 1.5f, (float)rng.NextDouble()), rng);
            }
        }

        /// <summary>The camp itself: the fire lit, the tent beside it, wood stacked.</summary>
        static void Camp(Transform parent, WorldPalette palette)
        {
            var group = new GameObject("Camp").transform;
            group.SetParent(parent, false);

            var rng = new System.Random(11);

            // Half again as big as they are in the game. This is a poster, not a
            // simulation - at fifteen metres through a thirty-four degree lens the honest
            // sizes read as gravel.
            // The Kenney kit ships geometry and no materials at all, so every one of
            // these renders as a flat magenta block until it is given one. The world
            // builder paints its copies the same way; these are separate instances and
            // need the same treatment.
            Paint(Plant(group, palette.campfireStones, Vector3.zero, 3.2f, rng),
                new Color(0.52f, 0.50f, 0.47f), "M_MenuStone");
            Paint(Plant(group, palette.campfireLogs, Vector3.zero, 2.5f, rng),
                new Color(0.44f, 0.30f, 0.20f), "M_MenuWood");
            Paint(Plant(group, palette.tent, new Vector3(4.4f, 0f, 2.6f), 2.9f, rng),
                new Color(0.80f, 0.72f, 0.55f), "M_MenuCanvas");
            Paint(Plant(group, palette.logStack, new Vector3(-1.4f, 0f, 3.0f), 2.0f, rng),
                new Color(0.46f, 0.32f, 0.21f), "M_MenuWood");
            Paint(Plant(group, palette.stump, new Vector3(2.8f, 0f, -2.4f), 1.7f, rng),
                new Color(0.50f, 0.36f, 0.24f), "M_MenuWood");

            // The fire itself. This is the warm point the whole composition hangs off.
            var fire = new GameObject("Firelight");
            fire.transform.SetParent(group, false);
            fire.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var light = fire.AddComponent<Light>();
            light.type = LightType.Point;
            // Warm, not blinding. At fourteen it bleached the fire ring to white paper,
            // which is the opposite of what a fire is meant to do to the things near it.
            light.color = new Color(1f, 0.66f, 0.30f);
            light.intensity = 4.5f;
            light.range = 13f;
            light.shadows = LightShadows.None;

            var flame = new GameObject("Flame");
            flame.transform.SetParent(group, false);
            flame.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            var ps = flame.AddComponent<ParticleSystem>();
            // Dressed at load, not here. A baked scene cannot hold ParticleArt's
            // materials - they are HideAndDontSave - so a reference written now is
            // magenta by the time anybody opens the scene.
            flame.AddComponent<ParticleDress>().additive = true;

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.82f, 0.32f), new Color(1f, 0.48f, 0.16f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = -0.06f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.5f;

            // Tapers and fades on the way up, which is most of what makes it a flame
            // rather than a column of dots.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Motes drifting through the firelight. Cheap, and it stops the frame dead still.</summary>
        static void Fireflies(Transform parent)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            go.AddComponent<ParticleDress>().additive = true;

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.88f, 0.55f), new Color(1f, 0.96f, 0.78f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.01f;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 6f, 26f);
        }

        static GameObject Plant(Transform parent, GameObject prefab, Vector3 at, float metres,
            System.Random rng)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (go == null) return null;

            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            // Normalise, exactly as the world does, so a number here means metres.
            var bounds = Measure(go);
            float largest = Mathf.Max(0.01f,
                Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));

            go.transform.localScale = Vector3.one * (metres / largest);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            var world = at;
            world.y = WorldComposer.Height(240f + at.x, 190f + at.z)
                    - WorldComposer.Height(240f, 190f);
            go.transform.localPosition = world;

            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>Gives a material-less kit model something to render with.</summary>
        static void Paint(GameObject instance, Color color, string materialName)
        {
            if (instance == null) return;

            string path = "Assets/Follow/Materials/" + materialName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
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

        static Bounds Measure(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.one);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        // --- light and camera ---------------------------------------------------------

        /// <summary>Late afternoon, low and warm, with the fog pulled in behind the trees.</summary>
        static void Lighting()
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);

            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(16f, 128f, 0f);
            sun.color = new Color(1f, 0.79f, 0.55f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.24f, 0.20f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.66f, 0.58f);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 96f;
        }

        /// <summary>
        /// Framed so the camp sits right of centre. The buttons own the left third, and a
        /// menu whose art is behind its own text is a menu with no art.
        /// </summary>
        static void CameraRig()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var focus = GameObject.Find("VignetteFocus");
            if (focus == null) focus = new GameObject("VignetteFocus");

            cam.fieldOfView = 36f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var orbit = cam.GetComponent<MenuCameraOrbit>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<MenuCameraOrbit>();
            orbit.focus = focus.transform;
            orbit.radius = 13f;
            orbit.height = 4.4f;
            orbit.degreesPerSecond = 1.1f;   // slow enough that it reads as air, not motion
            orbit.bobHeight = 0.3f;
            orbit.bobSeconds = 11f;
            orbit.startAngle = 128f;

            // Aim off to one side of the fire rather than straight at it.
            //
            // The buttons own the left half of the screen, so anything centred is behind
            // them. Offsetting the aim point along the camera's own right axis slides the
            // whole camp into the right of frame, and it is worth computing rather than
            // hand-tuning because it stays correct if the radius or the angle change.
            var campAt = new Vector3(0f, 1.4f, 0f);
            float a = orbit.startAngle * Mathf.Deg2Rad;
            Vector3 from = campAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * orbit.radius
                         + Vector3.up * orbit.height;
            Vector3 forward = (campAt - from).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            focus.transform.position = campAt - right * 3.8f;

            cam.transform.position = focus.transform.position
                + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * orbit.radius
                + Vector3.up * orbit.height;
            cam.transform.LookAt(focus.transform.position);

            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(orbit);
        }
    }
}
