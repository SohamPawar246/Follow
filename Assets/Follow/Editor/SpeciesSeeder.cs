using System.Collections.Generic;
using UnityEditor;
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

        struct Seed
        {
            public string id, common, sci, habitat, diet, note, model;
            public SpeciesKind kind;
            public ShotType shot;
            public int day;
            public float rarity, wariness;
        }

        static readonly Seed[] All =
        {
            new Seed { id="mithun", common="Mithun", sci="Bos frontalis",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=1, rarity=0.20f, wariness=9f,
                model=Animals+"Bull.fbx",
                habitat="Forest clearings and hill slopes; semi-domesticated across Nagaland.",
                diet="Grasses, bamboo leaves, forest browse.",
                note="The state animal, and entirely unbothered by either of us. It watched me set up the shot." },

            new Seed { id="barking_deer", common="Barking Deer", sci="Muntiacus muntjak",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=1, rarity=0.30f, wariness=14f,
                model=Animals+"Deer.fbx",
                habitat="Thick forest edge and secondary growth near water.",
                diet="Fruit, shoots, tender leaves, occasionally eggs.",
                note="The call really does sound like a dog. She answered it once, then looked embarrassed." },

            new Seed { id="sambar", common="Sambar", sci="Rusa unicolor",
                kind=SpeciesKind.Fauna, shot=ShotType.QuietHands, day=2, rarity=0.45f, wariness=18f,
                model=Animals+"Stag.fbx",
                habitat="Dense hill forest, feeding at the margins near dusk.",
                diet="Grass, foliage, fallen fruit and bark.",
                note="Enormous, and completely silent until it decides not to be." },

            new Seed { id="red_fox", common="Red Fox", sci="Vulpes vulpes",
                kind=SpeciesKind.Fauna, shot=ShotType.HoldStill, day=3, rarity=0.60f, wariness=20f,
                model=Animals+"Fox.fbx",
                habitat="Open slopes, scrub and forest edge.",
                diet="Rodents, birds, insects, fruit.",
                note="She wanted to follow it. To her enormous credit, she did not." },

            new Seed { id="dhole", common="Dhole", sci="Cuon alpinus",
                kind=SpeciesKind.Fauna, shot=ShotType.HoldStill, day=5, rarity=0.85f, wariness=26f,
                model=Animals+"Wolf.fbx",
                habitat="Dense forest and hill country; lives and hunts in packs.",
                diet="Chiefly deer; also hares and rodents.",
                note="Endangered, and the reason this survey is funded at all. Three of them, gone in seconds." },

            new Seed { id="emerald_dove", common="Emerald Dove", sci="Chalcophaps indica",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=1, rarity=0.25f, wariness=12f,
                model=Birds+"low_poly_pigeon.glb",
                habitat="Shaded forest floor and lower storey; flushes fast and low.",
                diet="Fallen seeds, small fruit, termites.",
                note="Green as wet moss in the right light. Almost impossible to see until it moves." },

            new Seed { id="serpent_eagle", common="Crested Serpent Eagle", sci="Spilornis cheela",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=3, rarity=0.55f, wariness=22f,
                model=Birds+"low_poly_eagle.glb",
                habitat="Perches on exposed branches at the forest edge; soars mid-morning.",
                diet="Snakes, lizards, small mammals.",
                note="Calls all afternoon from somewhere you cannot find, then lands where you were standing." },

            new Seed { id="hawk_eagle", common="Mountain Hawk-Eagle", sci="Nisaetus nipalensis",
                kind=SpeciesKind.Fauna, shot=ShotType.SteadyLens, day=7, rarity=0.90f, wariness=28f,
                model=Birds+"harpia-animated-low-poly/source/Harpia.fbx",
                habitat="Broadleaf forest on steep hill country, hunting below the canopy.",
                diet="Pheasants, hares, squirrels.",
                note="Crested, and far bigger than the photographs prepare you for." },

            // Flora is dressed from the nature kits rather than a single model, so these
            // carry no prefab: the level places the matching bush or tree.
            new Seed { id="rhododendron", common="Rhododendron", sci="Rhododendron arboreum",
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=1, rarity=0.20f, wariness=0f, model="",
                habitat="Temperate hill slopes above 1500 m, often in dense stands.", diet="",
                note="The whole ridge goes red in season. From a distance you would swear the hillside was burning." },

            new Seed { id="tree_fern", common="Tree Fern", sci="Cyathea gigantea",
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=1, rarity=0.30f, wariness=0f, model="",
                habitat="Damp shaded gullies and streamsides.", diet="",
                note="Older than the trees around it, as a lineage. It looks it, too." },

            new Seed { id="bamboo", common="Hill Bamboo", sci="Dendrocalamus hamiltonii",
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=2, rarity=0.15f, wariness=0f, model="",
                habitat="Hill slopes and village margins; forms dense groves.", diet="",
                note="Everything up here is made of this. Houses, baskets, the bridge we crossed on day two." },

            new Seed { id="blue_vanda", common="Blue Vanda", sci="Vanda coerulea",
                kind=SpeciesKind.Flora, shot=ShotType.Compose, day=4, rarity=0.70f, wariness=0f, model="",
                habitat="Epiphytic on oak and other broadleaf trees in humid hill forest.", diet="",
                note="A protected orchid, and the colour is genuinely difficult to believe until you see one." },
        };

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

                if (!string.IsNullOrEmpty(seed.model))
                {
                    data.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(seed.model);
                    if (data.modelPrefab == null)
                    {
                        Debug.LogWarning("Species '" + seed.id + "': model not found at " + seed.model);
                        missing++;
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
