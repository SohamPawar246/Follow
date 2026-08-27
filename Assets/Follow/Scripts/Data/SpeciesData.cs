using UnityEngine;

namespace Follow.Data
{
    public enum SpeciesKind { Fauna, Flora }

    /// <summary>How the subject behaves, which decides which photo minigame you get.</summary>
    public enum ShotType
    {
        SteadyLens,  // birds: drifting reticle, focus ring
        QuietHands,  // mammals: arrow sequence before it looks up
        Compose,     // flora: no timer, framing only
        HoldStill    // rare: stop moving and let it come to you
    }

    [CreateAssetMenu(menuName = "Follow/Species", fileName = "SP_")]
    public class SpeciesData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string commonName;
        public string scientificName;
        public SpeciesKind kind = SpeciesKind.Fauna;
        public ShotType shotType = ShotType.SteadyLens;

        [Header("Field entry")]
        [TextArea(2, 4)] public string habitat;
        [TextArea(2, 4)] public string diet;
        [Tooltip("Two lines in the surveyor's own voice. This is what makes the album worth reading.")]
        [TextArea(2, 5)] public string fieldNote;

        [Header("Appearance in the world")]
        [Tooltip("Rarer species are seeded to show up only after this day.")]
        public int firstAppearsOnDay = 1;
        [Range(0f, 1f)] public float rarity = 0.4f;
        [Tooltip("How close you can get before it reacts, in metres.")]
        public float wariness = 12f;

        [Header("Art")]
        public GameObject modelPrefab;
        public Sprite silhouette;

        public bool AvailableOn(int day) => day >= firstAppearsOnDay;
    }
}
