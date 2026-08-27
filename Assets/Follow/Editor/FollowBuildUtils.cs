using System.IO;
using UnityEditor;
using UnityEngine;

namespace Follow.EditorTools
{
    /// <summary>Shared plumbing for the builders: folders, layers, tags, terrain meshes.</summary>
    public static class FollowBuildUtils
    {
        public const string AssetRoot = "Assets/Follow";

        /// <summary>Layer lookup that degrades to Default rather than throwing.</summary>
        public static int Layer(string name)
        {
            int i = LayerMask.NameToLayer(name);
            return i < 0 ? 0 : i;
        }

        /// <summary>Assigns a tag only if it is registered, so a fresh project never fails.</summary>
        public static void SetTag(GameObject go, string tag)
        {
            foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
                if (t == tag) { go.tag = tag; return; }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace((char)92, '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Builds a heightfield mesh from a sampler and saves it as an asset, so the
        /// collider stays stable across reloads instead of being rebuilt every play.
        /// </summary>
        public static Mesh BuildHeightfield(string name, float size, int resolution,
            System.Func<float, float, float> height)
        {
            int n = resolution + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var tris = new int[resolution * resolution * 6];

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fx = (x / (float)resolution - 0.5f) * size;
                    float fz = (z / (float)resolution - 0.5f) * size;
                    verts[z * n + x] = new Vector3(fx, height(fx, fz), fz);
                    uvs[z * n + x] = new Vector2(x / (float)resolution, z / (float)resolution) * 16f;
                }
            }

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * n + x;
                    tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
                }
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder(AssetRoot + "/Meshes");
            string path = AssetRoot + "/Meshes/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
