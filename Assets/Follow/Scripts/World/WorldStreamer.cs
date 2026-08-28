using System.Collections.Generic;
using UnityEngine;

namespace Follow.World
{
    /// <summary>
    /// Builds the forest around the player, forever.
    ///
    /// The world used to be one baked mesh with an edge you could walk off. Now it is a
    /// grid of chunks generated from <see cref="WorldComposer"/> as you approach and
    /// released behind you. Because the composer is a pure function of position, a chunk
    /// rebuilt an hour later contains the same trees in the same places - the forest is
    /// endless without being forgetful.
    ///
    /// Chunks are budgeted across frames, except for the ring immediately around the
    /// player, which is built in Awake so there is always ground underfoot on the first
    /// frame.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class WorldStreamer : MonoBehaviour
    {
        public static WorldStreamer Instance { get; private set; }

        [Header("Content")]
        public WorldPalette palette;

        [Header("Grid")]
        public float chunkSize = 44f;
        [Tooltip("Chunks of ground and canopy in every direction.")]
        public int viewRadius = 3;
        [Tooltip("Chunks that also get undergrowth. The outer ring reads as fog-bound trees.")]
        public int detailRadius = 2;
        [Tooltip("Quads per chunk edge. Terrain collision is only as fine as this.")]
        public int meshQuads = 24;

        [Header("Scatter grids, in metres")]
        public float canopyStep = 4.6f;
        public float detailStep = 1.9f;

        [Header("Budget")]
        [Tooltip("Chunks built per frame once the game is running.")]
        public int chunksPerFrame = 1;

        /// <summary>
        /// A live chunk. Detail is tracked separately from the canopy so a chunk can gain
        /// or lose its undergrowth without the trees blinking out and back.
        /// </summary>
        class Chunk
        {
            public GameObject root;
            public GameObject detail;
            public readonly List<Vector2> trunks = new List<Vector2>(48);
        }

        Transform _focus;
        readonly Dictionary<Vector2Int, Chunk> _live = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> _pending = new List<Vector2Int>();
        readonly List<Vector2Int> _expired = new List<Vector2Int>();
        Transform _root;
        Vector2Int _lastCentre = new Vector2Int(int.MinValue, int.MinValue);

        void Awake()
        {
            Instance = this;
            if (palette == null) palette = WorldPalette.Active;
            _root = new GameObject("Chunks").transform;
            _root.SetParent(transform, false);

            _focus = ResolveFocus();
            if (_focus == null || palette == null) return;

            // The ground the player is standing on cannot wait for a frame budget.
            var centre = ChunkOf(_focus.position);
            _lastCentre = centre;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                Build(new Vector2Int(centre.x + dx, centre.y + dz));

            Refresh(centre);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Transform ResolveFocus()
        {
            var player = Follow.Game.PlayerMover.Instance;
            if (player != null) return player.transform;
            var found = GameObject.Find("Player");
            return found != null ? found.transform : null;
        }

        void Update()
        {
            if (palette == null) return;
            if (_focus == null)
            {
                _focus = ResolveFocus();
                if (_focus == null) return;
            }

            var centre = ChunkOf(_focus.position);
            if (centre != _lastCentre) Refresh(centre);

            for (int i = 0; i < chunksPerFrame && _pending.Count > 0; i++)
            {
                var key = _pending[0];
                _pending.RemoveAt(0);
                Build(key);
            }
        }

        Vector2Int ChunkOf(Vector3 world) => new Vector2Int(
            Mathf.FloorToInt(world.x / chunkSize),
            Mathf.FloorToInt(world.z / chunkSize));

        /// <summary>Works out what should exist, queues the gaps, and drops what is behind us.</summary>
        void Refresh(Vector2Int centre)
        {
            _lastCentre = centre;
            _pending.Clear();

            for (int dx = -viewRadius; dx <= viewRadius; dx++)
            for (int dz = -viewRadius; dz <= viewRadius; dz++)
            {
                var key = new Vector2Int(centre.x + dx, centre.y + dz);
                if (_live.TryGetValue(key, out var live))
                {
                    // Already here, but it may have crossed the detail line since.
                    bool wants = WantsDetail(key, centre);
                    if (wants == (live.detail != null)) continue;
                    if (wants) _pending.Add(key);
                    else { Destroy(live.detail); live.detail = null; }
                    continue;
                }
                _pending.Add(key);
            }

            // Nearest first, so the hole you are walking toward fills before the corners.
            _pending.Sort((a, b) =>
                ((a - centre).sqrMagnitude).CompareTo((b - centre).sqrMagnitude));

            _expired.Clear();
            foreach (var kv in _live)
            {
                var d = kv.Key - centre;
                // One chunk of hysteresis, so walking a boundary does not thrash.
                if (Mathf.Abs(d.x) > viewRadius + 1 || Mathf.Abs(d.y) > viewRadius + 1)
                    _expired.Add(kv.Key);
            }
            foreach (var key in _expired)
            {
                if (_live.TryGetValue(key, out var chunk) && chunk.root != null) Destroy(chunk.root);
                _live.Remove(key);
            }
        }

        bool WantsDetail(Vector2Int key, Vector2Int centre) =>
            Mathf.Abs(key.x - centre.x) <= detailRadius && Mathf.Abs(key.y - centre.y) <= detailRadius;

        // --- building -------------------------------------------------------------------

        void Build(Vector2Int key)
        {
            if (_live.TryGetValue(key, out var existing))
            {
                // Already standing; this is a request for its undergrowth.
                if (existing.detail == null) BuildDetail(key, existing);
                return;
            }

            var chunk = new Chunk();
            chunk.root = new GameObject("Chunk " + key.x + "," + key.y);
            chunk.root.transform.SetParent(_root, false);
            chunk.root.transform.position = new Vector3(key.x * chunkSize, 0f, key.y * chunkSize);
            _live[key] = chunk;

            BuildGround(key, chunk.root.transform);
            BuildWater(key, chunk.root.transform);

            var canopy = new GameObject("Canopy").transform;
            canopy.SetParent(chunk.root.transform, false);
            Pass(key, canopy, key.x * chunkSize, key.y * chunkSize, canopyStep, 0x51A1u, true, chunk.trunks);

            if (WantsDetail(key, _lastCentre)) BuildDetail(key, chunk);
        }

        void BuildDetail(Vector2Int key, Chunk chunk)
        {
            chunk.detail = new GameObject("Detail");
            chunk.detail.transform.SetParent(chunk.root.transform, false);
            Pass(key, chunk.detail.transform, key.x * chunkSize, key.y * chunkSize,
                detailStep, 0xD37Au, false, chunk.trunks);
        }

        /// <summary>
        /// The chunk's slab of ground. Normals come from the height function rather than
        /// from the triangles, because per-mesh normals leave a lighting seam on every
        /// chunk border; vertex colour carries the damp, the dry and the bare rock.
        /// </summary>
        void BuildGround(Vector2Int key, Transform parent)
        {
            int n = meshQuads + 1;
            float step = chunkSize / meshQuads;
            float ox = key.x * chunkSize;
            float oz = key.y * chunkSize;

            var verts = new Vector3[n * n];
            var norms = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float wx = ox + x * step;
                float wz = oz + z * step;
                int i = z * n + x;

                verts[i] = new Vector3(x * step, WorldComposer.Height(wx, wz), z * step);
                norms[i] = WorldComposer.Normal(wx, wz);
                uvs[i] = new Vector2(wx, wz) * 0.12f;
                colors[i] = GroundTint(wx, wz);
            }

            var tris = new int[meshQuads * meshQuads * 6];
            int t = 0;
            for (int z = 0; z < meshQuads; z++)
            for (int x = 0; x < meshQuads; x++)
            {
                int i = z * n + x;
                tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
            }

            var mesh = new Mesh { name = "Ground " + key.x + "," + key.y };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("Ground") < 0 ? 0 : LayerMask.NameToLayer("Ground");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = palette.groundMaterial;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>
        /// Ground colour, baked into the vertices. Flat green everywhere is the single
        /// biggest reason a low-poly forest floor reads as a placeholder.
        /// </summary>
        static Color GroundTint(float x, float z)
        {
            float damp = WorldComposer.Moisture(x, z);
            float slope = WorldComposer.Slope(x, z);

            // Two scales of drift. The slow one gives whole meadows their own cast; the
            // faster one breaks up the ground inside a single screen, which is the part
            // that was missing when the floor read as one flat green.
            float broad = Mathf.PerlinNoise(x * 0.008f + 71f, z * 0.008f + 12f);
            float patch = Mathf.PerlinNoise(x * 0.045f + 5.3f, z * 0.045f + 88.1f);

            var dry = new Color(0.60f, 0.58f, 0.30f);
            var lush = new Color(0.22f, 0.46f, 0.24f);
            var moss = new Color(0.30f, 0.52f, 0.30f);
            var earth = new Color(0.38f, 0.29f, 0.20f);

            // The fast noise has to carry most of the mix, or every screenful is one
            // colour and only somebody walking a hundred metres ever sees the difference.
            var c = Color.Lerp(dry, lush,
                Mathf.Clamp01(damp * 0.5f + broad * 0.35f + patch * 0.6f - 0.15f));
            c = Color.Lerp(c, moss, Mathf.Clamp01(patch * 0.8f - 0.25f));
            c = Color.Lerp(c, earth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 0.62f, slope)));

            // A little shade in the hollows, so the terrain has form even at noon.
            float low = Mathf.Clamp01(1f - (WorldComposer.Height(x, z) + 6f) / 14f);
            return Color.Lerp(c, c * 0.78f, low * 0.5f);
        }

        void BuildWater(Vector2Int key, Transform parent)
        {
            if (palette.waterMaterial == null) return;

            // Only the chunk that contains a pond's centre draws it, so the disc is
            // built exactly once however many chunks it overlaps.
            for (int cx = -1; cx <= 1; cx++)
            for (int cz = -1; cz <= 1; cz++)
            {
                int lx = Mathf.FloorToInt(key.x * chunkSize / WorldComposer.LandmarkCell) + cx;
                int lz = Mathf.FloorToInt(key.y * chunkSize / WorldComposer.LandmarkCell) + cz;
                if (!WorldComposer.CellLandmark(lx, lz, out var lm)) continue;
                if (lm.kind != WorldComposer.LandmarkKind.Pond) continue;
                if (ChunkOf(new Vector3(lm.position.x, 0f, lm.position.y)) != key) continue;

                var disc = new GameObject("Pond");
                disc.transform.SetParent(parent, true);
                disc.transform.position = new Vector3(
                    lm.position.x, WorldComposer.PondSurface(lm), lm.position.y);

                // Deliberately wider than the pond. The bank is a bowl, so a disc cut to
                // the exact radius ends in mid-air above the slope and you see the rim of
                // a sheet of glass. Overshooting buries the edge in the hillside instead,
                // and the visible waterline becomes wherever the ground actually crosses
                // it - an irregular shore rather than a drawn circle.
                float reach = lm.radius * 1.28f;
                disc.transform.localScale = new Vector3(reach, 1f, reach);
                disc.AddComponent<MeshFilter>().sharedMesh = WaterDisc();
                disc.AddComponent<MeshRenderer>().sharedMaterial = palette.waterMaterial;
            }
        }

        static Mesh _waterDisc;

        /// <summary>
        /// A flat fan, built once and reused at every pond.
        ///
        /// This used to be a Unity cylinder squashed to 8 centimetres, which meant every
        /// pond was a solid slab with a lit side wall and a cap facing the ground - three
        /// surfaces where the water wanted one.
        /// </summary>
        static Mesh WaterDisc()
        {
            if (_waterDisc == null)
            {
                const int segments = 48;
                var vertices = new Vector3[segments + 1];
                var normals = new Vector3[segments + 1];
                var uvs = new Vector2[segments + 1];
                var triangles = new int[segments * 3];

                vertices[0] = Vector3.zero;
                normals[0] = Vector3.up;
                uvs[0] = new Vector2(0.5f, 0.5f);

                for (int i = 0; i < segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    normals[i + 1] = Vector3.up;
                    uvs[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);

                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 1) % segments + 1;
                }

                _waterDisc = new Mesh { name = "PondDisc" };
                _waterDisc.vertices = vertices;
                _waterDisc.normals = normals;
                _waterDisc.uv = uvs;
                _waterDisc.triangles = triangles;
                _waterDisc.RecalculateBounds();
            }

            return _waterDisc;
        }

        // --- scatter ---------------------------------------------------------------------

        void Pass(Vector2Int key, Transform parent, float ox, float oz, float step,
            uint salt, bool canopy, List<Vector2> trunks)
        {
            int cells = Mathf.Max(1, Mathf.RoundToInt(chunkSize / step));
            float actual = chunkSize / cells;

            for (int gz = 0; gz < cells; gz++)
            for (int gx = 0; gx < cells; gx++)
            {
                // Seeded on the world cell, not the chunk, so a chunk rebuilt after you
                // walk back re-plants exactly the same tree in the same spot.
                uint state = Cell(key.x * cells + gx, key.y * cells + gz, salt);

                float x = ox + (gx + Jitter(ref state)) * actual;
                float z = oz + (gz + Jitter(ref state)) * actual;

                if (!canopy)
                {
                    bool blocked = false;
                    for (int i = 0; i < trunks.Count; i++)
                        if ((trunks[i] - new Vector2(x, z)).sqrMagnitude < 2.2f) { blocked = true; break; }
                    if (blocked) continue;
                }

                var layer = Choose(x, z, canopy, ref state);
                if (layer == null) continue;

                var prefab = layer.prefabs[Mathf.Min(layer.prefabs.Count - 1,
                    Mathf.FloorToInt(Roll(ref state) * layer.prefabs.Count))];
                if (prefab == null) continue;

                var inst = Instantiate(prefab, parent);
                inst.transform.position = new Vector3(x, WorldComposer.Height(x, z) - 0.06f, z);
                inst.transform.rotation = Quaternion.Euler(0f, Roll(ref state) * 360f, 0f);

                // Prefabs are baked at one unit across, so this is a size in metres.
                float s = Mathf.Lerp(layer.scale.x, layer.scale.y, Roll(ref state));
                inst.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.94f, 1.1f, Roll(ref state)), s);

                if (canopy) trunks.Add(new Vector2(x, z));
            }
        }

        /// <summary>
        /// Asks each layer in turn whether this point belongs to it. First one to accept
        /// wins, which is what keeps a fern from growing inside a boulder.
        /// </summary>
        ScatterLayer Choose(float x, float z, bool canopy, ref uint state)
        {
            var source = canopy ? palette.Canopy : palette.Detail;
            foreach (var layer in source)
            {
                float weight = Weight(layer.rule, x, z) * layer.chance;
                if (weight <= 0f) continue;
                if (Roll(ref state) < weight) return layer;
            }
            return null;
        }

        static float Weight(ScatterRule rule, float x, float z)
        {
            switch (rule)
            {
                case ScatterRule.Pine:
                    return WorldComposer.ZoneAt(x, z) == WorldComposer.Zone.Pine
                        ? WorldComposer.Density(x, z) : WorldComposer.Density(x, z) * 0.12f;

                case ScatterRule.Broadleaf:
                    return WorldComposer.ZoneAt(x, z) == WorldComposer.Zone.Broadleaf
                        ? WorldComposer.Density(x, z) : WorldComposer.Density(x, z) * 0.18f;

                case ScatterRule.AccentTree:
                    return WorldComposer.Density(x, z) * 0.35f;

                case ScatterRule.DeadTree:
                    return WorldComposer.NearKind(x, z, WorldComposer.LandmarkKind.DeadGrove);

                case ScatterRule.Understory:
                    return Mathf.Max(WorldComposer.EdgeBand(x, z), WorldComposer.Density(x, z) * 0.35f);

                case ScatterRule.Fern:
                    return WorldComposer.ZoneAt(x, z) == WorldComposer.Zone.Damp ? 0.9f : 0.2f;

                case ScatterRule.Grass:
                    return 1f - WorldComposer.Density(x, z) * 0.7f;

                case ScatterRule.Flower:
                    return WorldComposer.Density(x, z) < 0.3f ? 0.5f : 0.05f;

                case ScatterRule.Rock:
                    return Mathf.Max(WorldComposer.NearKind(x, z, WorldComposer.LandmarkKind.Outcrop),
                                     WorldComposer.Slope(x, z) * 0.8f);

                case ScatterRule.Pebble:
                    return 0.22f + WorldComposer.Slope(x, z) * 0.4f;

                case ScatterRule.Waterside:
                {
                    // A band just outside the water line: reeds and stones at the edge are
                    // what make a pond visible through the trees.
                    float near = WorldComposer.NearKind(x, z, WorldComposer.LandmarkKind.Pond);
                    return near > 0.02f && near < 0.55f ? 0.85f : 0f;
                }

                case ScatterRule.Firewood:
                    // Branches fall where trees are, and pile up in the dead groves. This
                    // used to be so sparse that a player could walk for a full day without
                    // finding the four sticks the fire needs to exist at all.
                    return 0.25f + WorldComposer.Density(x, z) * 0.6f
                         + WorldComposer.NearKind(x, z, WorldComposer.LandmarkKind.DeadGrove) * 0.9f;

                case ScatterRule.Forage:
                    // Mushrooms want damp shade, but food is not optional and damp ground
                    // is rare - a player who never crosses a hollow still has to eat.
                    return WorldComposer.ZoneAt(x, z) == WorldComposer.Zone.Damp
                        ? 0.25f + WorldComposer.Density(x, z) * 0.6f
                        : 0.10f + WorldComposer.Density(x, z) * 0.35f;
            }
            return 0f;
        }

        // --- deterministic per-cell noise ---------------------------------------------

        static uint Cell(int x, int z, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u ^ salt;
                h = (h ^ (uint)x) * 16777619u; h ^= h >> 13;
                h = (h ^ (uint)z) * 16777619u; h ^= h >> 15;
                return h ^ (h >> 7);
            }
        }

        static float Roll(ref uint state)
        {
            unchecked
            {
                state = state * 1664525u + 1013904223u;
                return (state >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>Kept off the cell corners, so the grid never shows through as rows.</summary>
        static float Jitter(ref uint state) => 0.15f + Roll(ref state) * 0.7f;

        // --- queries -------------------------------------------------------------------

        /// <summary>
        /// Ground height under a point. Uses the composer rather than a raycast, so it
        /// answers correctly for chunks that have not been built yet.
        /// </summary>
        public static float GroundAt(float x, float z) => WorldComposer.Height(x, z);

        /// <summary>Puts a transform on the ground, wherever it currently is.</summary>
        public static void Drop(Transform t, float clearance = 0.05f)
        {
            var p = t.position;
            t.position = new Vector3(p.x, WorldComposer.Height(p.x, p.z) + clearance, p.z);
        }
    }
}
