using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Plants one line of low-poly trees and bushes just inside the edge of the big purple lawn (the oval and
    /// the round bulge on its east side), leaving gaps where paths lead off it and around the exhibits.
    /// Meshes are generated (6-sided cones, icosahedrons, flat shaded) and saved once as assets; every tree
    /// shares them, has no collider, and is marked static, so the whole ring costs very little to draw.
    /// Rebuilding replaces the ring. The layout is random but repeatable (fixed seed).
    /// </summary>
    static class NatureRingBuilder
    {
        const string k_RootName = "Nature Ring";
        const string k_Folder = "Assets/Exhibits/Nature";

        // Environment meshes that make up the lawn the ring follows, and ground that leads off it.
        static readonly string[] k_LawnMeshes = { "Cylinder", "Cylinder.002" };
        static readonly string[] k_OtherGroundMeshes = { "Cylinder.001", "Cube", "Cube.001" };

        static readonly Vector2 k_LawnCenter = new Vector2(9.5f, 7f);
        const float k_Spacing = 1.6f;   // Metres along the edge between plants.
        const float k_Inset = 0.7f;     // How far inside the lawn's edge plants stand.
        const int k_Seed = 2035;
        const float k_OuterSpacing = 3.2f; // Metres between trees in the outer row on the sand.

        /// <summary>Top of the sand (the environment's large "back up floor") under a point.</summary>
        static bool SandHeight(Vector3 point, out float height)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(point.x, 50f, point.z), Vector3.down, 100f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name == "back up floor")
                {
                    height = hit.point.y;
                    return true;
                }
            }
            height = 0f;
            return false;
        }

        [MenuItem("XR Classroom/Environment/Build Tree Ring")]
        static void Build()
        {
            if (!CheckDemoSceneIsActive("plant the tree ring"))
                return;

            var existing = FindInScene(k_RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            var trunk = LitMaterial($"{k_Folder}/Materials/Trunk.mat", new Color(0.42f, 0.28f, 0.16f), smoothness: 0.1f);
            var leaves = LitMaterial($"{k_Folder}/Materials/Leaves.mat", new Color(0.24f, 0.55f, 0.27f), smoothness: 0.1f);
            var leavesLight = LitMaterial($"{k_Folder}/Materials/LeavesLight.mat", new Color(0.4f, 0.66f, 0.3f), smoothness: 0.1f);
            var bushMaterial = LitMaterial($"{k_Folder}/Materials/Bush.mat", new Color(0.2f, 0.47f, 0.24f), smoothness: 0.1f);
            foreach (var material in new[] { trunk, leaves, leavesLight, bushMaterial })
                material.enableInstancing = true;

            var pine = LowPolyMeshes.Save($"{k_Folder}/Meshes/PineTree.asset", PineTreeMesh());
            var round = LowPolyMeshes.Save($"{k_Folder}/Meshes/RoundTree.asset", RoundTreeMesh());
            var bush = LowPolyMeshes.Save($"{k_Folder}/Meshes/Bush.asset", BushMesh());

            var points = FindEdge();
            if (points.Count == 0)
            {
                Debug.LogError("[Nature] Could not find the lawn's edge (are the environment meshes named as expected?).");
                return;
            }

            var root = new GameObject(k_RootName).transform;
            var random = new System.Random(k_Seed);
            int planted = 0;
            float next = 0f;
            float travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                travelled += Vector3.Distance(points[i - 1].position, points[i].position);
                if (travelled < next)
                    continue;
                next = travelled + k_Spacing * (0.8f + 0.4f * (float)random.NextDouble());

                var (position, inward, open) = points[i];
                if (open)
                    continue;
                position += inward * (k_Inset + 0.5f * (float)random.NextDouble());
                if (IsBlocked(position))
                    continue;

                bool isTree = random.NextDouble() < 0.65;
                Mesh mesh;
                Material[] materials;
                string name;
                if (!isTree)
                {
                    (mesh, materials, name) = (bush, new[] { bushMaterial }, "Bush");
                }
                else if (random.NextDouble() < 0.5)
                {
                    (mesh, materials, name) = (pine, new[] { trunk, leaves }, "Pine");
                }
                else
                {
                    (mesh, materials, name) = (round, new[] { trunk, leavesLight }, "Tree");
                }

                var plant = new GameObject($"{name} {planted + 1}");
                plant.transform.SetParent(root, false);
                plant.transform.position = position;
                plant.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = isTree ? 0.85f + 0.45f * (float)random.NextDouble() : 0.8f + 0.6f * (float)random.NextDouble();
                plant.transform.localScale = Vector3.one * scale;
                plant.AddComponent<MeshFilter>().sharedMesh = mesh;
                plant.AddComponent<MeshRenderer>().sharedMaterials = materials;
                GameObjectUtility.SetStaticEditorFlags(plant, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);
                planted++;
            }

            // A second, looser row of trees just outside the lawn, on the sand, so the edge reads as a tree line.
            next = 0f;
            travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                travelled += Vector3.Distance(points[i - 1].position, points[i].position);
                if (travelled < next)
                    continue;
                next = travelled + k_OuterSpacing * (0.75f + 0.5f * (float)random.NextDouble());

                var (position, inward, open) = points[i];
                if (open)
                    continue;
                position -= inward * (1.4f + 1.4f * (float)random.NextDouble());
                if (!SandHeight(position, out float y) || IsBlocked(position))
                    continue;
                position.y = y;

                bool isPine = random.NextDouble() < 0.5;
                var plant = new GameObject($"{(isPine ? "Pine" : "Tree")} {planted + 1}");
                plant.transform.SetParent(root, false);
                plant.transform.position = position;
                plant.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                plant.transform.localScale = Vector3.one * (1f + 0.5f * (float)random.NextDouble());
                plant.AddComponent<MeshFilter>().sharedMesh = isPine ? pine : round;
                plant.AddComponent<MeshRenderer>().sharedMaterials = isPine ? new[] { trunk, leaves } : new[] { trunk, leavesLight };
                GameObjectUtility.SetStaticEditorFlags(plant, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);
                planted++;
            }

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Tree Ring");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[Nature] Planted {planted} trees and bushes around the lawn. Save the scene to keep them.");
        }

        [MenuItem("XR Classroom/Environment/Build Tree Ring", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// Walks around the lawn from its centre and finds its outer edge, every half degree. Each point says
        /// which way is inward and whether a path or other ground leads away there (an opening to keep clear).
        /// The environment meshes have no colliders, so temporary mesh colliders are added and removed again.
        /// </summary>
        static List<(Vector3 position, Vector3 inward, bool open)> FindEdge()
        {
            var lawn = new List<MeshCollider>();
            var other = new List<MeshCollider>();
            var points = new List<(Vector3, Vector3, bool)>();
            try
            {
                foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
                        continue;
                    var list = k_LawnMeshes.Contains(filter.name) ? lawn : k_OtherGroundMeshes.Contains(filter.name) ? other : null;
                    if (list == null || filter.transform.parent == null || filter.transform.parent.name != "environment")
                        continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    list.Add(collider);
                }
                Physics.SyncTransforms();
                if (lawn.Count == 0)
                    return points;

                for (float degrees = 0f; degrees < 360f; degrees += 0.5f)
                {
                    float radians = degrees * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                    var center = new Vector3(k_LawnCenter.x, 0f, k_LawnCenter.y);

                    // March outward until the lawn ends.
                    float edge = -1f;
                    float ground = 0f;
                    for (float r = 2f; r < 30f; r += 0.1f)
                    {
                        if (GroundHeight(center + direction * r, lawn, out float y))
                        {
                            edge = r;
                            ground = y;
                        }
                        else if (edge > 0f)
                        {
                            break;
                        }
                    }
                    if (edge < 0f)
                        continue;

                    var position = center + direction * edge;
                    position.y = ground;
                    // An opening: other ground continues just past the lawn's edge.
                    bool open = GroundHeight(center + direction * (edge + 0.8f), other, out _) ||
                                GroundHeight(center + direction * (edge + 2f), other, out _);
                    points.Add((position, -direction, open));
                }
            }
            finally
            {
                foreach (var collider in lawn.Concat(other))
                    Object.DestroyImmediate(collider);
                Physics.SyncTransforms();
            }

            // Widen each opening by a couple of metres on both sides so paths stay clear.
            var widened = new List<(Vector3 position, Vector3 inward, bool open)>(points);
            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].Item3)
                    continue;
                for (int j = -8; j <= 8; j++)
                {
                    int k = (i + j + points.Count) % points.Count;
                    widened[k] = (widened[k].position, widened[k].inward, true);
                }
            }
            if (widened.Count > 0)
                widened.Add(widened[0]); // Close the loop.
            return widened;
        }

        static bool GroundHeight(Vector3 point, List<MeshCollider> colliders, out float height)
        {
            var ray = new Ray(new Vector3(point.x, 50f, point.z), Vector3.down);
            foreach (var collider in colliders)
            {
                if (collider.Raycast(ray, out var hit, 100f))
                {
                    height = hit.point.y;
                    return true;
                }
            }
            height = 0f;
            return false;
        }

        /// <summary>
        /// True if an exhibit, table or other solid object stands where a plant would go. The environment's own
        /// colliders (invisible boundary walls along the edge, teleport floors) don't count.
        /// </summary>
        static bool IsBlocked(Vector3 position)
        {
            var environment = FindInScene("Environment");
            var hits = Physics.OverlapSphere(position + Vector3.up * 1.3f, 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (environment == null || !hit.transform.IsChildOf(environment))
                    return true;
            }
            return false;
        }

        // ---------- Generated low-poly meshes (flat shaded: every triangle has its own vertices) ----------

        /// <summary>About 2.6 m tall: a hexagonal trunk and two stacked six-sided cones.</summary>
        static Mesh PineTreeMesh()
        {
            var builder = new MeshBuilder(2);
            builder.Cone(0, 6, 0f, 0.9f, 0.1f, 0.08f);
            builder.Cone(1, 6, 0.6f, 1.9f, 0.8f, 0f);
            builder.Cone(1, 6, 1.3f, 2.6f, 0.58f, 0f, twist: 0.5f / 6f);
            return builder.ToMesh("PineTree");
        }

        /// <summary>About 2.5 m tall: a trunk under a lumpy icosahedron canopy.</summary>
        static Mesh RoundTreeMesh()
        {
            var builder = new MeshBuilder(2);
            builder.Cone(0, 6, 0f, 1.2f, 0.12f, 0.09f);
            builder.Blob(1, new Vector3(0f, 1.7f, 0f), new Vector3(0.85f, 0.75f, 0.85f), 7);
            return builder.ToMesh("RoundTree");
        }

        /// <summary>About 0.6 m tall: a squashed, lumpy icosahedron.</summary>
        static Mesh BushMesh()
        {
            var builder = new MeshBuilder(1);
            builder.Blob(0, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.38f, 0.55f), 3);
            return builder.ToMesh("Bush");
        }
    }
}
