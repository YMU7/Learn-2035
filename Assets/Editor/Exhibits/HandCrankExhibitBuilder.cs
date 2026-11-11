using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRClassroom.Exhibits;
using XRClassroom.Exhibits.Circuits;
using XRClassroom.Exhibits.Generator;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Builds the hand crank generator exhibit: a box with a crank on its right side, wired to a bulb on a
    /// stand. Inside the box (shown in the last step) a magnet spins between two copper coils. An energy
    /// sign at the front lights up along the chain mechanical -> electrical -> light.
    /// </summary>
    static class HandCrankExhibitBuilder
    {
        const string k_RootName = "HandCrankExhibit";
        const string k_MaterialFolder = "Assets/Exhibits/HandCrank/Materials";

        // The generator box, in "Experiment" space (bench top is y = 0).
        static readonly Vector3 k_BoxCenter = new Vector3(0.15f, 0.13f, 0.05f);
        static readonly Vector3 k_BoxSize = new Vector3(0.22f, 0.26f, 0.18f);
        const float k_AxleHeight = 0.15f;
        const float k_ArmLength = 0.11f;

        [MenuItem("XR Classroom/Exhibits/Build Hand Crank Exhibit")]
        static void BuildMenu() => Build(useDefaultPose: false);

        [MenuItem("XR Classroom/Exhibits/Build Hand Crank Exhibit", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Build(bool useDefaultPose)
        {
            if (!CheckDemoSceneIsActive("build the hand crank exhibit"))
                return;

            var placement = BeginExhibit(k_RootName, ExhibitLayout.HandCrankPosition, ExhibitLayout.Yaw, useDefaultPose);
            var root = new GameObject(k_RootName).transform;
            BuildContents(root);
            FinishExhibit(root, placement, "HAND CRANK GENERATOR", new Color(0.55f, 1f, 0.3f));
        }

        static void BuildContents(Transform root)
        {
            var materials = new SharedMaterials();
            string own = k_MaterialFolder;
            var housing = LitMaterial($"{own}/Housing.mat", new Color(0.25f, 0.45f, 0.75f), smoothness: 0.35f);
            var copper = LitMaterial($"{own}/CoilCopper.mat", new Color(0.85f, 0.47f, 0.28f), metallic: 0.8f, smoothness: 0.5f);
            var coilCore = LitMaterial($"{own}/CoilCore.mat", new Color(0.35f, 0.35f, 0.38f), metallic: 0.6f, smoothness: 0.4f);
            var north = LitMaterial($"{own}/MagnetNorth.mat", new Color(0.85f, 0.15f, 0.12f), smoothness: 0.4f);
            var south = LitMaterial($"{own}/MagnetSouth.mat", new Color(0.15f, 0.35f, 0.85f), smoothness: 0.4f);
            var handle = LitMaterial($"{own}/CrankHandle.mat", new Color(0.95f, 0.6f, 0.1f), smoothness: 0.4f);
            // Emission on (black) so the sign can glow through a property block.
            var signPanel = LitMaterial($"{own}/EnergyPanel.mat", new Color(1f, 0.97f, 0.88f), smoothness: 0.2f, emission: Color.black);

            var experiment = ExhibitFrame(root, materials);
            var guide = Guide(experiment, root, materials);

            // ---- Generator box ----
            var box = Empty("Generator", experiment, Vector3.zero).transform;
            Vector3 c = k_BoxCenter;
            Vector3 s = k_BoxSize;
            const float wall = 0.008f;
            Primitive(PrimitiveType.Cube, "Bottom", box, new Vector3(c.x, wall * 0.5f, c.z), new Vector3(s.x, wall, s.z), housing);
            Primitive(PrimitiveType.Cube, "Back", box, new Vector3(c.x, c.y, c.z + s.z * 0.5f), new Vector3(s.x, s.y, wall), housing);
            Primitive(PrimitiveType.Cube, "Left", box, new Vector3(c.x - s.x * 0.5f, c.y, c.z), new Vector3(wall, s.y, s.z), housing);
            Primitive(PrimitiveType.Cube, "Right", box, new Vector3(c.x + s.x * 0.5f, c.y, c.z), new Vector3(wall, s.y, s.z), housing);
            var top = Primitive(PrimitiveType.Cube, "Cover Top", box, new Vector3(c.x, s.y, c.z), new Vector3(s.x, wall, s.z), housing);
            var front = Primitive(PrimitiveType.Cube, "Cover Front", box, new Vector3(c.x, c.y, c.z - s.z * 0.5f), new Vector3(s.x, s.y, wall), housing);
            Label("Box Label", front.transform.parent, new Vector3(c.x, c.y, c.z - s.z * 0.5f - wall), Quaternion.identity,
                "GENERATOR", 0.28f, new Vector2(0.2f, 0.04f), Color.white).transform.SetParent(front.transform, true);

            // Inside: a bar magnet on the axle (red north, blue south) between two copper coils.
            var axleCenter = new Vector3(c.x, k_AxleHeight, c.z);
            Primitive(PrimitiveType.Cylinder, "Axle", box, axleCenter, new Vector3(0.012f, s.x * 0.5f + 0.03f, 0.012f), materials.metal,
                keepCollider: false, localRotation: Quaternion.Euler(0f, 0f, 90f));
            var magnet = Empty("Magnet", box, axleCenter).transform;
            Primitive(PrimitiveType.Cube, "North", magnet, new Vector3(0f, 0.025f, 0f), new Vector3(0.04f, 0.05f, 0.03f), north, keepCollider: false);
            Primitive(PrimitiveType.Cube, "South", magnet, new Vector3(0f, -0.025f, 0f), new Vector3(0.04f, 0.05f, 0.03f), south, keepCollider: false);
            var flat = Quaternion.Euler(0f, 0f, 0f);
            Label("N", magnet, new Vector3(0f, 0.03f, -0.0155f), flat, "N", 0.3f, new Vector2(0.03f, 0.03f), Color.white);
            Label("S", magnet, new Vector3(0f, -0.03f, -0.0155f), flat, "S", 0.3f, new Vector2(0.03f, 0.03f), Color.white);

            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? k_AxleHeight + 0.075f : k_AxleHeight - 0.075f;
                var coil = Empty(i == 0 ? "Coil Top" : "Coil Bottom", box, new Vector3(c.x, y, c.z)).transform;
                Primitive(PrimitiveType.Cylinder, "Core", coil, Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), coilCore, keepCollider: false);
                // Windings: a stack of copper rings around the core.
                for (int r = 0; r < 4; r++)
                    Primitive(PrimitiveType.Cylinder, $"Winding {r + 1}", coil, new Vector3(0f, -0.018f + r * 0.012f, 0f),
                        new Vector3(0.06f, 0.005f, 0.06f), copper, keepCollider: false);
            }

            // Output posts on a small block on the box's left side: + (front) and - (back).
            Vector3 postBlock = new Vector3(c.x - s.x * 0.5f - 0.03f, 0f, c.z);
            Primitive(PrimitiveType.Cube, "Post Block", box, postBlock + new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.04f, 0.1f), materials.darkPlastic);
            var plusPost = Empty("Post +", box, postBlock + new Vector3(0f, 0.04f, -0.03f)).transform;
            var minusPost = Empty("Post -", box, postBlock + new Vector3(0f, 0.04f, 0.03f)).transform;
            Primitive(PrimitiveType.Cylinder, "Post", plusPost, new Vector3(0f, 0.01f, 0f), new Vector3(0.014f, 0.01f, 0.014f), materials.clipRed, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Post", minusPost, new Vector3(0f, 0.01f, 0f), new Vector3(0.014f, 0.01f, 0.014f), materials.clipBlack, keepCollider: false);
            var plusTop = Empty("Top", plusPost, new Vector3(0f, 0.02f, 0f)).transform;
            var minusTop = Empty("Top", minusPost, new Vector3(0f, 0.02f, 0f)).transform;

            // The current's route inside the box: from the - post, round the bottom coil, up and round the
            // top coil, and out to the + post.
            float cx = c.x, cz = c.z, lo = k_AxleHeight - 0.075f, hi = k_AxleHeight + 0.075f;
            var coilPath = new[]
            {
                Empty("Coil Path 1", box, minusTop.parent.localPosition + new Vector3(0f, 0.02f, 0f)).transform,
                Empty("Coil Path 2", box, new Vector3(c.x - s.x * 0.5f + 0.02f, lo, cz + 0.04f)).transform,
                Empty("Coil Path 3", box, new Vector3(cx, lo, cz + 0.035f)).transform,
                Empty("Coil Path 4", box, new Vector3(cx + 0.035f, lo, cz)).transform,
                Empty("Coil Path 5", box, new Vector3(cx, lo, cz - 0.035f)).transform,
                Empty("Coil Path 6", box, new Vector3(cx - 0.035f, lo, cz)).transform,
                Empty("Coil Path 7", box, new Vector3(cx - 0.045f, hi, cz)).transform,
                Empty("Coil Path 8", box, new Vector3(cx, hi, cz + 0.035f)).transform,
                Empty("Coil Path 9", box, new Vector3(cx + 0.035f, hi, cz)).transform,
                Empty("Coil Path 10", box, new Vector3(cx, hi, cz - 0.035f)).transform,
                Empty("Coil Path 11", box, new Vector3(c.x - s.x * 0.5f + 0.02f, hi, cz - 0.04f)).transform,
                Empty("Coil Path 12", box, plusTop.parent.localPosition + new Vector3(0f, 0.02f, 0f)).transform,
            };

            // ---- Crank on the right side ----
            var pivot = Empty("Crank", box, new Vector3(c.x + s.x * 0.5f + 0.02f, k_AxleHeight, c.z)).transform;
            Primitive(PrimitiveType.Cylinder, "Hub", pivot, Vector3.zero, new Vector3(0.03f, 0.008f, 0.03f), materials.metal,
                keepCollider: false, localRotation: Quaternion.Euler(0f, 0f, 90f));
            Primitive(PrimitiveType.Cube, "Arm", pivot, new Vector3(0.012f, k_ArmLength * 0.5f, 0f), new Vector3(0.012f, k_ArmLength + 0.02f, 0.022f),
                materials.metal, keepCollider: false);
            var knob = Primitive(PrimitiveType.Cylinder, "Knob", pivot, new Vector3(0.05f, k_ArmLength, 0f), new Vector3(0.03f, 0.035f, 0.03f),
                handle, keepCollider: false, localRotation: Quaternion.Euler(0f, 0f, 90f));
            var knobCollider = knob.AddComponent<BoxCollider>();
            knobCollider.size = new Vector3(1.8f, 2.2f, 1.8f); // Generous, so the knob is easy to grab.
            var knobInteractable = knob.AddComponent<XRSimpleInteractable>();
            var crank = box.gameObject.AddComponent<HandCrank>();
            Set(crank, "m_Pivot", pivot);
            Set(crank, "m_Knob", knobInteractable);
            var crankSync = box.gameObject.AddComponent<HandCrankSync>();
            Set(crankSync, "m_Crank", crank);
            Label("Crank Label", experiment, new Vector3(c.x + s.x * 0.5f + 0.07f, 0.001f, -0.1f), Quaternion.Euler(90f, 0f, 0f),
                "Turn the crank!", 0.2f, new Vector2(0.18f, 0.03f), TextColor);

            // ---- Bulb on a riser, wired to the posts ----
            var riser = Primitive(PrimitiveType.Cube, "Bulb Riser", experiment, new Vector3(-0.3f, 0.04f, 0.05f), new Vector3(0.14f, 0.08f, 0.08f),
                materials.white);
            var bulb = Bulb(experiment, "Bulb", new Vector3(-0.3f, 0.08f, 0.05f), materials, acceptsLoosePlugs: false);
            var wireOut = FixedWire(experiment, "Wire Red (+)", plusTop, bulb.terminalB.transform, 0.36f, materials.wireRed, experiment);
            var wireBack = FixedWire(experiment, "Wire Black (-)", bulb.terminalA.transform, minusTop, 0.36f, materials.wireBlack, experiment);
            // The circuit's current is drawn as one loop, so the bulb's own spheres are not needed.
            Object.DestroyImmediate(bulb.flow.gameObject);
            Set(bulb, "m_Flow", null);
            ReversePath(bulb);

            var flow = Flow(experiment, "Current", true, materials, diameter: 0.01f, spacing: 0.03f);

            // ---- Energy sign along the front of the bench (shown in the last step) ----
            var sign = Empty("Energy Sign", experiment, new Vector3(-0.22f, 0.05f, -0.22f), Quaternion.Euler(-60f, 0f, 0f));
            var glows = new System.Collections.Generic.List<Renderer>();
            string[] steps =
            {
                "<b>1. MECHANICAL</b>\nYour arm turns the crank",
                "<b>2. ELECTRICAL</b>\nThe spinning magnet pushes charges through the coils",
                "<b>3. LIGHT</b>\nThe bulb glows",
            };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = -0.28f + 0.28f * i;
                var panel = Primitive(PrimitiveType.Cube, $"Panel {i + 1}", sign.transform, new Vector3(x, 0f, 0f), new Vector3(0.22f, 0.006f, 0.1f),
                    signPanel, keepCollider: false);
                glows.Add(panel.GetComponent<Renderer>());
                Label($"Text {i + 1}", sign.transform, new Vector3(x, 0.0035f, 0f), Quaternion.Euler(90f, 0f, 0f), steps[i], 0.17f,
                    new Vector2(0.2f, 0.085f), TextColor);
                if (i < steps.Length - 1)
                {
                    var arrow = Label($"Arrow {i + 1}", sign.transform, new Vector3(x + 0.14f, 0.0035f, 0f), Quaternion.Euler(90f, 0f, 0f),
                        "→", 0.5f, new Vector2(0.05f, 0.05f), new Color(0.9f, 0.5f, 0.05f));
                    arrow.fontStyle = TMPro.FontStyles.Bold;
                }
            }

            var exhibit = experiment.gameObject.AddComponent<CrankGeneratorExhibit>();
            Set(exhibit, "m_Guide", guide);
            Set(exhibit, "m_Crank", crank);
            Set(exhibit, "m_Bulb", bulb);
            Set(exhibit, "m_Magnet", magnet);
            Set(exhibit, "m_WireOut", wireOut);
            Set(exhibit, "m_WireBack", wireBack);
            SetArray(exhibit, "m_CoilPath", coilPath);
            Set(exhibit, "m_Flow", flow);
            SetArray(exhibit, "m_Cover", new Object[] { top, front });
            Set(exhibit, "m_EnergySign", sign);
            SetArray(exhibit, "m_EnergyGlow", glows.ToArray());
            sign.SetActive(false);
        }

        /// <summary>
        /// The red wire arrives at the bulb's post B, so the current runs through the bulb from B to A:
        /// reverse its path to match.
        /// </summary>
        static void ReversePath(LightBulb bulb)
        {
            var serialized = new SerializedObject(bulb);
            var path = serialized.FindProperty("m_Path");
            int n = path.arraySize;
            var items = new Object[n];
            for (int i = 0; i < n; i++)
                items[i] = path.GetArrayElementAtIndex(n - 1 - i).objectReferenceValue;
            for (int i = 0; i < n; i++)
                path.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
