using UnityEditor;
using UnityEngine;
using XRClassroom.Exhibits;
using XRClassroom.Exhibits.Circuits;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Builds the series vs. parallel exhibit: a wiring board at the front (battery, two bulbs, five loose
    /// wires) and, at the back, two pairs of example circuits (A and B) for the "which one is correct?"
    /// questions. Rebuilding keeps the exhibit's position and billboard slides.
    /// </summary>
    static class SeriesParallelExhibitBuilder
    {
        const string k_RootName = "SeriesParallelExhibit";
        const float k_FreeWireLength = 0.45f;

        [MenuItem("XR Classroom/Exhibits/Build Series vs Parallel Exhibit")]
        static void BuildMenu() => Build(useDefaultPose: false);

        [MenuItem("XR Classroom/Exhibits/Build Series vs Parallel Exhibit", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Build(bool useDefaultPose)
        {
            if (!CheckDemoSceneIsActive("build the series vs parallel exhibit"))
                return;

            var placement = BeginExhibit(k_RootName, ExhibitLayout.SeriesParallelPosition, ExhibitLayout.Yaw, useDefaultPose);
            var root = new GameObject(k_RootName).transform;
            BuildContents(root);
            FinishExhibit(root, placement, "SERIES & PARALLEL CIRCUITS", new Color(0.2f, 0.75f, 1f));
        }

        static void BuildContents(Transform root)
        {
            var materials = new SharedMaterials();
            var experiment = ExhibitFrame(root, materials);
            var guide = Guide(experiment, root, materials);

            // Front: the board the student wires up.
            var freeBoard = Board(experiment, "Wiring Board", new Vector3(0f, 0f, -0.06f), new Vector3(0.8f, 0.01f, 0.3f), materials,
                root, locked: false, out var battery, out var bulbs);
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Label("Bulb 1 Label", freeBoard.transform, new Vector3(0.02f, 0.0105f, -0.06f), flat, "Bulb 1", 0.18f, new Vector2(0.1f, 0.025f), TextColor);
            Label("Bulb 2 Label", freeBoard.transform, new Vector3(0.24f, 0.0105f, -0.06f), flat, "Bulb 2", 0.18f, new Vector2(0.1f, 0.025f), TextColor);
            Label("Battery Label", freeBoard.transform, new Vector3(-0.25f, 0.0105f, -0.06f), flat, "Battery", 0.18f, new Vector2(0.1f, 0.025f), TextColor);

            // Five loose wires lying side by side along the board's front edge, ready to be picked up.
            var wires = new CircuitWireLink[5];
            for (int i = 0; i < wires.Length; i++)
            {
                float x = -0.3f + 0.15f * i;
                wires[i] = WireLink(experiment, $"Wire {i + 1}", new Vector3(x - 0.045f, 0.01f, -0.17f), new Vector3(x + 0.045f, 0.01f, -0.17f),
                    k_FreeWireLength, materials.wireColors[i], root, experiment, materials);
            }
            SetArray(freeBoard, "m_Wires", wires);

            // Back: the example circuits, two at a time.
            var seriesExamples = Empty("Series Examples", experiment, Vector3.zero);
            var seriesA = ExampleBoard(seriesExamples.transform, "Series A", -0.36f, materials, root, experiment, ExampleKind.SeriesBackToStart);
            var seriesB = ExampleBoard(seriesExamples.transform, "Series B", 0.36f, materials, root, experiment, ExampleKind.Series);
            var parallelExamples = Empty("Parallel Examples", experiment, Vector3.zero);
            var parallelA = ExampleBoard(parallelExamples.transform, "Parallel A", -0.36f, materials, root, experiment, ExampleKind.Parallel);
            var parallelB = ExampleBoard(parallelExamples.transform, "Parallel B", 0.36f, materials, root, experiment, ExampleKind.ParallelShorted);
            parallelExamples.SetActive(false);

            var exhibit = experiment.gameObject.AddComponent<SeriesParallelExhibit>();
            Set(exhibit, "m_Guide", guide);
            Set(exhibit, "m_FreeBoard", freeBoard);
            Set(exhibit, "m_SeriesExamples", seriesExamples);
            Set(exhibit, "m_SeriesA", seriesA);
            Set(exhibit, "m_SeriesB", seriesB);
            Set(exhibit, "m_ParallelExamples", parallelExamples);
            Set(exhibit, "m_ParallelA", parallelA);
            Set(exhibit, "m_ParallelB", parallelB);
        }

        /// <summary>A board with a battery on the left and two bulbs to its right.</summary>
        static CircuitBoard Board(Transform parent, string name, Vector3 position, Vector3 size, SharedMaterials materials,
            Transform exhibitRoot, bool locked, out GameObject battery, out LightBulb[] bulbs)
        {
            var board = Empty(name, parent, position);
            var t = board.transform;
            Primitive(PrimitiveType.Cube, "Base", t, new Vector3(0f, size.y * 0.5f, 0f), size, materials.board);

            float top = size.y;
            float scale = size.x / 0.8f; // Example boards are narrower: pieces sit closer together.
            battery = Battery(t, "Battery", new Vector3(-0.25f * scale, top, 0.01f), materials,
                out var negative, out var positive, out var internalPath, acceptsLoosePlugs: !locked);
            bulbs = new[]
            {
                Bulb(t, "Bulb 1", new Vector3(0.02f * scale, top, 0.05f), materials, acceptsLoosePlugs: !locked),
                Bulb(t, "Bulb 2", new Vector3(0.24f * scale, top, 0.05f), materials, acceptsLoosePlugs: !locked),
            };
            var batteryFlow = Flow(battery.transform, "Current", false, materials, diameter: 0.008f, spacing: 0.02f);

            var circuit = board.AddComponent<CircuitBoard>();
            Set(circuit, "m_BatteryPositive", positive);
            Set(circuit, "m_BatteryNegative", negative);
            SetArray(circuit, "m_BatteryPath", internalPath);
            Set(circuit, "m_BatteryFlow", batteryFlow);
            SetArray(circuit, "m_Bulbs", bulbs);
            return circuit;
        }

        enum ExampleKind
        {
            /// <summary>Wrong: the loop comes back to the + post it started from, so nothing lights.</summary>
            SeriesBackToStart,
            /// <summary>Right: one loop through both bulbs.</summary>
            Series,
            /// <summary>Right: each bulb has its own path.</summary>
            Parallel,
            /// <summary>Wrong: parallel, plus a wire straight from + to - (a short circuit).</summary>
            ParallelShorted,
        }

        /// <summary>A pre-wired, unpowered example circuit with a big A or B beside it.</summary>
        static CircuitBoard ExampleBoard(Transform parent, string name, float x, SharedMaterials materials, Transform exhibitRoot,
            Transform restSurface, ExampleKind kind)
        {
            var board = Board(parent, name, new Vector3(x, 0f, 0.2f), new Vector3(0.44f, 0.008f, 0.16f), materials, exhibitRoot,
                locked: true, out _, out var bulbs);
            var serialized = new SerializedObject(board);
            var plus = (CircuitTerminal)serialized.FindProperty("m_BatteryPositive").objectReferenceValue;
            var minus = (CircuitTerminal)serialized.FindProperty("m_BatteryNegative").objectReferenceValue;
            var b1 = bulbs[0];
            var b2 = bulbs[1];

            (CircuitTerminal from, CircuitTerminal to)[] connections = kind switch
            {
                ExampleKind.SeriesBackToStart => new[] { (plus, b1.terminalA), (b1.terminalB, b2.terminalA), (b2.terminalB, plus) },
                ExampleKind.Series => new[] { (plus, b1.terminalA), (b1.terminalB, b2.terminalA), (b2.terminalB, minus) },
                ExampleKind.Parallel => new[] { (plus, b1.terminalA), (b1.terminalA, b2.terminalA), (b1.terminalB, minus), (b2.terminalB, b1.terminalB) },
                _ => new[] { (plus, b1.terminalA), (b1.terminalA, b2.terminalA), (b1.terminalB, minus), (b2.terminalB, b1.terminalB), (plus, minus) },
            };

            var wires = new CircuitWireLink[connections.Length];
            for (int i = 0; i < connections.Length; i++)
            {
                var (from, to) = connections[i];
                float length = Vector3.Distance(from.transform.position, to.transform.position) * 1.25f + 0.02f;
                var material = from == plus && to == minus ? materials.wireRed : materials.wireColors[i % materials.wireColors.Length];
                wires[i] = WireLink(board.transform, $"Wire {i + 1}", Vector3.zero, Vector3.zero, length, material, exhibitRoot,
                    restSurface, materials, from, to, locked: true);
            }
            SetArray(board, "m_Wires", wires);
            Set(board, "m_Powered", false);

            string letter = name.EndsWith("A") ? "A" : "B";
            var label = Label($"{name} Letter", board.transform, new Vector3(-0.19f, 0.0085f, -0.055f), Quaternion.Euler(90f, 0f, 0f),
                letter, 0.7f, new Vector2(0.06f, 0.06f), new Color(0.1f, 0.3f, 0.7f));
            label.fontStyle = TMPro.FontStyles.Bold;
            return board;
        }
    }
}
