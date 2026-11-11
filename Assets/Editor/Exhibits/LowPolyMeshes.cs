using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes (every triangle has its own vertices) and saves them as assets.
    /// Used for trees, bushes, potatoes, pennants and the glowing zone rings.
    /// </summary>
    sealed class MeshBuilder
    {
        readonly List<Vector3> m_Vertices = new List<Vector3>();
        readonly List<Color> m_Colors = new List<Color>();
        readonly List<List<int>> m_Submeshes = new List<List<int>>();
        bool m_HasColors;

        public MeshBuilder(int submeshes = 1)
        {
            for (int i = 0; i < submeshes; i++)
                m_Submeshes.Add(new List<int>());
        }

        /// <summary>A triangle, clockwise as seen from its front (Unity's front-face winding).</summary>
        public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c) =>
            Triangle(submesh, a, b, c, Color.white, Color.white, Color.white);

        public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            if (ca != Color.white || cb != Color.white || cc != Color.white)
                m_HasColors = true;
            int start = m_Vertices.Count;
            m_Vertices.Add(a);
            m_Vertices.Add(b);
            m_Vertices.Add(c);
            m_Colors.Add(ca);
            m_Colors.Add(cb);
            m_Colors.Add(cc);
            m_Submeshes[submesh].Add(start);
            m_Submeshes[submesh].Add(start + 1);
            m_Submeshes[submesh].Add(start + 2);
        }

        /// <summary>An n-sided cone (or a tapered prism if the top radius is above zero), closed at both ends.</summary>
        public void Cone(int submesh, int sides, float y0, float y1, float bottomRadius, float topRadius, float twist = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides + twist) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides + twist) * Mathf.PI * 2f;
                var b0 = new Vector3(Mathf.Cos(a0) * bottomRadius, y0, Mathf.Sin(a0) * bottomRadius);
                var b1 = new Vector3(Mathf.Cos(a1) * bottomRadius, y0, Mathf.Sin(a1) * bottomRadius);
                var t0 = new Vector3(Mathf.Cos(a0) * topRadius, y1, Mathf.Sin(a0) * topRadius);
                var t1 = new Vector3(Mathf.Cos(a1) * topRadius, y1, Mathf.Sin(a1) * topRadius);
                Triangle(submesh, b0, t0, b1);
                if (topRadius > 0f)
                {
                    Triangle(submesh, b1, t0, t1);
                    Triangle(submesh, new Vector3(0f, y1, 0f), t1, t0);
                }
                Triangle(submesh, new Vector3(0f, y0, 0f), b0, b1);
            }
        }

        /// <summary>A slightly lumpy icosahedron (20 faces) with the given radii.</summary>
        public void Blob(int submesh, Vector3 center, Vector3 radii, int seed, float lumpiness = 0.24f)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var random = new System.Random(seed);
            for (int i = 0; i < v.Length; i++)
            {
                float lump = 1f - lumpiness * 0.5f + lumpiness * (float)random.NextDouble();
                v[i] = center + Vector3.Scale(v[i].normalized * lump, radii);
            }
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < f.Length; i += 3)
                Triangle(submesh, v[f[i]], v[f[i + 1]], v[f[i + 2]]);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(m_Vertices);
            if (m_HasColors)
                mesh.SetColors(m_Colors);
            mesh.subMeshCount = m_Submeshes.Count;
            for (int i = 0; i < m_Submeshes.Count; i++)
                mesh.SetTriangles(m_Submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    static class LowPolyMeshes
    {
        /// <summary>Saves a generated mesh as an asset, updating it in place if it exists (keeps references).</summary>
        public static Mesh Save(string path, Mesh generated)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                ExhibitBuilderUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            existing.Clear();
            existing.SetVertices(generated.vertices);
            if (generated.colors.Length > 0)
                existing.SetColors(generated.colors);
            existing.subMeshCount = generated.subMeshCount;
            for (int i = 0; i < generated.subMeshCount; i++)
                existing.SetTriangles(generated.GetTriangles(i), i);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generated);
            return existing;
        }

        /// <summary>
        /// A soft, see-through, additive glow material (URP Particles/Unlit) that uses vertex colour and alpha,
        /// so a mesh can fade from bright at the floor to nothing above it.
        /// </summary>
        public static Material GlowMaterial(string path, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                ExhibitBuilderUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);          // Transparent
            material.SetFloat("_Blend", 2f);            // Additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);             // Visible from both sides
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Height of the visible purple ground under a point. The environment's ground meshes have no colliders
        /// (players walk on invisible teleport boxes a few cm higher), so temporary mesh colliders are used.
        /// </summary>
        public static bool VisibleGroundHeight(Vector3 point, out float height)
        {
            var added = new List<MeshCollider>();
            try
            {
                foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null ||
                        filter.transform.parent == null || filter.transform.parent.name != "environment")
                        continue;
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.name != "Material.003")
                        continue; // Only the light purple ground, not buildings.
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    added.Add(collider);
                }
                Physics.SyncTransforms();
                var ray = new Ray(new Vector3(point.x, 50f, point.z), Vector3.down);
                float best = float.NegativeInfinity;
                foreach (var collider in added)
                {
                    if (collider.Raycast(ray, out var hit, 100f))
                        best = Mathf.Max(best, hit.point.y);
                }
                height = best;
                return !float.IsNegativeInfinity(best);
            }
            finally
            {
                foreach (var collider in added)
                    Object.DestroyImmediate(collider);
                Physics.SyncTransforms();
            }
        }
    }
}
