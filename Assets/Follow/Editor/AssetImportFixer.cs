using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Follow.EditorTools
{
    /// <summary>
    /// Import-time fixes that cannot be done from the scene.
    ///
    /// The big one is looping: Quaternius ships its clips unlooped, so a walk cycle plays
    /// once and then holds on its final frame while the model keeps sliding forward. That
    /// is a ModelImporter setting, not something a state machine can paper over.
    /// </summary>
    public static class AssetImportFixer
    {
        const string Animals = "Assets/QuaterniusAnimals/FBX-20260827T061509Z-1-001/FBX";
        const string Birds = "Assets/Birds";
        const string Characters = "Assets/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx";
        const string AnimsA = "Assets/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Animations/fbx/Rig_Medium";
        const string AnimsB = "Assets/KayKit_Character_Animations_1.1/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium";
        const string RangerPath = Characters + "/Ranger.fbx";

        /// <summary>Clips that must cycle. Anything not matching here is a one-shot.</summary>
        static readonly string[] LoopingFragments =
        {
            "idle", "walk", "run", "gallop", "trot", "eat", "fly", "flap", "glide",
            "swim", "sit", "sleep", "graze", "hover", "wander"
        };

        static bool ShouldLoop(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (n.Contains("jump_to") || n.Contains("death") || n.Contains("hitreact")
                || n.Contains("attack") || n.Contains("land") || n.Contains("start")) return false;
            return LoopingFragments.Any(f => n.Contains(f));
        }

        [MenuItem("Follow/Fix Asset Imports", priority = 5)]
        public static void FixAll()
        {
            var report = new StringBuilder();
            int looped = 0, models = 0;

            foreach (var path in ModelPaths())
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool changed = false;
                foreach (var clip in clips)
                {
                    bool want = ShouldLoop(clip.name);
                    if (clip.loopTime == want) continue;
                    clip.loopTime = want;
                    // Matching pose removes the hitch where the last frame snaps to the first.
                    clip.loopPose = want;
                    changed = true;
                    if (want) looped++;
                }

                if (!changed) continue;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                models++;
            }

            report.AppendLine("Looped " + looped + " clips across " + models + " models.");
            report.AppendLine(SetupCharacterRig());
            report.AppendLine();
            report.AppendLine(MeasureModels());

            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        static IEnumerable<string> ModelPaths()
        {
            foreach (var folder in new[] { Animals, Birds, Characters, AnimsA, AnimsB })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
                    yield return AssetDatabase.GUIDToAssetPath(guid);
            }
        }

        /// <summary>
        /// KayKit ships the character mesh and the animation clips in separate files that
        /// share one skeleton. The clips only bind to the mesh if every animation file
        /// copies the character's avatar, so this points them all at Ranger.
        /// </summary>
        static string SetupCharacterRig()
        {
            var rangerImporter = AssetImporter.GetAtPath(RangerPath) as ModelImporter;
            if (rangerImporter == null) return "Ranger.fbx not found - character rig not configured.";

            rangerImporter.animationType = ModelImporterAnimationType.Generic;
            rangerImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            rangerImporter.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(RangerPath)
                .OfType<Avatar>().FirstOrDefault();
            if (avatar == null) return "Could not create an Avatar from Ranger.fbx.";

            int bound = 0;
            foreach (var folder in new[] { AnimsA, AnimsB })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (importer == null) continue;

                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    importer.SaveAndReimport();
                    bound++;
                }
            }
            return "Ranger avatar created and copied onto " + bound + " animation files.";
        }

        /// <summary>
        /// Reports each model's real height so scaling is set from measurements rather
        /// than guesswork. A mithun and a dove should not import at the same size.
        /// </summary>
        static string MeasureModels()
        {
            var sb = new StringBuilder("Measured heights (unscaled):");
            foreach (var folder in new[] { Animals, Birds, Characters })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null) continue;

                    var bounds = new Bounds(Vector3.zero, Vector3.zero);
                    bool first = true;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (first) { bounds = r.bounds; first = false; }
                        else bounds.Encapsulate(r.bounds);
                    }
                    if (first) continue;

                    sb.AppendLine("  " + System.IO.Path.GetFileNameWithoutExtension(p).PadRight(18)
                                  + " h=" + bounds.size.y.ToString("0.00")
                                  + "  l=" + bounds.size.z.ToString("0.00"));
                }
            }
            return sb.ToString();
        }
    }
}
