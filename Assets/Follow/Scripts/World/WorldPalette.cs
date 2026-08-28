using System;
using System.Collections.Generic;
using UnityEngine;

namespace Follow.World
{
    /// <summary>
    /// Which rule of the composition a scatter layer answers to. The layer supplies the
    /// models and the numbers; the rule supplies the opinion about where they belong.
    /// </summary>
    public enum ScatterRule
    {
        Pine,           // the heights
        Broadleaf,      // the valleys
        AccentTree,     // rare autumn colour, anywhere the canopy is
        DeadTree,       // dead groves only
        Understory,     // the fringe of an opening
        Fern,           // damp hollows
        Grass,          // the open ground between thickets
        Flower,         // punctuation, in the open
        Rock,           // outcrops and steep ground
        Pebble,         // scattered, mostly on slopes
        Waterside,      // the ring of reeds and stones around a pond
        Firewood,       // fallen branches: the fuel the fire runs on
        Forage          // mushrooms: the dog's dinner and yours
    }

    /// <summary>
    /// The prefabs and numbers for one band of the forest. Prefabs are baked by the editor
    /// with their wind materials and colliders already on them, so streaming a chunk is a
    /// plain Instantiate and never touches the asset database or builds a material.
    /// </summary>
    [Serializable]
    public class ScatterLayer
    {
        public string name = "Layer";
        public ScatterRule rule = ScatterRule.Grass;
        public List<GameObject> prefabs = new List<GameObject>();

        [Tooltip("Chance of taking a candidate point that already passed the rule.")]
        [Range(0f, 1f)] public float chance = 1f;

        public Vector2 scale = new Vector2(0.8f, 1.2f);

        [Tooltip("Canopy layers use the coarse candidate grid and block the fine one.")]
        public bool canopy;

        public bool Usable => prefabs != null && prefabs.Count > 0;
    }

    /// <summary>
    /// Everything the streamer needs to dress a chunk, in one asset. Built by
    /// <c>Follow/Build The World</c>; nothing here is authored by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Follow/World Palette", fileName = "WorldPalette")]
    public class WorldPalette : ScriptableObject
    {
        static WorldPalette _active;
        public static WorldPalette Active
        {
            get
            {
                if (_active == null) _active = Resources.Load<WorldPalette>("WorldPalette");
                return _active;
            }
            set { _active = value; }
        }

        [Header("Scatter")]
        public List<ScatterLayer> layers = new List<ScatterLayer>();

        [Header("Surfaces")]
        public Material groundMaterial;
        public Material waterMaterial;

        [Header("Camp")]
        public GameObject campfireStones;
        public GameObject campfireLogs;
        public GameObject tent;
        public GameObject logStack;
        public GameObject stump;

        [Header("Wildlife")]
        [Tooltip("Small birds for the ground flocks. Background life, never photographable.")]
        public List<GameObject> birdModels = new List<GameObject>();

        [Header("Pickups")]
        [Tooltip("Small fallen branch, used as the firewood pickup model.")]
        public GameObject stickModel;
        [Tooltip("Mushroom cluster, used as the forage pickup model.")]
        public List<GameObject> forageModels = new List<GameObject>();

        public IEnumerable<ScatterLayer> Canopy
        {
            get { foreach (var l in layers) if (l.canopy && l.Usable) yield return l; }
        }

        public IEnumerable<ScatterLayer> Detail
        {
            get { foreach (var l in layers) if (!l.canopy && l.Usable) yield return l; }
        }
    }
}
