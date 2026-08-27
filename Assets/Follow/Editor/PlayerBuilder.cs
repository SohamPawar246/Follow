using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Follow.Game;
using Comp = Follow.World.WorldComposer;

namespace Follow.EditorTools
{
    /// <summary>
    /// Replaces the placeholder capsule with the KayKit Ranger, wired to the shared
    /// Rig_Medium clips. Scale comes from the measured bounds: the model imports at 2.45
    /// units tall, so it needs 0.735 to stand 1.8 m.
    /// </summary>
    public static class PlayerBuilder
    {
        const string Ranger = "Assets/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Ranger.fbx";
        const string Root = "Assets/Follow";

        static readonly string[] AnimationFiles =
        {
            "Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx",
            "Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx",
            "Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_Tools.fbx",
        };

        /// <summary>
        /// Taller than a real surveyor on purpose. At this camera angle a person at true
        /// scale is a thumbnail; two metres is what makes the character legible without
        /// making the forest feel like a model village.
        /// </summary>
        const float TargetHeight = 2.05f;

        [MenuItem("Follow/Build The Player", priority = 22)]
        public static void Build()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager
                .OpenScene("Assets/Follow/Scenes/Game.unity",
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("No Player in scene"); return; }

            // Strip the placeholder capsule and any previous rig.
            foreach (Transform child in player.transform.Cast<Transform>().ToList())
                Object.DestroyImmediate(child.gameObject);

            var controller = BuildController();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Ranger);
            if (source == null) { Debug.LogError("Ranger.fbx not found"); return; }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, player.transform);
            model.name = "Ranger";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            float scale = ScaleFor(source, TargetHeight);
            model.transform.localScale = Vector3.one * scale;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var body = player.GetComponent<PlayerBody>();
            if (body == null) body = player.AddComponent<PlayerBody>();
            body.animator = animator;
            body.idle = PickClip(controller, "Idle", "Idle_A", "Idle_B");
            body.walk = PickClip(controller, "Walking_A", "Walking_B", "Walk");
            body.run = PickClip(controller, "Running_A", "Running_B", "Run");

            // The capsule matches the model now, not the old placeholder.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = TargetHeight;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Player rebuilt as Ranger. scale=" + scale.ToString("0.000")
                      + "  idle=" + body.idle + "  walk=" + body.walk + "  run=" + body.run);
        }

        static float ScaleFor(GameObject source, float targetHeight)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in source.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            if (first || bounds.size.y < 0.001f) return 1f;
            return targetHeight / bounds.size.y;
        }

        /// <summary>First clip name that exists in the controller, so a rename cannot break it.</summary>
        static string PickClip(AnimatorController controller, params string[] candidates)
        {
            var names = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            foreach (var c in candidates)
                if (names.Contains(c)) return c;
            return names.FirstOrDefault() ?? "Idle";
        }

        static AnimatorController BuildController()
        {
            FollowBuildUtils.EnsureFolder(Root + "/Animation");
            string path = Root + "/Animation/PlayerController.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            var seen = new HashSet<string>();
            AnimatorState idleState = null;

            foreach (var file in AnimationFiles)
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview")) continue;
                    if (clip.name.Contains("T-Pose")) continue;
                    if (!seen.Add(clip.name)) continue;

                    var state = machine.AddState(clip.name);
                    state.motion = clip;
                    if (clip.name == "Idle" || (idleState == null && clip.name.StartsWith("Idle")))
                        idleState = state;
                }
            }

            if (idleState != null) machine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

    }
}
