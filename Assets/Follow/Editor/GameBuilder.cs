using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Follow.Core;
using Follow.Data;
using Follow.Game;
using Follow.UI;

namespace Follow.EditorTools
{
    /// <summary>
    /// Builds the whole project skeleton from scratch: theme, species data, and the four
    /// scenes. Re-runnable, because the greybox will be rebuilt many times before the
    /// real art lands.
    /// </summary>
    public static class GameBuilder
    {
        const string Root = "Assets/Follow";
        const string Res = Root + "/Resources";
        const string Scenes = Root + "/Scenes";
        const string SpeciesDir = Root + "/Data/Species";

        [MenuItem("Follow/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            EnsureFolders();
            EnsureTmp();
            var theme = EnsureTheme();
            var library = EnsureSpecies();

            BuildBootScene();
            BuildMenuScene();
            BuildStoryScene();
            BuildGameScene();
            RegisterScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Follow: built theme, " + library.species.Count + " species, and 4 scenes. Open " + Scenes + "/Boot.unity and press play.");
        }

        // --- assets -------------------------------------------------------------

        static void EnsureFolders()
        {
            foreach (var path in new[] { Res, Scenes, SpeciesDir, Root + "/Materials" })
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                string parent = Path.GetDirectoryName(path).Replace((char)92, '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace((char)92, '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        /// <summary>TMP needs its essential resources imported once or every label renders blank.</summary>
        static void EnsureTmp()
        {
            if (TMPro.TMP_Settings.instance != null) return;
            Debug.Log("Follow: importing TMP essentials...");
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
        }

        static CozyTheme EnsureTheme()
        {
            string path = Res + "/CozyTheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<CozyTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<CozyTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            CozyTheme.Active = theme;
            return theme;
        }

        // --- species ------------------------------------------------------------

        static SpeciesLibrary EnsureSpecies() => SpeciesSeeder.Reseed();

        // --- scenes -------------------------------------------------------------

        static void Save(UnityEngine.SceneManagement.Scene scene, string name)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Scenes + "/" + name + ".unity");
        }

        static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Boot", typeof(Boot));
            var camGo = new GameObject("Camera", typeof(Camera));
            camGo.GetComponent<Camera>().backgroundColor = new Color(0.07f, 0.06f, 0.05f);
            camGo.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camGo.tag = "MainCamera";
            Save(scene, "Boot");
        }

        static Material Mat(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Light MakeSun(Quaternion rotation)
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = rotation;
            RenderSettings.sun = light;
            return light;
        }

        static void Atmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.47f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.75f, 0.78f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 150f;
        }

        /// <summary>A placeholder ground plane. The real forest arrives with the asset packs.</summary>
        static GameObject Ground(float size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground_PLACEHOLDER";
            go.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat("M_Ground", color);
            return go;
        }

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere();
            MakeSun(Quaternion.Euler(26f, 140f, 0f));
            Ground(60f, new Color(0.38f, 0.44f, 0.30f));

            // The vignette the menu camera drifts around. Dressed properly once packs land.
            var focus = new GameObject("VignetteFocus").transform;
            focus.position = new Vector3(0f, 0f, 0f);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.farClipPlane = 300f;
            var orbit = camGo.AddComponent<MenuCameraOrbit>();
            orbit.focus = focus;

            new GameObject("MainMenuUI", typeof(MainMenuUI));
            Save(scene, "MainMenu");
        }

        static void BuildStoryScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.08f, 0.07f);
            new GameObject("StoryUI", typeof(StoryUI));
            Save(scene, "Story");
        }

        static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere();
            var sun = MakeSun(Quaternion.Euler(35f, 40f, 0f));
            Ground(120f, new Color(0.36f, 0.42f, 0.28f));

            // Player
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.88f, 0f);
            player.AddComponent<PlayerMover>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body_PLACEHOLDER";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.86f, 0.6f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Mat("M_Player", new Color(0.85f, 0.72f, 0.55f));
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing_PLACEHOLDER";
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.1f, 0.36f);
            nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = Mat("M_Accent", new Color(0.78f, 0.42f, 0.28f));
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            // Camp anchor: where the fire, the table and the album will live.
            var camp = new GameObject("Camp");
            camp.transform.position = new Vector3(0f, 0f, -6f);

            // Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.farClipPlane = 300f;
            var topDown = camGo.AddComponent<TopDownCamera>();
            topDown.target = player.transform;

            // Day cycle, with curves that read as dawn through dusk.
            var cycleGo = new GameObject("DayCycle");
            var cycle = cycleGo.AddComponent<DayCycle>();
            cycle.sun = sun;
            cycle.sunColor = Ramp(
                (0.00f, new Color(0.45f, 0.50f, 0.72f)),
                (0.20f, new Color(1.00f, 0.82f, 0.58f)),
                (0.50f, new Color(1.00f, 0.97f, 0.90f)),
                (0.72f, new Color(1.00f, 0.72f, 0.42f)),
                (1.00f, new Color(0.30f, 0.34f, 0.55f)));
            cycle.ambientColor = Ramp(
                (0.00f, new Color(0.20f, 0.23f, 0.32f)),
                (0.30f, new Color(0.52f, 0.55f, 0.55f)),
                (0.72f, new Color(0.48f, 0.42f, 0.38f)),
                (1.00f, new Color(0.14f, 0.16f, 0.24f)));
            cycle.fogColor = Ramp(
                (0.00f, new Color(0.32f, 0.36f, 0.46f)),
                (0.25f, new Color(0.78f, 0.80f, 0.78f)),
                (0.72f, new Color(0.85f, 0.66f, 0.48f)),
                (1.00f, new Color(0.16f, 0.18f, 0.26f)));
            cycle.sunIntensity = new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(0.25f, 1.4f),
                new Keyframe(0.72f, 1.1f), new Keyframe(0.95f, 0.05f), new Keyframe(1f, 0.02f));

            new GameObject("GameHud", typeof(GameHud));

            Save(scene, "Game");
        }

        static Gradient Ramp(params (float t, Color c)[] stops)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[stops.Length];
            for (int i = 0; i < stops.Length; i++) keys[i] = new GradientColorKey(stops[i].c, stops[i].t);
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static void RegisterScenes()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in new[] { "Boot", "MainMenu", "Story", "Game" })
                list.Add(new EditorBuildSettingsScene(Scenes + "/" + name + ".unity", true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
