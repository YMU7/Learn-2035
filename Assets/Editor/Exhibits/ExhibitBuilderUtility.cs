using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using XRClassroom.Exhibits;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Shared building blocks for exhibit builder menu items: generated materials, primitives,
    /// the white lab bench and the blank billboards. Every exhibit uses the same scale:
    /// 1 unit = 1 m, matching the avatar and XR rig, with the bench top at hand height.
    /// </summary>
    static class ExhibitBuilderUtility
    {
        public const string DemoScenePath = "Assets/Scenes/DemoClass.unity";
        public const string SharedMaterialFolder = "Assets/Exhibits/Shared/Materials";

        /// <summary>Bench top height (m). Standing adults reach it comfortably with relaxed arms.</summary>
        public const float BenchTopHeight = 0.9f;

        /// <summary>Exhibits are only built into the DemoClass scene. Logs an error and returns false otherwise.</summary>
        public static bool CheckDemoSceneIsActive(string action)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == DemoScenePath)
                return true;
            Debug.LogError($"[Exhibits] Open {DemoScenePath} to {action}. The active scene is '{scene.path}'.");
            return false;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        /// <summary>Creates or updates a URP Lit material asset. Updating in place keeps references (and GUIDs) stable.</summary>
        public static Material LitMaterial(string path, Color color, float metallic = 0f, float smoothness = 0.25f, Color? emission = null)
        {
            var material = LoadOrCreate(path, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Creates or updates a URP Unlit material asset (flat color, unaffected by lighting).</summary>
        public static Material UnlitMaterial(string path, Color color)
        {
            var material = LoadOrCreate(path, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LoadOrCreate(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            return go;
        }

        /// <summary>A Unity primitive. Set <paramref name="keepCollider"/> false for purely visual parts.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = true, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Finds a GameObject by exact name anywhere in the active scene, including inactive ones.</summary>
        public static Transform FindInScene(string name)
        {
            return EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name);
        }

        /// <summary>Height of the walkable ground (the first solid collider, ignoring triggers) under a point.</summary>
        public static bool TryFindGround(Vector3 position, out float height)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(position.x, 50f, position.z), Vector3.down, out var hit, 100f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = 0f;
            return false;
        }

        /// <summary>
        /// A plain white lab bench: top, four legs, front skirt and a backsplash (which holds the exhibit's title).
        /// Local y = 0 is the floor. Legs reach a little below it so they meet the visible ground.
        /// </summary>
        public static GameObject Bench(Transform parent, Vector3 localPosition, float width, float depth, Material material)
        {
            const float topThickness = 0.04f;
            const float legSize = 0.05f;
            const float legSink = 0.05f;
            float top = BenchTopHeight;

            var bench = Empty("Bench", parent, localPosition);
            var t = bench.transform;
            Primitive(PrimitiveType.Cube, "Top", t, new Vector3(0f, top - topThickness * 0.5f, 0f),
                new Vector3(width, topThickness, depth), material);

            float legHeight = top - topThickness + legSink;
            float legY = (top - topThickness - legSink) * 0.5f;
            float legX = width * 0.5f - legSize;
            float legZ = depth * 0.5f - legSize;
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -legX : legX;
                float z = i < 2 ? -legZ : legZ;
                Primitive(PrimitiveType.Cube, $"Leg {i + 1}", t, new Vector3(x, legY, z),
                    new Vector3(legSize, legHeight, legSize), material);
            }

            Primitive(PrimitiveType.Cube, "Front Skirt", t, new Vector3(0f, top - topThickness - 0.04f, -depth * 0.5f + 0.01f),
                new Vector3(width, 0.08f, 0.02f), material);
            Primitive(PrimitiveType.Cube, "Backsplash", t, new Vector3(0f, top + 0.225f, depth * 0.5f - 0.01f),
                new Vector3(width, 0.45f, 0.02f), material);
            return bench;
        }

        /// <summary>
        /// A free-standing blank white billboard: a 1.6 m x 0.9 m (16:9) screen with its centre at eye level,
        /// on two posts. Its <see cref="ExhibitBillboard"/> takes the slide image. It faces local -z.
        /// </summary>
        public static ExhibitBillboard Billboard(Transform parent, string name, Vector3 localPosition, float yaw,
            Material frameMaterial, Material screenMaterial)
        {
            const float screenWidth = 1.6f;
            const float screenHeight = 0.9f;
            const float centerHeight = 1.55f;
            const float frameMargin = 0.05f;
            const float frameThickness = 0.04f;
            const float postSink = 0.05f;

            var go = Empty(name, parent, localPosition, Quaternion.Euler(0f, yaw, 0f));
            var t = go.transform;

            Primitive(PrimitiveType.Cube, "Frame", t, new Vector3(0f, centerHeight, frameThickness * 0.5f),
                new Vector3(screenWidth + frameMargin * 2f, screenHeight + frameMargin * 2f, frameThickness), frameMaterial);

            float postTop = centerHeight;
            float postHeight = postTop + postSink;
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.6f : 0.6f;
                Primitive(PrimitiveType.Cylinder, i == 0 ? "Post Left" : "Post Right", t,
                    new Vector3(x, (postTop - postSink) * 0.5f, frameThickness + 0.03f),
                    new Vector3(0.05f, postHeight * 0.5f, 0.05f), frameMaterial);
                Primitive(PrimitiveType.Cube, i == 0 ? "Foot Left" : "Foot Right", t,
                    new Vector3(x, 0f, frameThickness + 0.03f), new Vector3(0.12f, 0.06f, 0.4f), frameMaterial);
            }

            // Quads face -z, toward visitors standing in front of the billboard.
            var screen = Primitive(PrimitiveType.Quad, "Screen", t, new Vector3(0f, centerHeight, -0.002f),
                new Vector3(screenWidth, screenHeight, 1f), screenMaterial, keepCollider: false);
            var screenRenderer = screen.GetComponent<MeshRenderer>();
            screenRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var billboard = go.AddComponent<ExhibitBillboard>();
            billboard.screen = screenRenderer;
            return billboard;
        }

        /// <summary>
        /// World-space text. Faces local -z with an identity rotation. Use Euler(90, 0, 0) to lay it flat
        /// on a surface, readable from the front. Font size 1 is roughly 0.1 m tall.
        /// </summary>
        public static TextMeshPro Label(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            string text, float fontSize, Vector2 size, Color color)
        {
            var go = Empty(name, parent, localPosition, localRotation);
            var label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        /// <summary>Sets a serialized field (including private [SerializeField] ones) on a component.</summary>
        public static void Set(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Exhibits] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            switch (value)
            {
                case Object reference: property.objectReferenceValue = reference; break;
                case null: property.objectReferenceValue = null; break;
                case float number: property.floatValue = number; break;
                case int number: property.intValue = number; break;
                case bool flag: property.boolValue = flag; break;
                case System.Enum enumValue: property.enumValueIndex = System.Convert.ToInt32(enumValue); break;
                default:
                    Debug.LogError($"[Exhibits] Unsupported value type {value.GetType().Name} for '{field}'.");
                    return;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
