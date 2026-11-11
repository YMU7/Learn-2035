using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRClassroom.Exhibits;
using XRClassroom.Exhibits.PotatoBattery;
using static XRClassroom.EditorTools.Exhibits.ExhibitBuilderUtility;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Builds the potato battery exhibit into the DemoClass scene: a white lab bench with a grabbable potato,
    /// zinc and copper electrodes wired to an LED, a current-flow visualization, a reset button, and a blank
    /// billboard on each side. Running it again rebuilds the exhibit in the same place and keeps any slide
    /// images already assigned to its billboards. Move the "PotatoBatteryExhibit" root to relocate it.
    /// </summary>
    static class PotatoBatteryExhibitBuilder
    {
        const string k_RootName = "PotatoBatteryExhibit";
        const string k_MaterialFolder = "Assets/Exhibits/PotatoBattery/Materials";

        const float k_WireLength = 0.6f;

        static Color k_TextColor => TextColor;

        [MenuItem("XR Classroom/Exhibits/Build Potato Battery Exhibit")]
        static void BuildMenu() => Build(useDefaultPose: false);

        /// <summary>
        /// Builds (or rebuilds) the exhibit. A rebuild stays where the old one was, unless
        /// <paramref name="useDefaultPose"/> is set: then it goes to its spot in <see cref="ExhibitLayout"/>.
        /// </summary>
        public static void Build(bool useDefaultPose)
        {
            if (!CheckDemoSceneIsActive("build the potato battery exhibit"))
                return;

            var placement = BeginExhibit(k_RootName, ExhibitLayout.PotatoBatteryPosition, ExhibitLayout.Yaw, useDefaultPose);
            var root = new GameObject(k_RootName).transform;
            BuildContents(root);
            FinishExhibit(root, placement, "POTATO BATTERY LAB", new Color(1f, 0.6f, 0.2f));
        }

        [MenuItem("XR Classroom/Exhibits/Build Potato Battery Exhibit", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static void BuildContents(Transform root)
        {
            // Materials: shared ones for every exhibit, specific ones for this experiment.
            var materials = new SharedMaterials();
            string own = k_MaterialFolder;
            var charge = materials.charge;
            var potatoMaterial = LitMaterial($"{own}/Potato.mat", new Color(0.62f, 0.45f, 0.25f), smoothness: 0.15f);
            var zinc = LitMaterial($"{own}/Zinc.mat", new Color(0.66f, 0.68f, 0.72f), metallic: 0.9f, smoothness: 0.55f);
            var copper = LitMaterial($"{own}/Copper.mat", new Color(0.85f, 0.47f, 0.28f), metallic: 0.9f, smoothness: 0.6f);
            var clipRed = LitMaterial($"{own}/ClipRed.mat", new Color(0.8f, 0.08f, 0.06f), smoothness: 0.4f);
            var clipBlack = LitMaterial($"{own}/ClipBlack.mat", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.4f);
            var ledBase = LitMaterial($"{own}/LedBase.mat", new Color(0.2f, 0.2f, 0.22f));
            var ledOff = LitMaterial($"{own}/LedOff.mat", new Color(0.45f, 0.06f, 0.06f), smoothness: 0.85f);
            var ledOn = LitMaterial($"{own}/LedOn.mat", new Color(1f, 0.25f, 0.2f), smoothness: 0.85f,
                emission: new Color(0.9f, 0.08f, 0.04f)); // Kept low: the scene's bloom turns brighter values into a big halo.
            var wireRed = UnlitMaterial($"{own}/WireRed.mat", new Color(0.85f, 0.1f, 0.08f));
            var wireBlack = UnlitMaterial($"{own}/WireBlack.mat", new Color(0.06f, 0.06f, 0.06f));

            // Bench, billboards, and "Experiment": everything on the bench lives under it (local y = 0 is the bench top).
            var experiment = ExhibitFrame(root, materials);
            var guide = Guide(experiment, root, materials);

            var potato = BuildPotato(experiment, root, potatoMaterial);
            var led = BuildLed(experiment, ledBase, ledOff, clipRed, clipBlack, zinc,
                out var ledPositive, out var ledNegative, out var bulbTop, out var bulb, out var ledLight);

            // Zinc lies on the left pointing at the potato, copper on the right. Red (+) and black (-) clips.
            var zincElectrode = BuildElectrode("Zinc Electrode (-)", experiment, root, new Vector3(-0.28f, 0f, -0.12f),
                ElectrodeMetal.Zinc, zinc, clipBlack, potato, ledNegative);
            var copperElectrode = BuildElectrode("Copper Electrode (+)", experiment, root, new Vector3(0.28f, 0f, -0.12f),
                ElectrodeMetal.Copper, copper, clipRed, potato, ledPositive);

            // Conventional current: copper (+) -> red wire -> LED -> black wire -> zinc (-).
            var positiveWire = BuildWire("Wire Red (+)", experiment, copperElectrode.terminal, ledPositive, wireRed);
            var negativeWire = BuildWire("Wire Black (-)", experiment, ledNegative, zincElectrode.terminal, wireBlack);

            var flow = Empty("Current Flow", experiment, Vector3.zero).AddComponent<CurrentFlowVisualizer>();
            Set(flow, "m_ChargeMaterial", charge);

            // Name tags lying flat on the bench in front of each piece.
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Label("Zinc Label", experiment, new Vector3(-0.28f, 0.001f, -0.24f), flat,
                "Zinc (-)\nanode", 0.22f, new Vector2(0.25f, 0.08f), k_TextColor);
            Label("Copper Label", experiment, new Vector3(0.28f, 0.001f, -0.24f), flat,
                "Copper (+)\ncathode", 0.22f, new Vector2(0.25f, 0.08f), k_TextColor);
            Label("Potato Label", experiment, new Vector3(0f, 0.001f, -0.2f), flat,
                "Potato", 0.22f, new Vector2(0.25f, 0.05f), k_TextColor);

            var circuit = experiment.gameObject.AddComponent<PotatoBatteryCircuit>();
            Set(circuit, "m_Zinc", zincElectrode);
            Set(circuit, "m_Copper", copperElectrode);
            Set(circuit, "m_PositiveWire", positiveWire);
            Set(circuit, "m_NegativeWire", negativeWire);
            Set(circuit, "m_LedPositiveTerminal", ledPositive);
            Set(circuit, "m_LedBulbTop", bulbTop);
            Set(circuit, "m_LedNegativeTerminal", ledNegative);
            Set(circuit, "m_LedBulb", bulb);
            Set(circuit, "m_LedOffMaterial", ledOff);
            Set(circuit, "m_LedOnMaterial", ledOn);
            Set(circuit, "m_LedLight", ledLight);
            Set(circuit, "m_CurrentFlow", flow);
            Set(circuit, "m_Guide", guide);
        }

        static void SetUpGrabbable(GameObject go, Transform exhibitRoot, float mass, bool throwOnDetach) =>
            Grabbable(go, exhibitRoot, mass, throwOnDetach);

        static Potato BuildPotato(Transform experiment, Transform exhibitRoot, Material material)
        {
            // About 12 x 7 x 8.5 cm. Resting on its flattest side keeps it from rolling.
            var go = Empty("Potato", experiment, new Vector3(0f, 0.035f, -0.08f), Quaternion.Euler(0f, 15f, 0f));
            var shape = Primitive(PrimitiveType.Sphere, "Shape", go.transform, Vector3.zero,
                new Vector3(0.12f, 0.07f, 0.085f), material, keepCollider: false);
            var collider = shape.AddComponent<MeshCollider>();
            collider.sharedMesh = shape.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;

            SetUpGrabbable(go, exhibitRoot, 0.25f, throwOnDetach: true);
            var potato = go.AddComponent<Potato>();
            Set(potato, "m_Shape", shape.transform);
            return potato;
        }

        static GameObject BuildLed(Transform experiment, Material baseMaterial, Material offMaterial,
            Material positiveMaterial, Material negativeMaterial, Material legMaterial,
            out Transform positiveTerminal, out Transform negativeTerminal, out Transform bulbTop,
            out Renderer bulb, out Light light)
        {
            // A kinematic body so the electrode wires can be tethered to it.
            var led = Empty("LED", experiment, new Vector3(0f, 0f, 0.2f));
            var body = led.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var t = led.transform;

            Primitive(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.04f, 0.06f), baseMaterial);
            Primitive(PrimitiveType.Cylinder, "Post (+)", t, new Vector3(0.04f, 0.05f, 0f),
                new Vector3(0.012f, 0.01f, 0.012f), positiveMaterial, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Post (-)", t, new Vector3(-0.04f, 0.05f, 0f),
                new Vector3(0.012f, 0.01f, 0.012f), negativeMaterial, keepCollider: false);
            positiveTerminal = Empty("Terminal (+)", t, new Vector3(0.04f, 0.06f, 0f)).transform;
            negativeTerminal = Empty("Terminal (-)", t, new Vector3(-0.04f, 0.06f, 0f)).transform;

            // Legs: down from the bulb, then across to each post.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Primitive(PrimitiveType.Cylinder, i == 0 ? "Leg (+)" : "Leg (-)", t, new Vector3(0.006f * side, 0.064f, 0f),
                    new Vector3(0.002f, 0.006f, 0.002f), legMaterial, keepCollider: false);
                Primitive(PrimitiveType.Cube, i == 0 ? "Lead (+)" : "Lead (-)", t, new Vector3(0.023f * side, 0.059f, 0f),
                    new Vector3(0.036f, 0.002f, 0.002f), legMaterial, keepCollider: false);
            }

            // A 3 cm LED: a bit larger than life so it reads clearly from a step back.
            var bulbObject = Primitive(PrimitiveType.Capsule, "Bulb", t, new Vector3(0f, 0.085f, 0f),
                new Vector3(0.03f, 0.0175f, 0.03f), offMaterial, keepCollider: false);
            bulb = bulbObject.GetComponent<Renderer>();
            bulb.shadowCastingMode = ShadowCastingMode.Off;
            bulbTop = Empty("Bulb Top", t, new Vector3(0f, 0.102f, 0f)).transform;

            var lightObject = Empty("Glow", t, new Vector3(0f, 0.095f, 0f));
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.15f);
            // Faint on purpose: lit white surfaces near it easily pass the scene's bloom threshold and flare.
            light.range = 0.2f;
            light.intensity = 0.05f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            Label("Sign (+)", t, new Vector3(0.04f, 0.02f, -0.031f), Quaternion.identity, "+", 0.35f,
                new Vector2(0.03f, 0.04f), new Color(0.95f, 0.95f, 0.95f));
            Label("Sign (-)", t, new Vector3(-0.04f, 0.02f, -0.031f), Quaternion.identity, "-", 0.35f,
                new Vector2(0.03f, 0.04f), new Color(0.95f, 0.95f, 0.95f));
            return led;
        }

        static Electrode BuildElectrode(string name, Transform experiment, Transform exhibitRoot, Vector3 position,
            ElectrodeMetal metal, Material metalMaterial, Material clipMaterial, Potato potato, Transform wireAnchor)
        {
            // Built along local y: tip (bottom) -> 9 cm of metal -> 3.5 cm clip -> terminal (top).
            // Laid flat on the bench with the tip pointing toward the potato in the middle.
            const float halfLength = 0.0625f;
            const float restHeight = 0.009f;
            float roll = metal == ElectrodeMetal.Zinc ? 90f : -90f;
            var go = Empty(name, experiment, position + Vector3.up * restHeight, Quaternion.Euler(0f, 0f, roll));
            var t = go.transform;

            if (metal == ElectrodeMetal.Zinc)
            {
                // A galvanized nail.
                Primitive(PrimitiveType.Cylinder, "Metal", t, new Vector3(0f, -0.0175f, 0f),
                    new Vector3(0.008f, 0.045f, 0.008f), metalMaterial, keepCollider: false);
            }
            else
            {
                // A flat copper strip, lying flat.
                Primitive(PrimitiveType.Cube, "Metal", t, new Vector3(0f, -0.0175f, 0f),
                    new Vector3(0.003f, 0.09f, 0.018f), metalMaterial, keepCollider: false);
            }
            Primitive(PrimitiveType.Cube, "Clip", t, new Vector3(0f, 0.045f, 0f),
                new Vector3(0.016f, 0.035f, 0.016f), clipMaterial, keepCollider: false);
            var tip = Empty("Tip", t, new Vector3(0f, -halfLength, 0f)).transform;
            var terminal = Empty("Terminal", t, new Vector3(0f, halfLength, 0f)).transform;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.018f, halfLength * 2f, 0.018f);

            SetUpGrabbable(go, exhibitRoot, 0.02f, throwOnDetach: false);
            var electrode = go.AddComponent<Electrode>();
            Set(electrode, "m_Metal", metal);
            Set(electrode, "m_Tip", tip);
            Set(electrode, "m_Terminal", terminal);
            Set(electrode, "m_Potato", potato);
            Set(electrode, "m_WireAnchor", wireAnchor);
            Set(electrode, "m_WireLength", k_WireLength);
            return electrode;
        }

        static CircuitWire BuildWire(string name, Transform experiment, Transform from, Transform to, Material material)
        {
            var go = Empty(name, experiment, Vector3.zero);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = 0.005f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;

            var wire = go.AddComponent<CircuitWire>();
            Set(wire, "m_From", from);
            Set(wire, "m_To", to);
            Set(wire, "m_Length", k_WireLength);
            Set(wire, "m_RestSurface", experiment);
            return wire;
        }
    }
}
