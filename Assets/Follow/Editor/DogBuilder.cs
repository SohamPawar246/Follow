using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Follow.Dog;
using Comp = Follow.World.WorldComposer;

namespace Follow.EditorTools
{
    /// <summary>
    /// Spawns the Shiba, wires its animator from the clips already inside the FBX, and
    /// seeds the forest with the scent points that make it necessary.
    /// </summary>
    public static class DogBuilder
    {
        const string Shiba = "Assets/QuaterniusAnimals/FBX-20260827T061509Z-1-001/FBX/ShibaInu.fbx";
        const string Root = "Assets/Follow";

        [MenuItem("Follow/Spawn The Dog", priority = 21)]
        public static void Spawn()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager
                .OpenScene("Assets/Follow/Scenes/Game.unity",
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

            var existing = GameObject.Find("Dog");
            if (existing != null) Object.DestroyImmediate(existing);

            var controller = BuildController();
            var dog = BuildDog(controller);
            if (dog != null && dog.GetComponent<Follow.World.GroundGuard>() == null)
                dog.AddComponent<Follow.World.GroundGuard>();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Dog spawned at " + dog.transform.position + " with "
                      + (controller != null ? controller.layers[0].stateMachine.states.Length : 0)
                      + " animation states. Subjects are placed by ScentField while you play.");
        }

        // --- animation ------------------------------------------------------------------

        /// <summary>
        /// One state per clip, no transitions. DogBody cross-fades between them directly,
        /// so the brain stays the only authority on what the dog is doing.
        /// </summary>
        static AnimatorController BuildController()
        {
            FollowBuildUtils.EnsureFolder(Root + "/Animation");
            string path = Root + "/Animation/DogController.controller";

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            var clips = AssetDatabase.LoadAllAssetsAtPath(Shiba)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview"))
                .ToList();

            if (clips.Count == 0)
            {
                Debug.LogError("No animation clips found on " + Shiba);
                return controller;
            }

            AnimatorState first = null;
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name);
                state.motion = clip;
                // Everything except the one-shots should loop; the FBX already marks them,
                // but the state needs its speed set explicitly or fast clips read as jitter.
                state.speed = 1f;
                if (first == null) first = state;
                // Exact match: EndsWith("Idle") also catches Jump_ToIdle, which would
                // spawn the dog mid-landing.
                if (clip.name.EndsWith("|Idle")) first = state;
            }

            if (first != null) machine.defaultState = first;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // --- the dog --------------------------------------------------------------------

        static GameObject BuildDog(AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Shiba);
            if (source == null) { Debug.LogError("ShibaInu.fbx not found"); return null; }

            var root = new GameObject("Dog");
            root.layer = FollowBuildUtils.Layer("Animal");

            // Start beside the player so day one begins with it already present.
            var player = GameObject.Find("Player");
            Vector3 at = player != null
                ? player.transform.position + player.transform.right * 2.4f
                : Vector3.zero;
            at.y = Comp.Height(at.x, at.z) + 0.1f;
            root.transform.position = at;

            var cc = root.AddComponent<CharacterController>();
            cc.height = 0.8f;
            cc.radius = 0.26f;
            cc.center = new Vector3(0f, 0.42f, 0f);
            cc.slopeLimit = 55f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.02f;

            // A pivot between the controller and the mesh, so bob and bank never fight
            // the CharacterController's own transform.
            var bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(root.transform, false);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, bodyRoot);
            model.name = "ShibaInu";
            model.transform.localPosition = Vector3.zero;
            // Measured: the FBX is 3.60 units tall. Life size put the Shiba at 0.65 m,
            // which was true and unreadable - from this camera she was a smudge. At 0.95 m
            // she is a large dog rather than a small one, and you can see what she is doing.
            model.transform.localScale = Vector3.one * 0.264f;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            root.AddComponent<DogBrain>();

            var body = root.AddComponent<DogBody>();
            body.animator = animator;
            body.bodyRoot = bodyRoot;
            body.head = FindBone(model.transform, "head", "neck", "spine");

            var audio = root.AddComponent<DogAudio>();
            BindAudio(audio);

            return root;
        }

        /// <summary>Finds the best-matching bone by name fragment, deepest match wins.</summary>
        static Transform FindBone(Transform root, params string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                Transform best = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.ToLowerInvariant().Contains(fragment)) { best = t; break; }
                }
                if (best != null) return best;
            }
            return null;
        }

        static void BindAudio(DogAudio audio)
        {
            // No dog vocals in the packs yet, so footsteps stand in and the bark stays
            // empty rather than playing something that reads as wrong.
            var sounds = AssetDatabase.LoadAssetAtPath<Follow.UI.CozySounds>(
                "Assets/Follow/Resources/CozySounds.asset");
            if (sounds != null && sounds.footsteps != null && sounds.footsteps.Length > 0)
                audio.footsteps = sounds.footsteps;

            var barks = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Follow/Audio/Dog" }))
                barks.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            audio.barks = barks.ToArray();

            if (audio.barks.Length == 0)
                Debug.LogWarning("Dog has no bark clips - the bark is how a find is communicated.");
        }

    }
}
