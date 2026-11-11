using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Set dressing for DemoClass, built from primitives and generated low-poly meshes (no textures):
    /// <list type="bullet">
    /// <item>A potato truck parked behind the potato battery lab: a low-poly flatbed with an open container
    /// heaped with (non-interactive) potatoes. The story: students fetch fresh potatoes from it on every reset.</item>
    /// <item>A campus club festival entrance at the spawn point: an arch with a big "Learn-2035 XR" sign,
    /// bunting, string lights, balloons, and two club tents.</item>
    /// </list>
    /// Decor has no scripts and is marked static. Each menu item rebuilds its piece in place.
    /// </summary>
    static class CampusDecorBuilder
    {
        const string k_Folder = "Assets/Exhibits/Campus";

        // North of the potato lab, cab facing east, container nearest the lab.
        static readonly Vector3 k_TruckPosition = new Vector3(0.8f, 0f, 13f);
        const float k_TruckYaw = 0f;

        // Straight ahead of the spawn point, facing it.
        static readonly Vector3 k_ArchPosition = new Vector3(-0.7f, 0f, 1.9f);

        [MenuItem("XR Classroom/Environment/Build Potato Truck")]
        static void BuildTruck()
        {
            if (!CheckDemoSceneIsActive("build the potato truck"))
                return;
            var root = Begin("Potato Truck", k_TruckPosition, k_TruckYaw);
            Truck(root);
            Finish(root);
        }

        [MenuItem("XR Classroom/Environment/Build Festival Entrance")]
        static void BuildEntrance()
        {
            if (!CheckDemoSceneIsActive("build the festival entrance"))
                return;
            var root = Begin("Festival Entrance", k_ArchPosition, 0f);
            Entrance(root);
            Finish(root);
        }

        // In the open lawn in front of the hand crank lab, facing it.
        static readonly Vector3[] k_BenchPositions = { new Vector3(17.5f, 0f, 4.4f), new Vector3(20.3f, 0f, 4.4f) };

        [MenuItem("XR Classroom/Environment/Build Benches")]
        static void BuildBenches()
        {
            if (!CheckDemoSceneIsActive("build the benches"))
                return;
            var existing = FindInScene("Benches");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);
            var root = new GameObject("Benches").transform;
            var wood = LitMaterial($"{k_Folder}/Materials/BenchWood.mat", new Color(0.62f, 0.42f, 0.25f), smoothness: 0.25f);
            var frame = LitMaterial($"{k_Folder}/Materials/BenchFrame.mat", new Color(0.18f, 0.2f, 0.22f), metallic: 0.5f, smoothness: 0.4f);
            for (int i = 0; i < k_BenchPositions.Length; i++)
            {
                var position = k_BenchPositions[i];
                if (LowPolyMeshes.VisibleGroundHeight(position, out float ground))
                    position.y = ground;
                // Built facing local -z; turned around so people sitting on it face the lab to the north.
                var bench = Empty($"Bench {i + 1}", root, position, Quaternion.Euler(0f, 180f, 0f)).transform;
                Bench(bench, wood, frame);
            }
            Finish(root);
        }

        /// <summary>A 1.6 m park bench: three seat slats, two back slats, dark metal side frames and armrests.</summary>
        static void Bench(Transform bench, Material wood, Material frame)
        {
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cube, "Seat Slat", bench, new Vector3(0f, 0.45f, -0.14f + 0.13f * i), new Vector3(1.6f, 0.04f, 0.11f), wood);
            for (int i = 0; i < 2; i++)
                Part(PrimitiveType.Cube, "Back Slat", bench, new Vector3(0f, 0.62f + 0.16f * i, 0.23f + 0.02f * i), new Vector3(1.6f, 0.1f, 0.035f), wood,
                    new Vector3(-10f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.72f * side;
                Part(PrimitiveType.Cube, "Front Leg", bench, new Vector3(x, 0.225f, -0.17f), new Vector3(0.06f, 0.45f, 0.06f), frame);
                Part(PrimitiveType.Cube, "Back Leg", bench, new Vector3(x, 0.44f, 0.22f), new Vector3(0.06f, 0.88f, 0.06f), frame, new Vector3(-6f, 0f, 0f));
                Part(PrimitiveType.Cube, "Seat Rail", bench, new Vector3(x, 0.415f, 0.02f), new Vector3(0.05f, 0.04f, 0.46f), frame);
                Part(PrimitiveType.Cube, "Armrest", bench, new Vector3(x, 0.66f, 0f), new Vector3(0.07f, 0.04f, 0.45f), frame);
                Part(PrimitiveType.Cube, "Arm Post", bench, new Vector3(x, 0.55f, -0.17f), new Vector3(0.05f, 0.2f, 0.05f), frame);
            }
            // One box so players and the tree ring treat the bench as solid.
            var collider = bench.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0.03f);
            collider.size = new Vector3(1.65f, 0.9f, 0.5f);
        }

        [MenuItem("XR Classroom/Environment/Build Benches", true)]
        [MenuItem("XR Classroom/Environment/Build Potato Truck", true)]
        [MenuItem("XR Classroom/Environment/Build Festival Entrance", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static Transform Begin(string name, Vector3 defaultPosition, float defaultYaw)
        {
            var position = defaultPosition;
            var rotation = Quaternion.Euler(0f, defaultYaw, 0f);
            var existing = FindInScene(name);
            if (existing != null)
            {
                position = existing.position;
                rotation = existing.rotation;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
            else if (LowPolyMeshes.VisibleGroundHeight(position, out float ground))
            {
                position.y = ground;
            }
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(position, rotation);
            return root;
        }

        static void Finish(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(root.gameObject, $"Build {root.name}");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[Campus] Built {root.name} at {root.position}. Save the scene to keep it.", root);
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material,
            Vector3 euler = default) =>
            Primitive(type, name, parent, position, scale, material, keepCollider: false, localRotation: Quaternion.Euler(euler));

        // ---------------------------------------------------------------- Truck

        /// <summary>
        /// A medium-duty truck about 7 m long, built along local +x (cab at the front, +x). Instead of a flatbed it
        /// carries an open container heaped with potatoes.
        /// </summary>
        static void Truck(Transform root)
        {
            var paint = LitMaterial($"{k_Folder}/Materials/TruckPaint.mat", new Color(0.78f, 0.42f, 0.36f), smoothness: 0.35f);
            var glass = LitMaterial($"{k_Folder}/Materials/TruckGlass.mat", new Color(0.18f, 0.22f, 0.27f), smoothness: 0.85f);
            var tire = LitMaterial($"{k_Folder}/Materials/Tire.mat", new Color(0.1f, 0.1f, 0.1f), smoothness: 0.15f);
            var hub = LitMaterial($"{k_Folder}/Materials/Hub.mat", new Color(0.82f, 0.74f, 0.6f), metallic: 0.4f, smoothness: 0.4f);
            var chassis = LitMaterial($"{k_Folder}/Materials/Chassis.mat", new Color(0.16f, 0.15f, 0.14f), smoothness: 0.2f);
            var chrome = LitMaterial($"{k_Folder}/Materials/Chrome.mat", new Color(0.8f, 0.8f, 0.82f), metallic: 0.9f, smoothness: 0.7f);
            var amber = LitMaterial($"{k_Folder}/Materials/Amber.mat", new Color(1f, 0.6f, 0.1f), smoothness: 0.6f,
                emission: new Color(0.6f, 0.3f, 0.02f));
            var container = LitMaterial($"{k_Folder}/Materials/Container.mat", new Color(0.33f, 0.43f, 0.39f), smoothness: 0.3f);
            var rib = LitMaterial($"{k_Folder}/Materials/ContainerRib.mat", new Color(0.27f, 0.36f, 0.32f), smoothness: 0.3f);
            var potatoSkin = LitMaterial("Assets/Exhibits/PotatoBattery/Materials/Potato.mat", new Color(0.62f, 0.45f, 0.25f), smoothness: 0.15f);

            var body = Empty("Body", root, Vector3.zero).transform;

            // Chassis rails and fuel tank.
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Cube, "Rail", body, new Vector3(-0.2f, 0.8f, 0.45f * side), new Vector3(6.6f, 0.22f, 0.14f), chassis);
            Part(PrimitiveType.Cube, "Fuel Tank", body, new Vector3(1.6f, 0.68f, -1.0f), new Vector3(0.85f, 0.45f, 0.38f), chassis);

            // Cab: lower body, upper cabin with windows, roof, roof marker lights.
            Part(PrimitiveType.Cube, "Cab Lower", body, new Vector3(1.8f, 1.45f, 0f), new Vector3(1.6f, 0.9f, 2.2f), paint);
            Part(PrimitiveType.Cube, "Cab Upper", body, new Vector3(1.72f, 2.25f, 0f), new Vector3(1.44f, 0.7f, 2.1f), paint);
            Part(PrimitiveType.Cube, "Roof", body, new Vector3(1.72f, 2.63f, 0f), new Vector3(1.5f, 0.07f, 2.16f), paint);
            Part(PrimitiveType.Cube, "Windshield", body, new Vector3(2.445f, 2.25f, 0f), new Vector3(0.02f, 0.52f, 1.86f), glass);
            Part(PrimitiveType.Cube, "Rear Window", body, new Vector3(0.995f, 2.3f, 0f), new Vector3(0.02f, 0.36f, 1.2f), glass);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cube, "Side Window", body, new Vector3(1.78f, 2.25f, 1.055f * side), new Vector3(1.0f, 0.46f, 0.02f), glass);
                Part(PrimitiveType.Cube, "Door Line", body, new Vector3(1.15f, 1.6f, 1.105f * side), new Vector3(0.02f, 1.1f, 0.01f), chassis);
                Part(PrimitiveType.Cube, "Handle", body, new Vector3(1.3f, 1.75f, 1.11f * side), new Vector3(0.18f, 0.04f, 0.02f), chrome);
                // Mirror on an arm.
                Part(PrimitiveType.Cube, "Mirror Arm", body, new Vector3(2.35f, 2.05f, 1.2f * side), new Vector3(0.03f, 0.03f, 0.25f), chrome);
                Part(PrimitiveType.Cube, "Mirror", body, new Vector3(2.35f, 2.15f, 1.33f * side), new Vector3(0.05f, 0.4f, 0.14f), chrome);
            }
            for (int i = 0; i < 5; i++)
                Part(PrimitiveType.Cube, "Marker Light", body, new Vector3(2.38f, 2.69f, -0.6f + 0.3f * i), new Vector3(0.1f, 0.05f, 0.08f), amber);

            // Hood, grille, bumper, headlights, fenders.
            Part(PrimitiveType.Cube, "Hood", body, new Vector3(3.0f, 1.42f, 0f), new Vector3(0.95f, 0.8f, 2.0f), paint);
            Part(PrimitiveType.Cube, "Hood Top", body, new Vector3(2.95f, 1.84f, 0f), new Vector3(0.85f, 0.06f, 1.85f), paint);
            Part(PrimitiveType.Cube, "Grille", body, new Vector3(3.48f, 1.4f, 0f), new Vector3(0.02f, 0.55f, 1.3f), chassis);
            Part(PrimitiveType.Cube, "Bumper", body, new Vector3(3.55f, 0.88f, 0f), new Vector3(0.12f, 0.22f, 2.3f), chrome);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cylinder, "Headlight", body, new Vector3(3.49f, 1.45f, 0.82f * side), new Vector3(0.18f, 0.02f, 0.18f), chrome,
                    new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cube, "Turn Signal", body, new Vector3(3.47f, 1.18f, 0.88f * side), new Vector3(0.04f, 0.08f, 0.12f), amber);
                Part(PrimitiveType.Cube, "Fender", body, new Vector3(2.85f, 1.12f, 1.0f * side), new Vector3(1.15f, 0.12f, 0.38f), paint);
            }

            // Headache rack (the frame behind the cab).
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Cube, "Rack Post", body, new Vector3(0.93f, 1.85f, 0.95f * side), new Vector3(0.07f, 1.5f, 0.07f), chassis);
            Part(PrimitiveType.Cube, "Rack Top", body, new Vector3(0.93f, 2.58f, 0f), new Vector3(0.07f, 0.07f, 1.97f), chassis);

            // Wheels: single at the front, dual at the back.
            Wheel(body, new Vector3(2.85f, 0.5f, 1.0f), tire, hub, 1);
            Wheel(body, new Vector3(2.85f, 0.5f, -1.0f), tire, hub, -1);
            foreach (float z in new[] { 0.98f, 0.66f })
            {
                Wheel(body, new Vector3(-1.7f, 0.5f, z), tire, hub, 1);
                Wheel(body, new Vector3(-1.7f, 0.5f, -z), tire, hub, -1);
            }

            // The container: floor, walls, ribs, top rail.
            var box = Empty("Container", root, Vector3.zero).transform;
            const float x0 = -3.4f, x1 = 0.85f, floorY = 1.1f, wallTop = 2.1f, halfWidth = 1.2f;
            float length = x1 - x0, midX = (x0 + x1) * 0.5f, wallH = wallTop - floorY, wallY = (floorY + wallTop) * 0.5f;
            Part(PrimitiveType.Cube, "Floor", box, new Vector3(midX, floorY - 0.05f, 0f), new Vector3(length, 0.1f, halfWidth * 2f), container);
            Part(PrimitiveType.Cube, "Wall Left", box, new Vector3(midX, wallY, -halfWidth), new Vector3(length, wallH, 0.06f), container);
            Part(PrimitiveType.Cube, "Wall Right", box, new Vector3(midX, wallY, halfWidth), new Vector3(length, wallH, 0.06f), container);
            Part(PrimitiveType.Cube, "Wall Back", box, new Vector3(x0, wallY, 0f), new Vector3(0.06f, wallH, halfWidth * 2f), container);
            Part(PrimitiveType.Cube, "Wall Front", box, new Vector3(x1, wallY, 0f), new Vector3(0.06f, wallH, halfWidth * 2f), container);
            for (float x = x0 + 0.35f; x < x1 - 0.2f; x += 0.5f)
            {
                Part(PrimitiveType.Cube, "Rib", box, new Vector3(x, wallY, -halfWidth - 0.04f), new Vector3(0.06f, wallH, 0.04f), rib);
                Part(PrimitiveType.Cube, "Rib", box, new Vector3(x, wallY, halfWidth + 0.04f), new Vector3(0.06f, wallH, 0.04f), rib);
            }
            Part(PrimitiveType.Cube, "Top Rail", box, new Vector3(midX, wallTop + 0.03f, -halfWidth), new Vector3(length + 0.06f, 0.06f, 0.1f), rib);
            Part(PrimitiveType.Cube, "Top Rail", box, new Vector3(midX, wallTop + 0.03f, halfWidth), new Vector3(length + 0.06f, 0.06f, 0.1f), rib);
            var sign = Label("Sign", box, new Vector3(midX, wallY, -halfWidth - 0.07f), Quaternion.identity,
                "<b>FRESH POTATOES</b>\n<size=60%>for science! Help yourself at every reset</size>", 2.2f, new Vector2(3.6f, 0.8f),
                new Color(0.98f, 0.94f, 0.82f));
            sign.fontStyle = TMPro.FontStyles.Normal;

            // The heap: a lumpy mound filling the container, covered in individual potatoes.
            var heapMesh = LowPolyMeshes.Save($"{k_Folder}/Meshes/PotatoHeap.asset", Heap());
            var heap = new GameObject("Potato Heap");
            heap.transform.SetParent(box, false);
            heap.transform.localPosition = new Vector3(midX, 0f, 0f);
            heap.AddComponent<MeshFilter>().sharedMesh = heapMesh;
            heap.AddComponent<MeshRenderer>().sharedMaterial = potatoSkin;

            var potatoMesh = LowPolyMeshes.Save($"{k_Folder}/Meshes/Potato.asset", PotatoMesh());
            var random = new System.Random(42);
            var heapCenter = new Vector3(0f, k_HeapCenterY, 0f);
            for (int i = 0; i < 90; i++)
            {
                // Somewhere on the mound's top surface.
                float u = (float)random.NextDouble() * 2f - 1f;
                float v = (float)random.NextDouble() * 2f - 1f;
                float s = u * u + v * v;
                if (s > 0.92f)
                {
                    i--;
                    continue;
                }
                float y = heapCenter.y + k_HeapRadii.y * Mathf.Sqrt(1f - s);
                var potato = new GameObject($"Potato {i + 1}");
                potato.transform.SetParent(heap.transform, false);
                potato.transform.localPosition = new Vector3(u * k_HeapRadii.x, y + 0.02f, v * k_HeapRadii.z);
                potato.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 360f);
                potato.transform.localScale = Vector3.one * (0.12f + 0.05f * (float)random.NextDouble());
                potato.AddComponent<MeshFilter>().sharedMesh = potatoMesh;
                var renderer = potato.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = potatoSkin;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            // One box collider so players (and the tree ring) treat the truck as solid.
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.05f, 1.35f, 0f);
            collider.size = new Vector3(7.1f, 2.7f, 2.5f);
        }

        const float k_HeapCenterY = 1.65f;
        static readonly Vector3 k_HeapRadii = new Vector3(1.95f, 0.6f, 1.05f);

        static Mesh Heap()
        {
            var builder = new MeshBuilder();
            builder.Blob(0, new Vector3(0f, k_HeapCenterY, 0f), k_HeapRadii, 11, lumpiness: 0.1f);
            return builder.ToMesh("PotatoHeap");
        }

        static Mesh PotatoMesh()
        {
            var builder = new MeshBuilder();
            builder.Blob(0, Vector3.zero, new Vector3(0.5f, 0.36f, 0.4f), 5, lumpiness: 0.3f);
            return builder.ToMesh("Potato");
        }

        static void Wheel(Transform parent, Vector3 position, Material tire, Material hub, int side)
        {
            var wheel = Empty("Wheel", parent, position).transform;
            Part(PrimitiveType.Cylinder, "Tire", wheel, Vector3.zero, new Vector3(1.0f, 0.15f, 1.0f), tire, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Hub", wheel, new Vector3(0f, 0f, 0.13f * side), new Vector3(0.58f, 0.03f, 0.58f), hub, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Hub Cap", wheel, new Vector3(0f, 0f, 0.17f * side), new Vector3(0.16f, 0.03f, 0.16f), hub, new Vector3(90f, 0f, 0f));
        }

        // ---------------------------------------------------------------- Festival entrance

        static void Entrance(Transform root)
        {
            var navy = LitMaterial($"{k_Folder}/Materials/SignNavy.mat", new Color(0.12f, 0.2f, 0.5f), smoothness: 0.3f,
                emission: new Color(0.03f, 0.05f, 0.12f));
            var whiteTrim = LitMaterial($"{k_Folder}/Materials/Trim.mat", new Color(0.97f, 0.97f, 0.95f), smoothness: 0.3f,
                emission: new Color(0.3f, 0.3f, 0.3f));
            var pillarBlue = LitMaterial($"{k_Folder}/Materials/PillarBlue.mat", new Color(0.2f, 0.45f, 0.85f), smoothness: 0.3f);
            var pillarOrange = LitMaterial($"{k_Folder}/Materials/PillarOrange.mat", new Color(0.98f, 0.6f, 0.15f), smoothness: 0.3f);
            var rope = LitMaterial($"{k_Folder}/Materials/Rope.mat", new Color(0.2f, 0.2f, 0.22f));
            var bulb = LitMaterial($"{k_Folder}/Materials/StringLight.mat", new Color(1f, 0.92f, 0.7f), smoothness: 0.8f,
                emission: new Color(1.1f, 0.9f, 0.5f));
            Color[] palette =
            {
                new Color(0.95f, 0.3f, 0.3f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.6f, 0.95f),
                new Color(0.35f, 0.8f, 0.45f), new Color(0.7f, 0.4f, 0.9f),
            };
            var colors = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
                colors[i] = LitMaterial($"{k_Folder}/Materials/Festive{i + 1}.mat", palette[i], smoothness: 0.4f);

            // Pillars with alternating bands.
            const float pillarX = 1.9f, pillarHeight = 3.3f;
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = Primitive(PrimitiveType.Cube, side < 0 ? "Pillar Left" : "Pillar Right", root,
                    new Vector3(pillarX * side, pillarHeight * 0.5f, 0f), new Vector3(0.5f, pillarHeight, 0.5f), whiteTrim);
                for (int b = 0; b < 4; b++)
                    Part(PrimitiveType.Cube, "Band", pillar.transform.parent, new Vector3(pillarX * side, 0.5f + b * 0.7f, 0f),
                        new Vector3(0.52f, 0.22f, 0.52f), b % 2 == 0 ? pillarBlue : pillarOrange);
                Part(PrimitiveType.Cube, "Footing", root, new Vector3(pillarX * side, 0.06f, 0f), new Vector3(0.7f, 0.12f, 0.7f), navy);
            }

            // The big sign across the top, readable from both sides.
            const float signY = 3.75f;
            Primitive(PrimitiveType.Cube, "Sign Board", root, new Vector3(0f, signY, 0f), new Vector3(4.7f, 1.0f, 0.16f), navy);
            Part(PrimitiveType.Cube, "Sign Trim Top", root, new Vector3(0f, signY + 0.52f, 0f), new Vector3(4.8f, 0.06f, 0.2f), whiteTrim);
            Part(PrimitiveType.Cube, "Sign Trim Bottom", root, new Vector3(0f, signY - 0.52f, 0f), new Vector3(4.8f, 0.06f, 0.2f), whiteTrim);
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? -0.09f : 0.09f;
                var rotation = face == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                var title = Label(face == 0 ? "Title Front" : "Title Back", root, new Vector3(0f, signY + 0.1f, z), rotation,
                    "Learn-2035 XR", 6.5f, new Vector2(4.5f, 0.7f), Color.white);
                title.fontStyle = TMPro.FontStyles.Bold;
                title.enableAutoSizing = true; // Shrinks to fit the board on one line.
                title.fontSizeMin = 2f;
                title.fontSizeMax = 6.5f;
                Label(face == 0 ? "Subtitle Front" : "Subtitle Back", root, new Vector3(0f, signY - 0.33f, z), rotation,
                    "CAMPUS CLUB FESTIVAL  •  ELECTROMAGNETISM LABS", 1.6f, new Vector2(4.5f, 0.2f), new Color(1f, 0.82f, 0.3f));
            }

            // Bunting and string lights between the pillars, sagging a little.
            BuntingString(root, new Vector3(-pillarX, 3.0f, -0.12f), new Vector3(pillarX, 3.0f, -0.12f), 0.35f, rope, colors, null, 13);
            BuntingString(root, new Vector3(-pillarX, 3.1f, 0.12f), new Vector3(pillarX, 3.1f, 0.12f), 0.25f, rope, null, bulb, 15);

            // Balloon clusters beside the sign ends.
            var random = new System.Random(7);
            for (int side = -1; side <= 1; side += 2)
            {
                var anchor = new Vector3((pillarX + 0.3f) * side, 3.0f, 0f);
                for (int i = 0; i < 4; i++)
                {
                    var offset = new Vector3((0.25f + 0.35f * (float)random.NextDouble()) * side, 0.7f + 0.6f * (float)random.NextDouble(),
                        -0.25f + 0.5f * (float)random.NextDouble());
                    var top = anchor + offset;
                    Part(PrimitiveType.Sphere, "Balloon", root, top, new Vector3(0.36f, 0.44f, 0.36f), colors[(i + (side > 0 ? 2 : 0)) % colors.Length]);
                    // String from the pillar to the balloon.
                    var mid = (anchor + top - Vector3.up * 0.22f) * 0.5f;
                    var dir = top - Vector3.up * 0.22f - anchor;
                    Part(PrimitiveType.Cylinder, "Balloon String", root, mid, new Vector3(0.008f, dir.magnitude * 0.5f, 0.008f), rope,
                        Quaternion.FromToRotation(Vector3.up, dir).eulerAngles);
                }
            }

            // Two club tents just inside the lawn, either side of the path.
            Tent(root, "Physics Club Tent", new Vector3(-2.6f, 0f, 2.3f), colors[0], whiteTrim, "PHYSICS CLUB");
            Tent(root, "Robotics Club Tent", new Vector3(2.6f, 0f, 2.3f), colors[2], whiteTrim, "XR & ROBOTICS CLUB");
        }

        /// <summary>A sagging string between two points with pennant flags (if colours are given) or light bulbs.</summary>
        static void BuntingString(Transform parent, Vector3 a, Vector3 b, float sag, Material rope, Material[] flagColors, Material bulb, int count)
        {
            var line = Empty("Bunting", parent, Vector3.zero);
            var renderer = line.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.sharedMaterial = rope;
            renderer.widthMultiplier = 0.012f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            const int segments = 24;
            renderer.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
                renderer.SetPosition(i, Sag(a, b, sag, i / (float)segments));

            var pennant = flagColors != null ? LowPolyMeshes.Save($"{k_Folder}/Meshes/Pennant.asset", PennantMesh()) : null;
            for (int i = 1; i < count; i++)
            {
                var point = Sag(a, b, sag, i / (float)count);
                if (pennant != null)
                {
                    var flag = new GameObject("Pennant");
                    flag.transform.SetParent(line.transform, false);
                    flag.transform.localPosition = point;
                    flag.AddComponent<MeshFilter>().sharedMesh = pennant;
                    flag.AddComponent<MeshRenderer>().sharedMaterial = flagColors[i % flagColors.Length];
                }
                else
                {
                    var light = Part(PrimitiveType.Sphere, "Bulb", line.transform, point - Vector3.up * 0.05f, Vector3.one * 0.07f, bulb);
                    light.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        static Vector3 Sag(Vector3 a, Vector3 b, float sag, float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));

        /// <summary>A downward-pointing triangle flag, visible from both sides.</summary>
        static Mesh PennantMesh()
        {
            var builder = new MeshBuilder();
            var left = new Vector3(-0.12f, 0f, 0f);
            var right = new Vector3(0.12f, 0f, 0f);
            var tip = new Vector3(0f, -0.28f, 0f);
            builder.Triangle(0, left, right, tip);
            builder.Triangle(0, left, tip, right);
            return builder.ToMesh("Pennant");
        }

        /// <summary>A pop-up canopy tent (2 m square) with a table and a club banner.</summary>
        static void Tent(Transform parent, string name, Vector3 position, Material canopy, Material white, string club)
        {
            var tent = Empty(name, parent, position).transform;
            const float half = 1.0f, legHeight = 2.1f;
            var legMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{SharedMaterialFolder}/Metal.mat");
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -half : half;
                float z = i < 2 ? -half : half;
                Part(PrimitiveType.Cylinder, "Leg", tent, new Vector3(x, legHeight * 0.5f, z), new Vector3(0.05f, legHeight * 0.5f, 0.05f), legMaterial);
            }
            var roofMesh = LowPolyMeshes.Save($"{k_Folder}/Meshes/TentRoof.asset", RoofMesh());
            var roof = new GameObject("Roof");
            roof.transform.SetParent(tent, false);
            roof.transform.localPosition = new Vector3(0f, legHeight, 0f);
            roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            roof.AddComponent<MeshRenderer>().sharedMaterial = canopy;
            // Valance skirts around the roof edge.
            for (int i = 0; i < 4; i++)
            {
                var rotation = Quaternion.Euler(0f, 90f * i, 0f);
                var skirt = Part(PrimitiveType.Cube, "Valance", tent, rotation * new Vector3(0f, legHeight - 0.1f, -half), new Vector3(2.0f, 0.2f, 0.02f),
                    i % 2 == 0 ? canopy : white);
                skirt.transform.localRotation = rotation;
            }
            // Table with a cloth and the club's name on the front.
            Primitive(PrimitiveType.Cube, "Table", tent, new Vector3(0f, 0.74f, -0.3f), new Vector3(1.5f, 0.05f, 0.6f), white);
            Part(PrimitiveType.Cube, "Table Cloth", tent, new Vector3(0f, 0.45f, -0.6f), new Vector3(1.5f, 0.55f, 0.02f), canopy);
            Label("Club Banner", tent, new Vector3(0f, 0.47f, -0.615f), Quaternion.identity, club, 1.3f, new Vector2(1.4f, 0.3f), Color.white)
                .fontStyle = TMPro.FontStyles.Bold;
            Label("Valance Text", tent, new Vector3(0f, legHeight - 0.1f, -half - 0.015f), Quaternion.identity, club, 0.9f,
                new Vector2(1.9f, 0.18f), Color.white).fontStyle = TMPro.FontStyles.Bold;
        }

        /// <summary>A square pyramid roof, 2 m across and 0.6 m tall, open underneath but visible from below.</summary>
        static Mesh RoofMesh()
        {
            var builder = new MeshBuilder();
            builder.Cone(0, 4, 0f, 0.6f, 1.414f, 0f, twist: 0.125f);
            return builder.ToMesh("TentRoof");
        }
    }
}
