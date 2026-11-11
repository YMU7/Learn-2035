using UnityEditor;
using UnityEngine;
using XRClassroom.Exhibits;
using XRClassroom.Exhibits.Circuits;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Builds the series vs. parallel exhibit: one wiring board with a SERIES side and a PARALLEL side (each a
    /// battery and two bulbs) and eight loose wires along its front edge, so students can wire both and compare
    /// them side by side. Rebuilding keeps the exhibit's position and billboard slides.
    /// </summary>
    static class SeriesParallelExhibitBuilder
    {
        const string k_RootName = "SeriesParallelExhibit";
        const float k_WireLength = 0.45f;
        const int k_WireCount = 8;

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

            // One board across the bench: series side on the left, parallel side on the right.
            var board = Empty("Wiring Board", experiment, new Vector3(0f, 0f, -0.03f));
            var t = board.transform;
            const float top = 0.01f;
            Primitive(PrimitiveType.Cube, "Base", t, new Vector3(0f, top * 0.5f, 0f), new Vector3(1.24f, top, 0.44f), materials.board);
            Primitive(PrimitiveType.Cube, "Divider", t, new Vector3(0f, top + 0.0005f, 0f), new Vector3(0.006f, 0.001f, 0.42f),
                materials.darkPlastic, keepCollider: false);

            var flat = Quaternion.Euler(90f, 0f, 0f);
            var sectionColor = new Color(0.1f, 0.3f, 0.7f);
            Label("Series Title", t, new Vector3(-0.31f, top + 0.001f, 0.18f), flat, "SERIES", 0.45f, new Vector2(0.5f, 0.06f), sectionColor)
                .fontStyle = TMPro.FontStyles.Bold;
            Label("Parallel Title", t, new Vector3(0.31f, top + 0.001f, 0.18f), flat, "PARALLEL", 0.45f, new Vector2(0.5f, 0.06f), sectionColor)
                .fontStyle = TMPro.FontStyles.Bold;

            var seriesBattery = Battery(t, "Series Battery", new Vector3(-0.31f, top, -0.08f), materials,
                out var seriesMinus, out var seriesPlus, out var seriesPath);
            var parallelBattery = Battery(t, "Parallel Battery", new Vector3(0.31f, top, -0.08f), materials,
                out var parallelMinus, out var parallelPlus, out var parallelPath);
            var s1 = Bulb(t, "Series Bulb 1", new Vector3(-0.46f, top, 0.08f), materials);
            var s2 = Bulb(t, "Series Bulb 2", new Vector3(-0.16f, top, 0.08f), materials);
            var p1 = Bulb(t, "Parallel Bulb 1", new Vector3(0.16f, top, 0.08f), materials);
            var p2 = Bulb(t, "Parallel Bulb 2", new Vector3(0.46f, top, 0.08f), materials);
            foreach (var (bulb, text) in new[] { (s1, "1"), (s2, "2"), (p1, "1"), (p2, "2") })
                Label($"{bulb.name} Label", t, bulb.transform.localPosition + new Vector3(0f, 0.001f, -0.045f), flat, $"Bulb {text}", 0.18f,
                    new Vector2(0.1f, 0.025f), TextColor);
            var seriesFlow = Flow(seriesBattery.transform, "Current", false, materials, diameter: 0.008f, spacing: 0.02f);
            var parallelFlow = Flow(parallelBattery.transform, "Current", false, materials, diameter: 0.008f, spacing: 0.02f);

            // Loose wires lying along the board's front edge, ready to be picked up.
            var wires = new CircuitWireLink[k_WireCount];
            for (int i = 0; i < k_WireCount; i++)
            {
                float x = -0.525f + 0.15f * i;
                wires[i] = WireLink(experiment, $"Wire {i + 1}", new Vector3(x - 0.045f, top, -0.22f), new Vector3(x + 0.045f, top, -0.22f),
                    k_WireLength, materials.wireColors[i % materials.wireColors.Length], root, experiment, materials);
            }

            var circuit = board.AddComponent<CircuitBoard>();
            var serialized = new SerializedObject(circuit);
            var batteries = serialized.FindProperty("m_Batteries");
            batteries.arraySize = 2;
            SetBattery(batteries.GetArrayElementAtIndex(0), seriesPlus, seriesMinus, seriesPath, seriesFlow);
            SetBattery(batteries.GetArrayElementAtIndex(1), parallelPlus, parallelMinus, parallelPath, parallelFlow);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetArray(circuit, "m_Bulbs", new Object[] { s1, s2, p1, p2 });
            SetArray(circuit, "m_Wires", wires);

            var exhibit = experiment.gameObject.AddComponent<SeriesParallelExhibit>();
            Set(exhibit, "m_Guide", guide);
            Set(exhibit, "m_Board", circuit);
            Set(exhibit, "m_SeriesBulb1", s1);
            Set(exhibit, "m_SeriesBulb2", s2);
            Set(exhibit, "m_ParallelBulb1", p1);
            Set(exhibit, "m_ParallelBulb2", p2);
        }

        static void SetBattery(SerializedProperty element, CircuitTerminal plus, CircuitTerminal minus, Transform[] path, CurrentFlowVisualizer flow)
        {
            element.FindPropertyRelative("positive").objectReferenceValue = plus;
            element.FindPropertyRelative("negative").objectReferenceValue = minus;
            element.FindPropertyRelative("flow").objectReferenceValue = flow;
            var points = element.FindPropertyRelative("path");
            points.arraySize = path.Length;
            for (int i = 0; i < path.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = path[i];
        }
    }
}
