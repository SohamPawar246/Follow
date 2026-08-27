using System.Collections.Generic;
using UnityEngine;

namespace Follow.World
{
    /// <summary>
    /// The authored structure of the forest, as a set of pure functions of world position.
    ///
    /// Nothing here is stored in a list that has a last element, because the forest has no
    /// edge. Landmarks are derived from the coordinate they sit on rather than generated
    /// into an array, so asking "what is at (4000, -900)?" costs the same as asking about
    /// the clearing behind camp, and always gives the same answer.
    ///
    /// This lives in the runtime assembly on purpose: the streamer needs it while the game
    /// is running, and the editor tools need the identical answers when they preview.
    /// </summary>
    public static class WorldComposer
    {
        public const float CampRadius = 13f;

        // --- deterministic noise ----------------------------------------------------

        /// <summary>Integer hash. Two cells that differ by one must not look related.</summary>
        static uint Hash(int x, int y, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u ^ salt;
                h = (h ^ (uint)x) * 16777619u;
                h ^= h >> 13;
                h = (h ^ (uint)y) * 16777619u;
                h ^= h >> 15;
                h = (h ^ (h >> 7)) * 2654435761u;
                return h ^ (h >> 16);
            }
        }

        static float Next01(ref uint state)
        {
            unchecked
            {
                state = state * 1664525u + 1013904223u;
                return (state >> 8) * (1f / 16777216f);
            }
        }

        // --- landmarks ---------------------------------------------------------------

        public enum LandmarkKind { Pond, Outcrop, DeadGrove, Clearing }

        public struct Landmark
        {
            public LandmarkKind kind;
            public Vector2 position;
            public float radius;
        }

        /// <summary>
        /// One landmark candidate per cell of this size. Big enough that two ponds never
        /// touch, small enough that you meet something every twenty seconds of walking.
        /// </summary>
        public const float LandmarkCell = 50f;

        /// <summary>
        /// The landmark belonging to a cell, or none. Derived from the cell coordinate, so
        /// it is stable forever without being stored anywhere.
        /// </summary>
        public static bool CellLandmark(int cx, int cz, out Landmark landmark)
        {
            landmark = default;

            uint state = Hash(cx, cz, 0x5EED2026u);
            if (Next01(ref state) < 0.18f) return false;   // some cells stay empty

            var pos = new Vector2(
                cx * LandmarkCell + (Next01(ref state) - 0.5f) * LandmarkCell * 0.6f,
                cz * LandmarkCell + (Next01(ref state) - 0.5f) * LandmarkCell * 0.6f);

            // Camp owns the middle. Nothing is allowed to dig a pond through the fire.
            if (pos.magnitude < CampRadius + 22f) return false;

            // Water is the most useful thing on the map, so it gets half the weight. A
            // forest where you cannot find a pond is a forest with nothing in it.
            float roll = Next01(ref state);
            LandmarkKind kind = roll < 0.46f ? LandmarkKind.Pond
                              : roll < 0.64f ? LandmarkKind.Outcrop
                              : roll < 0.80f ? LandmarkKind.DeadGrove
                              : LandmarkKind.Clearing;

            landmark = new Landmark
            {
                kind = kind,
                position = pos,
                radius = kind switch
                {
                    LandmarkKind.Pond => Mathf.Lerp(9f, 15f, Next01(ref state)),
                    LandmarkKind.Outcrop => 8f,
                    LandmarkKind.DeadGrove => 13f,
                    _ => 15f
                }
            };
            return true;
        }

        /// <summary>
        /// Every landmark that can reach a point. Separate buffers per caller because
        /// Height, Moisture and Density each hold their results across a nested call.
        /// </summary>
        static void Gather(float x, float z, List<Landmark> into)
        {
            into.Clear();
            int cx = Mathf.FloorToInt(x / LandmarkCell);
            int cz = Mathf.FloorToInt(z / LandmarkCell);
            for (int ix = cx - 1; ix <= cx + 1; ix++)
            for (int iz = cz - 1; iz <= cz + 1; iz++)
                if (CellLandmark(ix, iz, out var lm)) into.Add(lm);
        }

        static readonly List<Landmark> _forHeight = new List<Landmark>(9);
        static readonly List<Landmark> _forMoisture = new List<Landmark>(9);
        static readonly List<Landmark> _forDensity = new List<Landmark>(9);
        static readonly List<Landmark> _forQuery = new List<Landmark>(64);
        static readonly List<Landmark> _forWalkable = new List<Landmark>(9);

        /// <summary>Landmarks within a radius of a point, for gameplay rather than terrain.</summary>
        public static List<Landmark> LandmarksNear(Vector2 centre, float radius)
        {
            _forQuery.Clear();
            int span = Mathf.CeilToInt(radius / LandmarkCell) + 1;
            int cx = Mathf.FloorToInt(centre.x / LandmarkCell);
            int cz = Mathf.FloorToInt(centre.y / LandmarkCell);

            for (int ix = cx - span; ix <= cx + span; ix++)
            for (int iz = cz - span; iz <= cz + span; iz++)
            {
                if (!CellLandmark(ix, iz, out var lm)) continue;
                if (Vector2.Distance(lm.position, centre) > radius) continue;
                _forQuery.Add(lm);
            }
            return _forQuery;
        }

        /// <summary>The nearest pond, for the compass. A full sweep, because an early
        /// exit on the first ring that contains one can still miss a closer diagonal.</summary>
        public static bool NearestPond(Vector2 from, float maxRange, out Landmark pond)
        {
            pond = default;
            float best = maxRange;
            bool found = false;

            int span = Mathf.CeilToInt(maxRange / LandmarkCell) + 1;
            int cx = Mathf.FloorToInt(from.x / LandmarkCell);
            int cz = Mathf.FloorToInt(from.y / LandmarkCell);

            for (int ix = cx - span; ix <= cx + span; ix++)
            for (int iz = cz - span; iz <= cz + span; iz++)
            {
                if (!CellLandmark(ix, iz, out var lm)) continue;
                if (lm.kind != LandmarkKind.Pond) continue;

                float d = Vector2.Distance(lm.position, from);
                if (d >= best) continue;
                best = d;
                pond = lm;
                found = true;
            }
            return found;
        }

        // --- terrain ------------------------------------------------------------------

        /// <summary>Rolling ground, flattened toward camp so the start is level.</summary>
        public static float Height(float x, float z)
        {
            float h = 0f;
            h += (Mathf.PerlinNoise(x * 0.0075f + 3.1f, z * 0.0075f + 7.7f) - 0.5f) * 13f;
            h += (Mathf.PerlinNoise(x * 0.031f + 11.3f, z * 0.031f + 2.9f) - 0.5f) * 3.4f;
            h += (Mathf.PerlinNoise(x * 0.11f + 5.5f, z * 0.11f + 19.1f) - 0.5f) * 0.7f;

            float d = Mathf.Sqrt(x * x + z * z);
            h *= Mathf.Clamp01((d - CampRadius) / 18f);

            // Ponds sit in real hollows, not painted onto flat ground.
            Gather(x, z, _forHeight);
            for (int i = 0; i < _forHeight.Count; i++)
            {
                var p = _forHeight[i];
                if (p.kind != LandmarkKind.Pond) continue;
                float pd = Vector2.Distance(new Vector2(x, z), p.position);
                float reach = p.radius * 1.7f;
                if (pd > reach) continue;
                h -= (1f - Mathf.SmoothStep(0f, 1f, pd / reach)) * 3.6f;
            }
            return h;
        }

        /// <summary>
        /// The surface normal, from the analytic gradient rather than from the triangles.
        /// Chunk meshes are built independently, and per-mesh normals would leave a visible
        /// lighting seam along every chunk border.
        /// </summary>
        public static Vector3 Normal(float x, float z)
        {
            const float e = 0.6f;
            float hx = Height(x + e, z) - Height(x - e, z);
            float hz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        /// <summary>Approximate slope, 0 flat to 1 steep. Drives what will grow where.</summary>
        public static float Slope(float x, float z)
        {
            const float e = 1.6f;
            float hx = Height(x + e, z) - Height(x - e, z);
            float hz = Height(x, z + e) - Height(x, z - e);
            return Mathf.Clamp01(new Vector2(hx, hz).magnitude / (e * 2f) * 1.6f);
        }

        /// <summary>Damp ground: low, flat, and near water. Ferns and clover want this.</summary>
        public static float Moisture(float x, float z)
        {
            float noise = Mathf.PerlinNoise(x * 0.018f + 41.2f, z * 0.018f + 17.9f);
            float low = 1f - Mathf.Clamp01((Height(x, z) + 4f) / 10f);

            Gather(x, z, _forMoisture);
            float nearWater = 0f;
            for (int i = 0; i < _forMoisture.Count; i++)
            {
                var p = _forMoisture[i];
                if (p.kind != LandmarkKind.Pond) continue;
                float d = Vector2.Distance(new Vector2(x, z), p.position);
                nearWater = Mathf.Max(nearWater, 1f - Mathf.Clamp01(d / (p.radius * 3.5f)));
            }
            return Mathf.Clamp01(noise * 0.5f + low * 0.3f + nearWater * 0.8f);
        }

        // --- trails --------------------------------------------------------------------

        /// <summary>
        /// Openness owed to trails, 0 on the path and 1 in the trees.
        ///
        /// Two things at once: four spokes out of camp so leaving the fire always has an
        /// obvious direction, and a ridged noise that makes deer trails everywhere else.
        /// A ridge - the place a noise field crosses its own midpoint - is a continuous
        /// line that never ends, which is exactly what a path through an endless wood is.
        /// </summary>
        public static float TrailOpenness(float x, float z)
        {
            var p = new Vector2(x, z);
            float open = 1f;

            // Camp spokes: four routes out of the fire, so leaving always has a
            // direction. They wobble with distance, so each curves away rather than
            // leaving camp as a surveyor's ray.
            float fromCamp = p.magnitude;
            if (fromCamp > 1f && fromCamp < 130f)
            {
                float degrees = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
                float wobble = (Mathf.PerlinNoise(fromCamp * 0.018f, 4.7f) - 0.5f) * 26f;
                // Spokes every 90 degrees; this is the angle to whichever is closest.
                float offAxis = Mathf.Abs(Mathf.Repeat(degrees + wobble + 45f, 90f) - 45f);
                float lateral = offAxis * Mathf.Deg2Rad * fromCamp;

                float clear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.4f, 9f, lateral));
                // The spokes fade out rather than stopping dead at their far end.
                open *= Mathf.Lerp(1f, clear, Mathf.InverseLerp(130f, 95f, fromCamp));
            }

            // Deer trails: the ridge of a slow noise field.
            float ridge = Mathf.Abs(Mathf.PerlinNoise(x * 0.0052f + 13.7f, z * 0.0052f + 51.3f) - 0.5f);
            open *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.012f, 0.05f, ridge));

            return open;
        }

        // --- the density field ----------------------------------------------------------

        /// <summary>
        /// How much canopy belongs at a point, 0 to 1. Thickets and glades come from low
        /// frequency noise; clearings, trails and camp carve holes with soft edges.
        /// </summary>
        public static float Density(float x, float z)
        {
            var p = new Vector2(x, z);

            // Two octaves, sharply remapped. A gentle gradient gives evenly-wooded
            // everywhere; the contrast is what produces thickets you push through and
            // glades you can breathe in.
            float thicket = Mathf.PerlinNoise(x * 0.012f + 60.3f, z * 0.012f + 22.7f) * 0.75f
                          + Mathf.PerlinNoise(x * 0.033f + 8.1f, z * 0.033f + 44.6f) * 0.25f;
            float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.60f, thicket));
            d = Mathf.Lerp(0.02f, 1f, d);

            // Camp is open, with a soft rim rather than a hard circle.
            d *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CampRadius, CampRadius + 10f, p.magnitude));

            d *= TrailOpenness(x, z);

            Gather(x, z, _forDensity);
            for (int i = 0; i < _forDensity.Count; i++)
            {
                var lm = _forDensity[i];
                float ld = Vector2.Distance(p, lm.position);
                switch (lm.kind)
                {
                    case LandmarkKind.Pond:
                        // Generous: a pond you cannot see until you are standing in it is
                        // not a landmark, so the trees pull well back from the water.
                        d *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.radius * 1.0f, lm.radius * 2.1f, ld));
                        break;
                    case LandmarkKind.Clearing:
                        d *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.radius * 0.7f, lm.radius * 1.35f, ld));
                        break;
                    case LandmarkKind.Outcrop:
                        d *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lm.radius * 0.6f, lm.radius, ld));
                        break;
                    case LandmarkKind.DeadGrove:
                        // Thins rather than clears - dead trees handle this patch themselves.
                        if (ld < lm.radius) d *= 0.25f;
                        break;
                }
            }

            // Nothing clings to a cliff.
            d *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.9f, Slope(x, z)));
            return Mathf.Clamp01(d);
        }

        /// <summary>
        /// True at the fringe of an opening. Bushes go here: a clearing with a defined
        /// edge reads as a place, one that simply fades out reads as missing geometry.
        /// </summary>
        public static float EdgeBand(float x, float z)
        {
            float here = Density(x, z);
            if (here < 0.12f || here > 0.62f) return 0f;
            return 1f - Mathf.Abs(here - 0.36f) / 0.26f;
        }

        /// <summary>Nearness to a landmark of one kind, 0 to 1, for layers that belong to it.</summary>
        public static float NearKind(float x, float z, LandmarkKind kind)
        {
            Gather(x, z, _forDensity);
            var p = new Vector2(x, z);
            float best = 0f;
            for (int i = 0; i < _forDensity.Count; i++)
            {
                var lm = _forDensity[i];
                if (lm.kind != kind) continue;
                best = Mathf.Max(best, 1f - Mathf.Clamp01(Vector2.Distance(p, lm.position) / lm.radius));
            }
            return best;
        }

        // --- species zoning ---------------------------------------------------------------

        public enum Zone { Pine, Broadleaf, Damp, Rocky }

        /// <summary>Pines take the heights, broadleaf the valleys, ferns the damp hollows.</summary>
        public static Zone ZoneAt(float x, float z)
        {
            if (Slope(x, z) > 0.42f) return Zone.Rocky;
            if (Moisture(x, z) > 0.62f) return Zone.Damp;
            return Height(x, z) > 1.6f ? Zone.Pine : Zone.Broadleaf;
        }

        /// <summary>
        /// Ground clear enough to stand something on: away from water, off the steep, and
        /// out from under the canopy. Used to place the dog's finds and the player's spawn.
        /// </summary>
        public static bool IsWalkable(float x, float z)
        {
            if (Slope(x, z) > 0.5f) return false;
            Gather(x, z, _forWalkable);
            for (int i = 0; i < _forWalkable.Count; i++)
            {
                var lm = _forWalkable[i];
                if (lm.kind != LandmarkKind.Pond) continue;
                if (Vector2.Distance(new Vector2(x, z), lm.position) < lm.radius * 1.15f) return false;
            }
            return true;
        }

        /// <summary>The water surface height of a pond, which is above the floor it sits in.</summary>
        public static float PondSurface(Landmark pond) =>
            Height(pond.position.x, pond.position.y) + 1.55f;
    }
}
