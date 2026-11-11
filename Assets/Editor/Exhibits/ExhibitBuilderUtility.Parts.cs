using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRClassroom.Exhibits;
using XRClassroom.Exhibits.Circuits;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>Materials shared by every exhibit (created or updated in place under Assets/Exhibits/Shared).</summary>
    sealed class SharedMaterials
    {
        public readonly Material white;
        public readonly Material screen;
        public readonly Material buttonCap;
        public readonly Material buttonBase;
        public readonly Material charge;
        public readonly Material metal;
        public readonly Material darkPlastic;
        public readonly Material bulbGlass;
        public readonly Material board;
        public readonly Material clipRed;
        public readonly Material clipBlack;
        public readonly Material wireRed;
        public readonly Material wireBlack;
        public readonly Material[] wireColors;

        public SharedMaterials()
        {
            string f = ExhibitBuilderUtility.SharedMaterialFolder;
            // A little self-illumination keeps the white reading as white on faces turned away from the sun.
            white = ExhibitBuilderUtility.LitMaterial($"{f}/ExhibitWhite.mat", new Color(0.95f, 0.95f, 0.95f), smoothness: 0.2f,
                emission: new Color(0.35f, 0.35f, 0.35f));
            screen = ExhibitBuilderUtility.UnlitMaterial($"{f}/BillboardScreen.mat", Color.white);
            // Buttons are recoloured per button with a property block; the material itself stays white.
            buttonCap = ExhibitBuilderUtility.LitMaterial($"{f}/ButtonCap.mat", Color.white, smoothness: 0.5f);
            buttonBase = ExhibitBuilderUtility.LitMaterial($"{f}/ButtonBase.mat", new Color(0.3f, 0.3f, 0.32f));
            charge = ExhibitBuilderUtility.LitMaterial($"{f}/CurrentCharge.mat", new Color(1f, 0f, 0f), smoothness: 0.4f,
                emission: new Color(0.6f, 0f, 0f));
            metal = ExhibitBuilderUtility.LitMaterial($"{f}/Metal.mat", new Color(0.7f, 0.72f, 0.75f), metallic: 0.9f, smoothness: 0.6f);
            darkPlastic = ExhibitBuilderUtility.LitMaterial($"{f}/DarkPlastic.mat", new Color(0.18f, 0.18f, 0.2f), smoothness: 0.3f);
            // Emission is switched on (black) so each bulb can glow through its own property block.
            bulbGlass = ExhibitBuilderUtility.LitMaterial($"{f}/BulbGlass.mat", new Color(0.85f, 0.85f, 0.8f), smoothness: 0.9f,
                emission: Color.black);
            board = ExhibitBuilderUtility.LitMaterial($"{f}/WiringBoard.mat", new Color(0.86f, 0.8f, 0.68f), smoothness: 0.2f);
            clipRed = ExhibitBuilderUtility.LitMaterial($"{f}/PlasticRed.mat", new Color(0.8f, 0.08f, 0.06f), smoothness: 0.4f);
            clipBlack = ExhibitBuilderUtility.LitMaterial($"{f}/PlasticBlack.mat", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.4f);
            wireRed = ExhibitBuilderUtility.UnlitMaterial($"{f}/WireRed.mat", new Color(0.85f, 0.1f, 0.08f));
            wireBlack = ExhibitBuilderUtility.UnlitMaterial($"{f}/WireBlack.mat", new Color(0.06f, 0.06f, 0.06f));
            wireColors = new[]
            {
                ExhibitBuilderUtility.UnlitMaterial($"{f}/WireYellow.mat", new Color(0.95f, 0.8f, 0.1f)),
                ExhibitBuilderUtility.UnlitMaterial($"{f}/WireBlue.mat", new Color(0.15f, 0.4f, 0.9f)),
                ExhibitBuilderUtility.UnlitMaterial($"{f}/WireGreen.mat", new Color(0.15f, 0.7f, 0.3f)),
                ExhibitBuilderUtility.UnlitMaterial($"{f}/WireOrange.mat", new Color(0.95f, 0.5f, 0.1f)),
                ExhibitBuilderUtility.UnlitMaterial($"{f}/WirePurple.mat", new Color(0.6f, 0.25f, 0.85f)),
            };
        }
    }

    /// <summary>Where an exhibit is being (re)built, and what to carry over from the old one.</summary>
    struct ExhibitPlacement
    {
        public Vector3 position;
        public Quaternion rotation;
        public Transform parent;
        public Dictionary<string, Texture> slides;
    }

    static partial class ExhibitBuilderUtility
    {
        public static readonly Color TextColor = new Color(0.15f, 0.15f, 0.18f);

        // Control strip buttons (left to right) and their colours.
        static readonly Color k_HintColor = new Color(0.95f, 0.72f, 0.1f);
        static readonly Color k_NextColor = new Color(0.15f, 0.68f, 0.3f);
        static readonly Color k_ResetColor = new Color(0.85f, 0.12f, 0.1f);

        /// <summary>
        /// Removes the exhibit's old root (keeping its pose and billboard slides) or, if there is none or
        /// <paramref name="useDefaultPose"/> is set, uses the default pose on the ground.
        /// </summary>
        public static ExhibitPlacement BeginExhibit(string rootName, Vector3 defaultPosition, float defaultYaw, bool useDefaultPose)
        {
            var placement = new ExhibitPlacement
            {
                position = defaultPosition,
                rotation = Quaternion.Euler(0f, defaultYaw, 0f),
                slides = new Dictionary<string, Texture>(),
            };

            var existing = FindInScene(rootName);
            if (existing != null)
            {
                if (!useDefaultPose)
                {
                    placement.position = existing.position;
                    placement.rotation = existing.rotation;
                }
                placement.parent = existing.parent;
                foreach (var billboard in existing.GetComponentsInChildren<ExhibitBillboard>(true))
                {
                    if (billboard.slide != null)
                        placement.slides[billboard.name] = billboard.slide;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            if (existing == null || useDefaultPose)
            {
                if (TryFindGround(placement.position, out float ground))
                    placement.position.y = ground;
                else
                    Debug.LogWarning($"[Exhibits] No ground found under {rootName}; placing it at y = {placement.position.y}.");
            }
            return placement;
        }

        /// <summary>
        /// Puts a freshly built exhibit in place, restores slides, adds its glowing floor zone (titled
        /// <paramref name="zoneTitle"/>), and registers it for undo.
        /// </summary>
        public static void FinishExhibit(Transform root, ExhibitPlacement placement, string zoneTitle, Color zoneColor)
        {
            foreach (var billboard in root.GetComponentsInChildren<ExhibitBillboard>(true))
            {
                if (placement.slides.TryGetValue(billboard.name, out var slide))
                    billboard.slide = slide;
            }

            root.SetParent(placement.parent, false);
            root.SetPositionAndRotation(placement.position, placement.rotation);
            // Draw the wires in place so they show in the Scene view before entering Play mode.
            foreach (var wire in root.GetComponentsInChildren<CircuitWire>(true))
                wire.Rebuild();

            Zone(root, zoneTitle, zoneColor);

            Undo.RegisterCreatedObjectUndo(root.gameObject, $"Build {root.name}");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[Exhibits] Built {root.name} at {placement.position}. Save the scene to keep it.", root);
        }

        /// <summary>
        /// The bench, both billboards and the "Experiment" holder (local y = 0 is the bench top) that every
        /// exhibit starts from.
        /// </summary>
        public static Transform ExhibitFrame(Transform root, SharedMaterials materials)
        {
            Bench(root, Vector3.zero, 1.6f, 0.7f, materials.white);
            // Billboards stand wide of the bench so the video monitor fits between the bench and the left one.
            Billboard(root, "Billboard Left", new Vector3(-2.4f, 0f, 0.2f), -25f, materials.white, materials.screen);
            Billboard(root, "Billboard Right", new Vector3(2.4f, 0f, 0.2f), 25f, materials.white, materials.screen);
            VideoMonitor(root, new Vector3(-1.3f, 0f, -0.1f), -35f, materials);
            return Empty("Experiment", root, new Vector3(0f, BenchTopHeight, 0f)).transform;
        }

        /// <summary>
        /// A 16:9 monitor on a floor stand with Play/Pause and Restart buttons. Set its video (a clip or a direct
        /// .mp4 link) on its <see cref="ExhibitVideoMonitor"/>. It faces local -z, turned by <paramref name="yaw"/>.
        /// </summary>
        public static ExhibitVideoMonitor VideoMonitor(Transform parent, Vector3 localPosition, float yaw, SharedMaterials materials)
        {
            const float screenHeight = 1.45f;
            var root = Empty("Video Monitor", parent, localPosition, Quaternion.Euler(0f, yaw, 0f)).transform;
            Primitive(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.015f, 0f), new Vector3(0.4f, 0.015f, 0.4f), materials.darkPlastic);
            Primitive(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, 0.65f, 0.03f), new Vector3(0.05f, 0.62f, 0.05f), materials.darkPlastic);
            Primitive(PrimitiveType.Cube, "Bezel", root, new Vector3(0f, screenHeight, 0f), new Vector3(0.82f, 0.5f, 0.05f), materials.darkPlastic);
            var screenMaterial = UnlitMaterial($"{SharedMaterialFolder}/MonitorScreen.mat", Color.white);
            var screen = Primitive(PrimitiveType.Quad, "Screen", root, new Vector3(0f, screenHeight, -0.0255f), new Vector3(0.76f, 0.4275f, 1f),
                screenMaterial, keepCollider: false);
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var message = Label("Message", root, new Vector3(0f, screenHeight, -0.027f), Quaternion.identity, "", 0.26f,
                new Vector2(0.7f, 0.3f), new Color(0.85f, 0.88f, 0.92f));
            Label("Caption", root, new Vector3(0f, screenHeight + 0.29f, -0.02f), Quaternion.identity, "Lesson video", 0.3f,
                new Vector2(0.5f, 0.05f), TextColor);

            var shelf = Empty("Controls", root, new Vector3(0f, 1.1f, -0.06f), Quaternion.Euler(-30f, 0f, 0f)).transform;
            Primitive(PrimitiveType.Cube, "Shelf", shelf, Vector3.zero, new Vector3(0.36f, 0.02f, 0.12f), materials.darkPlastic);
            var playPause = PokeButton(shelf, "Play Pause", new Vector3(-0.08f, 0.01f, 0.015f), new Color(0.15f, 0.68f, 0.3f), "Play / Pause", materials);
            var restart = PokeButton(shelf, "Restart", new Vector3(0.09f, 0.01f, 0.015f), new Color(0.15f, 0.45f, 0.9f), "Restart", materials);
            foreach (var label in shelf.GetComponentsInChildren<TextMeshPro>(true))
                label.color = Color.white;

            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f; // Heard near the monitor, not across the lawn.
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 1f;
            audio.maxDistance = 6f;

            root.gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            var monitor = root.gameObject.AddComponent<ExhibitVideoMonitor>();
            Set(monitor, "m_Screen", screen.GetComponent<Renderer>());
            Set(monitor, "m_Message", message);
            Set(monitor, "m_PlayPauseButton", playPause);
            Set(monitor, "m_RestartButton", restart);
            Set(monitor, "m_Audio", audio);
            return monitor;
        }

        /// <summary>
        /// A glowing rounded-rectangle boundary on the floor around the exhibit, with a soft halo rising from
        /// it and the exhibit's name at the front edge. It sits on the visible ground, not on the teleport floor.
        /// </summary>
        static void Zone(Transform root, string title, Color color)
        {
            float lift = 0.012f;
            if (LowPolyMeshes.VisibleGroundHeight(root.position, out float ground))
                lift += ground - root.position.y;

            var zone = Empty("Zone", root, new Vector3(0f, lift, 0f)).transform;
            var mesh = LowPolyMeshes.Save("Assets/Exhibits/Shared/Meshes/ExhibitZone.asset", ZoneMesh());
            var material = LowPolyMeshes.GlowMaterial($"{SharedMaterialFolder}/ZoneGlow.mat", Color.white);
            zone.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = zone.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var glow = zone.gameObject.AddComponent<ExhibitZoneGlow>();
            SetArray(glow, "m_Renderers", new Object[] { renderer });
            var serialized = new SerializedObject(glow);
            serialized.FindProperty("m_Color").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var label = Label("Zone Title", zone, new Vector3(0f, 0.003f, k_ZoneFront + 0.3f), Quaternion.Euler(90f, 0f, 0f),
                title, 3f, new Vector2(4f, 0.4f), color * 0.85f);
            label.fontStyle = FontStyles.Bold;
        }

        // Zone footprint in exhibit space: wide enough for both billboards, deep enough for visitors in front.
        const float k_ZoneHalfWidth = 3.4f;
        const float k_ZoneFront = -2.5f;
        const float k_ZoneBack = 1.1f;
        const float k_ZoneCorner = 0.8f;

        static Mesh ZoneMesh()
        {
            const float band = 0.06f;
            const float haloHeight = 0.45f;
            var builder = new MeshBuilder();
            var inner = RoundedRect(-band);
            var middle = RoundedRect(0f);
            var outer = RoundedRect(band);
            var clear = new Color(1f, 1f, 1f, 0f);
            var bright = new Color(1f, 1f, 1f, 1f);
            var halo = new Color(1f, 1f, 1f, 0.45f);
            for (int i = 0; i < middle.Count; i++)
            {
                int j = (i + 1) % middle.Count;
                // Floor line: transparent at its edges, bright along its middle.
                builder.Triangle(0, inner[i], middle[i], middle[j], clear, bright, bright);
                builder.Triangle(0, inner[i], middle[j], inner[j], clear, bright, clear);
                builder.Triangle(0, middle[i], outer[i], outer[j], bright, clear, clear);
                builder.Triangle(0, middle[i], outer[j], middle[j], bright, clear, bright);
                // Halo: a low curtain of light fading out upward.
                var up = Vector3.up * haloHeight;
                builder.Triangle(0, middle[i], middle[i] + up, middle[j] + up, halo, clear, clear);
                builder.Triangle(0, middle[i], middle[j] + up, middle[j], halo, clear, halo);
            }
            return builder.ToMesh("ExhibitZone");
        }

        static List<Vector3> RoundedRect(float offset)
        {
            const int cornerSegments = 8;
            float halfDepth = (k_ZoneBack - k_ZoneFront) * 0.5f;
            float centerZ = (k_ZoneBack + k_ZoneFront) * 0.5f;
            float r = k_ZoneCorner + offset;
            float cx = k_ZoneHalfWidth - k_ZoneCorner;
            float cz = halfDepth - k_ZoneCorner;
            var points = new List<Vector3>();
            var corners = new[] { new Vector2(cx, cz), new Vector2(-cx, cz), new Vector2(-cx, -cz), new Vector2(cx, -cz) };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float angle = (c * 90f + s * 90f / cornerSegments) * Mathf.Deg2Rad;
                    points.Add(new Vector3(corners[c].x + Mathf.Cos(angle) * r, 0f, centerZ + corners[c].y + Mathf.Sin(angle) * r));
                }
            }
            return points;
        }

        /// <summary>
        /// The step guide: title, open question, live observations and hint text on the backsplash, plus a
        /// sloped control strip along the bench's front edge with Hint, Next and Reset buttons.
        /// </summary>
        public static ExhibitGuide Guide(Transform experiment, Transform exhibitRoot, SharedMaterials materials)
        {
            float z = 0.35f - 0.021f; // Just in front of the backsplash.
            // Text boxes shrink their font to fit rather than overflowing into each other.
            var title = Label("Guide Title", experiment, new Vector3(0f, 0.54f, z), Quaternion.identity, "", 0.45f, new Vector2(1.5f, 0.07f), TextColor);
            title.fontStyle = FontStyles.Bold;
            var prompt = Label("Guide Prompt", experiment, new Vector3(0f, 0.43f, z), Quaternion.identity, "", 0.38f, new Vector2(1.45f, 0.16f), TextColor);
            var feedback = Label("Guide Feedback", experiment, new Vector3(0f, 0.3f, z), Quaternion.identity, "", 0.32f, new Vector2(1.45f, 0.1f),
                new Color(0.1f, 0.4f, 0.2f));
            var hint = Label("Guide Hint", experiment, new Vector3(0f, 0.215f, z), Quaternion.identity, "", 0.28f, new Vector2(1.45f, 0.07f),
                new Color(0.55f, 0.4f, 0.05f));
            hint.fontStyle = FontStyles.Italic;
            foreach (var text in new[] { title, prompt, feedback, hint })
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = text.fontSize * 0.6f;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            // The strip hangs off the bench's front edge, tilted toward the visitor.
            var strip = Empty("Control Strip", experiment, new Vector3(0f, -0.015f, -0.415f), Quaternion.Euler(-20f, 0f, 0f)).transform;
            Primitive(PrimitiveType.Cube, "Panel", strip, Vector3.zero, new Vector3(1.4f, 0.02f, 0.13f), materials.white);

            var hintButton = PokeButton(strip, "Hint", new Vector3(-0.5f, 0.01f, 0.015f), k_HintColor, "Hint", materials);
            var nextButton = PokeButton(strip, "Next", new Vector3(0.05f, 0.01f, 0.015f), k_NextColor, "Next", materials);
            var resetButton = PokeButton(strip, "Reset", new Vector3(0.5f, 0.01f, 0.015f), k_ResetColor, "Reset", materials);

            // Shared with everyone in the session: one NetworkObject per exhibit, owned by one player at a time.
            if (!exhibitRoot.TryGetComponent<Unity.Netcode.NetworkObject>(out _))
                exhibitRoot.gameObject.AddComponent<Unity.Netcode.NetworkObject>();

            var guide = experiment.gameObject.AddComponent<ExhibitGuide>();
            var guideSync = experiment.gameObject.AddComponent<ExhibitGuideSync>();
            Set(guideSync, "m_Guide", guide);
            Set(guide, "m_ExhibitRoot", exhibitRoot);
            Set(guide, "m_Title", title);
            Set(guide, "m_Prompt", prompt);
            Set(guide, "m_Feedback", feedback);
            Set(guide, "m_Hint", hint);
            Set(guide, "m_HintButton", hintButton);
            Set(guide, "m_NextButton", nextButton);
            Set(guide, "m_ResetButton", resetButton);
            return guide;
        }

        /// <summary>A round push button with its label printed in front of it. Poke it, or point and select.</summary>
        public static ExhibitPokeButton PokeButton(Transform parent, string name, Vector3 localPosition, Color color, string label,
            SharedMaterials materials)
        {
            var root = Empty($"{name} Button", parent, localPosition).transform;
            Primitive(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.003f, 0f), new Vector3(0.06f, 0.003f, 0.06f),
                materials.buttonBase, keepCollider: false);
            var cap = Primitive(PrimitiveType.Cylinder, "Cap", root, new Vector3(0f, 0.012f, 0f), new Vector3(0.046f, 0.007f, 0.046f),
                materials.buttonCap, keepCollider: false);
            var collider = cap.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 2f, 1f); // The cylinder mesh is 1 x 2 x 1 before scaling.

            cap.AddComponent<XRSimpleInteractable>();
            // Pokes count when the finger moves down onto the cap.
            var poke = cap.AddComponent<XRPokeFilter>();
            poke.pokeConfiguration = new PokeThresholdDatumProperty(new PokeThresholdData
            {
                pokeDirection = PokeAxis.NegativeY,
                enablePokeAngleThreshold = true,
                pokeAngleThreshold = 60f,
            });

            var text = Label("Label", root, new Vector3(0f, 0.0015f, -0.047f), Quaternion.Euler(90f, 0f, 0f), label, 0.2f,
                new Vector2(0.12f, 0.03f), TextColor);

            var button = cap.AddComponent<ExhibitPokeButton>();
            Set(button, "m_Root", root.gameObject);
            Set(button, "m_CapRenderer", cap.GetComponent<Renderer>());
            Set(button, "m_Label", text);
            var serialized = new SerializedObject(button);
            serialized.FindProperty("m_Color").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>Red spheres that show current along a path (see <see cref="CurrentFlowVisualizer"/>).</summary>
        public static CurrentFlowVisualizer Flow(Transform parent, string name, bool closedLoop, SharedMaterials materials,
            float diameter = 0.01f, float spacing = 0.03f)
        {
            var flow = Empty(name, parent, Vector3.zero).AddComponent<CurrentFlowVisualizer>();
            Set(flow, "m_ChargeMaterial", materials.charge);
            Set(flow, "m_ClosedLoop", closedLoop);
            Set(flow, "m_ChargeDiameter", diameter);
            Set(flow, "m_Spacing", spacing);
            return flow;
        }

        /// <summary>
        /// A metal post that wire plugs clip onto. It has two slots side by side, so wires can branch.
        /// <paramref name="localPosition"/> is the bottom of the post; plugs sit on its top.
        /// </summary>
        public static CircuitTerminal Post(Transform parent, string name, Vector3 localPosition, Material capMaterial,
            SharedMaterials materials, bool acceptsLoosePlugs = true)
        {
            const float height = 0.02f;
            var post = Empty(name, parent, localPosition).transform;
            Primitive(PrimitiveType.Cylinder, "Post", post, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.012f, height * 0.5f, 0.012f),
                materials.metal, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Collar", post, new Vector3(0f, 0.004f, 0f), new Vector3(0.02f, 0.004f, 0.02f),
                capMaterial, keepCollider: false);
            var top = Empty("Top", post, new Vector3(0f, height, 0f)).transform;
            var slotA = Empty("Slot 1", top, new Vector3(0f, 0f, -0.008f)).transform;
            var slotB = Empty("Slot 2", top, new Vector3(0f, 0f, 0.008f)).transform;

            var terminal = top.gameObject.AddComponent<CircuitTerminal>();
            var serialized = new SerializedObject(terminal);
            var slots = serialized.FindProperty("m_Slots");
            slots.arraySize = 2;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = slotA;
            slots.GetArrayElementAtIndex(1).objectReferenceValue = slotB;
            serialized.FindProperty("m_AcceptsLoosePlugs").boolValue = acceptsLoosePlugs;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return terminal;
        }

        /// <summary>
        /// A small light bulb in a holder, with post A on the left and post B on the right.
        /// Returns the bulb; its glow and current spheres follow the current through it.
        /// </summary>
        public static LightBulb Bulb(Transform parent, string name, Vector3 localPosition, SharedMaterials materials,
            bool acceptsLoosePlugs = true)
        {
            var root = Empty(name, parent, localPosition).transform;
            Primitive(PrimitiveType.Cube, "Holder", root, new Vector3(0f, 0.01f, 0f), new Vector3(0.1f, 0.02f, 0.05f), materials.darkPlastic);
            var a = Post(root, "Post A", new Vector3(-0.035f, 0.02f, 0f), materials.metal, materials, acceptsLoosePlugs);
            var b = Post(root, "Post B", new Vector3(0.035f, 0.02f, 0f), materials.metal, materials, acceptsLoosePlugs);
            Primitive(PrimitiveType.Cylinder, "Socket", root, new Vector3(0f, 0.03f, 0f), new Vector3(0.024f, 0.01f, 0.024f),
                materials.metal, keepCollider: false);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Primitive(PrimitiveType.Cube, i == 0 ? "Lead A" : "Lead B", root, new Vector3(0.024f * side, 0.022f, 0f),
                    new Vector3(0.024f, 0.003f, 0.003f), materials.metal, keepCollider: false);
            }
            var glass = Primitive(PrimitiveType.Sphere, "Glass", root, new Vector3(0f, 0.062f, 0f), new Vector3(0.045f, 0.05f, 0.045f),
                materials.bulbGlass, keepCollider: false);
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // The current's route: post A, up through the filament, down to post B.
            var path = new[]
            {
                Empty("Path 1", root, new Vector3(-0.035f, 0.04f, 0f)).transform,
                Empty("Path 2", root, new Vector3(-0.008f, 0.05f, 0f)).transform,
                Empty("Path 3", root, new Vector3(0f, 0.07f, 0f)).transform,
                Empty("Path 4", root, new Vector3(0.008f, 0.05f, 0f)).transform,
                Empty("Path 5", root, new Vector3(0.035f, 0.04f, 0f)).transform,
            };

            var lightObject = Empty("Glow", root, new Vector3(0f, 0.065f, 0f));
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.5f);
            light.range = 0.25f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var flow = Flow(root, "Current", false, materials, diameter: 0.008f, spacing: 0.02f);

            var bulb = root.gameObject.AddComponent<LightBulb>();
            Set(bulb, "m_TerminalA", a);
            Set(bulb, "m_TerminalB", b);
            Set(bulb, "m_Glass", glass.GetComponent<Renderer>());
            Set(bulb, "m_Light", light);
            Set(bulb, "m_Flow", flow);
            SetArray(bulb, "m_Path", path);
            return bulb;
        }

        /// <summary>
        /// A battery in a holder: post - on the left, post + on the right, labelled.
        /// </summary>
        public static GameObject Battery(Transform parent, string name, Vector3 localPosition, SharedMaterials materials,
            out CircuitTerminal negative, out CircuitTerminal positive, out Transform[] internalPath, bool acceptsLoosePlugs = true)
        {
            var root = Empty(name, parent, localPosition).transform;
            Primitive(PrimitiveType.Cube, "Holder", root, new Vector3(0f, 0.0075f, 0f), new Vector3(0.15f, 0.015f, 0.05f), materials.darkPlastic);
            Primitive(PrimitiveType.Cylinder, "Cell", root, new Vector3(0f, 0.035f, 0f), new Vector3(0.04f, 0.04f, 0.04f),
                materials.clipBlack, keepCollider: false, localRotation: Quaternion.Euler(0f, 0f, 90f));
            Primitive(PrimitiveType.Cylinder, "Cell Band", root, new Vector3(0.03f, 0.035f, 0f), new Vector3(0.041f, 0.01f, 0.041f),
                materials.clipRed, keepCollider: false, localRotation: Quaternion.Euler(0f, 0f, 90f));
            negative = Post(root, "Post -", new Vector3(-0.06f, 0.015f, 0f), materials.clipBlack, materials, acceptsLoosePlugs);
            positive = Post(root, "Post +", new Vector3(0.06f, 0.015f, 0f), materials.clipRed, materials, acceptsLoosePlugs);
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Label("Sign -", root, new Vector3(-0.06f, 0.0155f, -0.02f), flat, "-", 0.3f, new Vector2(0.03f, 0.02f), Color.white);
            Label("Sign +", root, new Vector3(0.06f, 0.0155f, -0.02f), flat, "+", 0.3f, new Vector2(0.03f, 0.02f), Color.white);

            internalPath = new[]
            {
                Empty("Path 1", root, new Vector3(-0.06f, 0.035f, 0f)).transform,
                Empty("Path 2", root, new Vector3(-0.045f, 0.035f, 0f)).transform,
                Empty("Path 3", root, new Vector3(0.045f, 0.035f, 0f)).transform,
                Empty("Path 4", root, new Vector3(0.06f, 0.035f, 0f)).transform,
            };
            return root.gameObject;
        }

        /// <summary>
        /// A wire with a grabbable plug on each end. Free plugs start at the given positions; locked wires
        /// start clipped onto the given posts and cannot be moved (for example circuits).
        /// </summary>
        public static CircuitWireLink WireLink(Transform parent, string name, Vector3 plugAPosition, Vector3 plugBPosition,
            float length, Material cableMaterial, Transform exhibitRoot, Transform restSurface, SharedMaterials materials,
            CircuitTerminal startA = null, CircuitTerminal startB = null, bool locked = false)
        {
            var root = Empty(name, parent, Vector3.zero).transform;
            var plugA = Plug(root, "Plug A", startA != null ? startA.transform.position : parent.TransformPoint(plugAPosition),
                cableMaterial, exhibitRoot, startA, locked);
            var plugB = Plug(root, "Plug B", startB != null ? startB.transform.position : parent.TransformPoint(plugBPosition),
                cableMaterial, exhibitRoot, startB, locked);

            var cableObject = Empty("Cable", root, Vector3.zero);
            var line = cableObject.AddComponent<LineRenderer>();
            line.sharedMaterial = cableMaterial;
            line.widthMultiplier = 0.005f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            var cable = cableObject.AddComponent<CircuitWire>();
            Set(cable, "m_From", plugA.cableEnd);
            Set(cable, "m_To", plugB.cableEnd);
            Set(cable, "m_Length", length);
            Set(cable, "m_RestSurface", restSurface);

            var flow = Flow(root, "Current", false, materials);

            var link = root.gameObject.AddComponent<CircuitWireLink>();
            Set(link, "m_PlugA", plugA);
            Set(link, "m_PlugB", plugB);
            Set(link, "m_Cable", cable);
            Set(link, "m_Flow", flow);
            Set(link, "m_Length", length);
            return link;
        }

        static WirePlug Plug(Transform parent, string name, Vector3 worldPosition, Material material, Transform exhibitRoot,
            CircuitTerminal start, bool locked)
        {
            // Pivot at the metal tip, which sits on a post; the coloured grip stands above it.
            var go = Empty(name, parent, Vector3.zero);
            go.transform.position = worldPosition;
            go.transform.rotation = parent.rotation;
            var t = go.transform;
            Primitive(PrimitiveType.Cylinder, "Tip", t, new Vector3(0f, 0.005f, 0f), new Vector3(0.007f, 0.005f, 0.007f),
                AssetDatabase.LoadAssetAtPath<Material>($"{SharedMaterialFolder}/Metal.mat"), keepCollider: false);
            var gripMaterial = LitMaterial($"{SharedMaterialFolder}/Plug_{material.name}.mat", material.GetColor("_BaseColor"), smoothness: 0.4f);
            Primitive(PrimitiveType.Cylinder, "Grip", t, new Vector3(0f, 0.022f, 0f), new Vector3(0.014f, 0.012f, 0.014f),
                gripMaterial, keepCollider: false);
            var cableEnd = Empty("Cable End", t, new Vector3(0f, 0.034f, 0f)).transform;

            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.018f, 0f);
            collider.size = new Vector3(0.02f, 0.036f, 0.02f);

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.02f;
            body.angularDamping = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // Always kinematic and moved straight to the hand: plugs never collide, so they can't snag on the
            // bench, posts or bulbs. Their collider is only there so hands and rays can grab them.
            body.isKinematic = true;
            body.useGravity = false;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;

            if (!locked)
            {
                var resettable = go.AddComponent<ExhibitResettable>();
                resettable.exhibitRoot = exhibitRoot;
            }

            var plug = go.AddComponent<WirePlug>();
            Set(plug, "m_StartTerminal", start);
            Set(plug, "m_Locked", locked);
            Set(plug, "m_CableEnd", cableEnd);
            return plug;
        }

        /// <summary>A grabbable that stays physical while held (so joints and tethers work) and can be reset.</summary>
        public static void Grabbable(GameObject go, Transform exhibitRoot, float mass, bool throwOnDetach)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.angularDamping = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = throwOnDetach;

            var resettable = go.AddComponent<ExhibitResettable>();
            resettable.exhibitRoot = exhibitRoot;
        }

        /// <summary>A fixed (not grabbable) wire drawn between two points, sagging a little.</summary>
        public static CircuitWire FixedWire(Transform parent, string name, Transform from, Transform to, float length,
            Material material, Transform restSurface)
        {
            var go = Empty(name, parent, Vector3.zero);
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
            Set(wire, "m_Length", length);
            Set(wire, "m_RestSurface", restSurface);
            return wire;
        }

        /// <summary>Sets a serialized array of object references.</summary>
        public static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
