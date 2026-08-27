using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Follow.Data
{
    /// <summary>
    /// Every species in the build. Also generates the day's survey list, which is what
    /// makes the theme literal: the forest sets your objectives, not you.
    /// </summary>
    [CreateAssetMenu(menuName = "Follow/Species Library", fileName = "SpeciesLibrary")]
    public class SpeciesLibrary : ScriptableObject
    {
        static SpeciesLibrary _active;
        public static SpeciesLibrary Active
        {
            get
            {
                if (_active == null) _active = Resources.Load<SpeciesLibrary>("SpeciesLibrary");
                return _active;
            }
        }

        public List<SpeciesData> species = new List<SpeciesData>();

        public int Total => species.Count;

        public SpeciesData ById(string id) => species.FirstOrDefault(s => s != null && s.id == id);

        public IEnumerable<SpeciesData> AvailableOn(int day) =>
            species.Where(s => s != null && s.AvailableOn(day));

        /// <summary>
        /// Builds a day's list: mostly fauna, a little flora, weighted toward things the
        /// player has not photographed well yet so the list stays worth reading.
        /// </summary>
        /// <summary>
        /// The day's targets. Deterministic for a given day so the list never reshuffles
        /// while the player is working through it.
        /// </summary>
        public List<SpeciesData> BuildSurveyList(int day, int fauna, int flora, System.Func<string, bool> alreadyHave = null)
        {
            var pool = AvailableOn(day).ToList();
            var rng = new System.Random(day * 7919);

            // Ordering is seeded on the day alone. It must NOT depend on album state, or
            // the list reorders itself every time the player photographs something.
            List<SpeciesData> Pick(SpeciesKind kind, int count)
            {
                return pool.Where(s => s.kind == kind)
                    .OrderBy(s => rng.Next())
                    .Take(count)
                    .ToList();
            }

            var list = Pick(SpeciesKind.Fauna, fauna);
            list.AddRange(Pick(SpeciesKind.Flora, flora));
            return list;
        }
    }
}
