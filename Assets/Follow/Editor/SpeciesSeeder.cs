using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Follow.Data;

namespace Follow.EditorTools
{
    /// <summary>
    /// Seeds the species assets. Every entry is backed by a model that exists in the
    /// project: the Quaternius set is temperate farm fauna, so several are honest reskins
    /// - a bull reads as a mithun, a wolf as a dhole - which holds up at low-poly and is
    /// true to what actually lives in these hills.
    /// </summary>
    public static class SpeciesSeeder
    {
        const string Res = "Assets/Follow/Resources";
        const string Dir = "Assets/Follow/Data/Species";
        const string Animals = "Assets/QuaterniusAnimals/FBX-20260827T061509Z-1-001/FBX/";
        const string Birds = "Assets/Birds/";
        const string Plants = "Assets/Stylized Nature MegaKit[Standard]/FBX (Unity)/";
        const string Controllers = "Assets/Follow/Animation/Animals";

        struct Seed
        {
            public string id, common, sci, habitat, diet, note, model;
            public float scale;
            /// <summary>What a photographable specimen is coloured, so it stands out.</summary>
            public Color tint;
            public SpeciesKind kind;
            public ShotType shot;
            public int day;
            public float rarity, wariness;
        }

        static readonly Seed[] All =
        {
            new Seed { id="mithun", scale=0.297f, common="Mithun", sci="Bos frontalis",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=1, rarity=0.20f, wariness=9f,
                model=Animals+"Bull.fbx",
                habitat="Forest clearings and hill slopes; semi-domesticated across Nagaland.",
                diet="Grasses, bamboo leaves, forest browse.",
                note="The state animal, and entirely unbothered by either of us. It watched me set up the shot." },

            new Seed { id="barking_deer", scale=0.189f, common="Barking Deer", sci="Muntiacus muntjak",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=1, rarity=0.30f, wariness=14f,
                model=Animals+"Deer.fbx",
                habitat="Thick forest edge and secondary growth near water.",
                diet="Fruit, shoots, tender leaves, occasionally eggs.",
                note="The call really does sound like a dog. She answered it once, then looked embarrassed." },

            new Seed { id="sambar", scale=0.273f, common="Sambar", sci="Rusa unicolor",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=2, rarity=0.45f, wariness=18f,
                model=Animals+"Stag.fbx",
                habitat="Dense hill forest, feeding at the margins near dusk.",
                diet="Grass, foliage, fallen fruit and bark.",
                note="Enormous, and completely silent until it decides not to be." },

            new Seed { id="red_fox", scale=0.185f, common="Red Fox", sci="Vulpes vulpes",
                kind=SpeciesKind.Fauna, shot=ShotType.HoldStill, day=3, rarity=0.60f, wariness=20f,
                model=Animals+"Fox.fbx",
                habitat="Open slopes, scrub and forest edge.",
                diet="Rodents, birds, insects, fruit.",
                note="She wanted to follow it. To her enormous credit, she did not." },

            new Seed { id="dhole", scale=0.235f, common="Dhole", sci="Cuon alpinus",
                kind=SpeciesKind.Fauna, shot=ShotType.HoldStill, day=5, rarity=0.85f, wariness=26f,
                model=Animals+"Wolf.fbx",
                habitat="Dense forest and hill country; lives and hunts in packs.",
                diet="Chiefly deer; also hares and rodents.",
                note="Endangered, and the reason this survey is funded at all. Three of them, gone in seconds." },

            new Seed { id="emerald_dove", scale=0.5f, common="Emerald Dove", sci="Chalcophaps indica",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=1, rarity=0.25f, wariness=12f,
                model=Birds+"low_poly_pigeon.glb",
                habitat="Shaded forest floor and lower storey; flushes fast and low.",
                diet="Fallen seeds, small fruit, termites.",
                note="Green as wet moss in the right light. Almost impossible to see until it moves." },

            new Seed { id="serpent_eagle", scale=0.6f, common="Crested Serpent Eagle", sci="Spilornis cheela",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=3, rarity=0.55f, wariness=22f,
                model=Birds+"low_poly_eagle.glb",
                habitat="Perches on exposed branches at the forest edge; soars mid-morning.",
                diet="Snakes, lizards, small mammals.",
                note="Calls all afternoon from somewhere you cannot find, then lands where you were standing." },

            new Seed { id="hawk_eagle", scale=0.35f, common="Mountain Hawk-Eagle", sci="Nisaetus nipalensis",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=7, rarity=0.90f, wariness=28f,
                model=Birds+"harpia-animated-low-poly/source/Harpia.fbx",
                habitat="Broadleaf forest on steep hill country, hunting below the canopy.",
                diet="Pheasants, hares, squirrels.",
                note="Crested, and far bigger than the photographs prepare you for." },

            // Flora is dressed from the nature kits rather than a single model, so these
            // carry no prefab: the level places the matching bush or tree.
            new Seed { id="rhododendron", scale=1.15f, common="Rhododendron", sci="Rhododendron arboreum",
                tint=new Color(1.00f, 0.34f, 0.36f),
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=1, rarity=0.20f, wariness=0f,
                model=Plants+"Bush_Common_Flowers.fbx",
                habitat="Temperate hill slopes above 1500 m, often in dense stands.", diet="",
                note="The whole ridge goes red in season. From a distance you would swear the hillside was burning." },

            new Seed { id="tree_fern", scale=1.6f, common="Tree Fern", sci="Cyathea gigantea",
                tint=new Color(0.42f, 0.86f, 0.50f),
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=1, rarity=0.30f, wariness=0f,
                model=Plants+"Fern_1.fbx",
                habitat="Damp shaded gullies and streamsides.", diet="",
                note="Older than the trees around it, as a lineage. It looks it, too." },

            new Seed { id="bamboo", scale=1.9f, common="Hill Bamboo", sci="Dendrocalamus hamiltonii",
                tint=new Color(0.83f, 0.93f, 0.42f),
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=2, rarity=0.15f, wariness=0f,
                model=Plants+"Plant_7_Big.fbx",
                habitat="Hill slopes and village margins; forms dense groves.", diet="",
                note="Everything up here is made of this. Houses, baskets, the bridge we crossed on day two." },

            new Seed { id="blue_vanda", scale=1.5f, common="Blue Vanda", sci="Vanda coerulea",
                tint=new Color(0.62f, 0.55f, 1.00f),
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=4, rarity=0.70f, wariness=0f,
                model=Plants+"Flower_3_Group.fbx",
                habitat="Epiphytic on oak and other broadleaf trees in humid hill forest.", diet="",
                note="A protected orchid, and the colour is genuinely difficult to believe until you see one." },
        };

        /// <summary>
        /// One animator per animal, built from the clips already inside its own FBX.
        ///
        /// Without this every animal placed in the world imports as a T-pose, which is
        /// both the least convincing thing a wild animal can do and the first thing a
        /// player photographs. The controller has no transitions: one state per clip, and
        /// whichever reads as idle is the default.
        /// </summary>
        static RuntimeAnimatorController BuildController(string id, string modelPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview"))
                .ToList();
            if (clips.Count == 0) return null;

            if (!AssetDatabase.IsValidFolder("Assets/Follow/Animation"))
                AssetDatabase.CreateFolder("Assets/Follow", "Animation");
            if (!AssetDatabase.IsValidFolder(Controllers))
                AssetDatabase.CreateFolder("Assets/Follow/Animation", "Animals");

            string path = Controllers + "/" + id + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            AnimatorState fallback = null;
            AnimatorState idle = null;

            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name);
                state.motion = clip;
                if (fallback == null) fallback = state;

                string lower = clip.name.ToLowerInvariant();
                // "Jump_ToIdle" ends in idle and would spawn the animal mid-landing.
                if (idle == null && lower.Contains("idle") && !lower.Contains("jump"))
                    idle = state;
            }

            machine.defaultState = idle ?? fallback;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        [MenuItem("Follow/Reseed Species", priority = 11)]
        public static SpeciesLibrary Reseed()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Follow/Data"))
                    AssetDatabase.CreateFolder("Assets/Follow", "Data");
                AssetDatabase.CreateFolder("Assets/Follow/Data", "Species");
            }

            string libPath = Res + "/SpeciesLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<SpeciesLibrary>(libPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SpeciesLibrary>();
                AssetDatabase.CreateAsset(library, libPath);
            }

            // Drop entries that no longer exist so a rename never leaves a ghost behind.
            foreach (var guid in AssetDatabase.FindAssets("t:SpeciesData", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string id = System.IO.Path.GetFileNameWithoutExtension(path).Replace("SP_", "");
                bool keep = false;
                foreach (var s in All) if (s.id == id) { keep = true; break; }
                if (!keep) AssetDatabase.DeleteAsset(path);
            }

            library.species = new List<SpeciesData>();
            int missing = 0;

            foreach (var seed in All)
            {
                string path = Dir + "/SP_" + seed.id + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<SpeciesData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<SpeciesData>();
                    AssetDatabase.CreateAsset(data, path);
                }

                data.id = seed.id;
                data.commonName = seed.common;
                data.scientificName = seed.sci;
                data.kind = seed.kind;
                data.shotType = seed.shot;
                data.habitat = seed.habitat;
                data.diet = seed.diet;
                data.fieldNote = seed.note;
                data.firstAppearsOnDay = seed.day;
                data.rarity = seed.rarity;
                data.wariness = seed.wariness;
                data.worldScale = seed.scale > 0f ? seed.scale : 0.2f;
                data.tint = seed.tint.a > 0f ? seed.tint : Color.white;

                if (!string.IsNullOrEmpty(seed.model))
                {
                    data.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(seed.model);
                    if (data.modelPrefab == null)
                    {
                        Debug.LogWarning("Species '" + seed.id + "': model not found at " + seed.model);
                        missing++;
                    }
                    else if (seed.kind == SpeciesKind.Fauna)
                    {
                        data.animator = BuildController(seed.id, seed.model);
                    }
                }

                EditorUtility.SetDirty(data);
                library.species.Add(data);
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Species: seeded " + All.Length + " (" + missing + " missing models).");
            return library;
        }
    }
}
