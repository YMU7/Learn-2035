using System.Linq;
using Oculus.Interaction.Editor;
using Oculus.Interaction.Input;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;

namespace XRClassroom.EditorTools
{
    /// <summary>
    /// Phase 1 of the Meta conversion: swaps the XRI player rig for the Meta OVR interaction rig
    /// in the open "_Meta" scene. Mirrors what Meta's "Add OVR Interaction Rig" quick action does.
    /// Safe to run more than once: steps that are already done are skipped.
    /// Nothing is deleted: the XRI rig is only deactivated.
    /// </summary>
    public static class MetaRigConverter
    {
        const string k_MenuPath = "Learn2035/Meta/Convert Open Scene To Meta Rig";

        // Only scenes whose name ends with this suffix may be converted, so the original
        // XRClassroom scene can never be changed by accident.
        const string k_RequiredSceneSuffix = "_Meta";

        // Asset GUIDs taken from Meta's OVRComprehensiveRigWizard (Interaction SDK OVR 201.0.0).
        const string k_OVRCameraRigGuid = "126d619cf4daa52469682f85c1378b4a";
        const string k_OVRInteractionComprehensiveGuid = "0a7d2469f24041c4284c66706f84c45e";

        const string k_LogPrefix = "[MetaRigConverter]";

        [MenuItem(k_MenuPath)]
        static void ConvertOpenScene()
        {
            Convert(interactive: true);
        }

        /// <summary>
        /// Converts the active scene. Returns false if the scene is not a "_Meta" copy.
        /// interactive = false logs instead of showing a dialog (used by EditorCommandQueue).
        /// </summary>
        public static bool Convert(bool interactive)
        {
            Scene scene = SceneManager.GetActiveScene();

            // Guard: refuse to run on any scene that is not a "_Meta" copy.
            if (!scene.name.EndsWith(k_RequiredSceneSuffix))
            {
                string message = $"Open a scene whose name ends with \"{k_RequiredSceneSuffix}\" first. Current scene: {scene.name}";
                if (interactive)
                    EditorUtility.DisplayDialog("Meta Rig Converter", message, "OK");
                else
                    Debug.LogError($"{k_LogPrefix} {message}");
                return false;
            }

            Undo.SetCurrentGroupName("Convert To Meta Rig");
            int undoGroup = Undo.GetCurrentGroup();

            Pose spawnPose = DeactivateXRIRig(scene);
            OVRCameraRig cameraRig = FindOrCreateCameraRig(spawnPose);
            FindOrCreateInteractionRig(cameraRig);
            DisableDuplicateHandVisuals(cameraRig);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = cameraRig.gameObject;

            WarnIfMetaXRFeatureDisabled();
            Debug.Log($"{k_LogPrefix} Done. Save the scene (Ctrl+S) to keep the changes.");
            return true;
        }

        /// <summary>
        /// Deactivates every XRI rig (the outermost prefab instance that contains an XROrigin).
        /// Returns the pose of the first rig so the Meta rig can spawn at the same place.
        /// </summary>
        static Pose DeactivateXRIRig(Scene scene)
        {
            Pose spawnPose = Pose.identity;
            bool poseTaken = false;

            XROrigin[] origins = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XROrigin>(true))
                .ToArray();

            foreach (XROrigin origin in origins)
            {
                GameObject rigRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(origin.gameObject);
                if (rigRoot == null)
                    rigRoot = origin.gameObject;

                if (!poseTaken)
                {
                    // Keep only the yaw so the Meta rig stays level on the floor.
                    Vector3 forward = Vector3.ProjectOnPlane(rigRoot.transform.forward, Vector3.up);
                    Quaternion yaw = forward.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(forward, Vector3.up)
                        : Quaternion.identity;
                    spawnPose = new Pose(rigRoot.transform.position, yaw);
                    poseTaken = true;
                }

                if (rigRoot.activeSelf)
                {
                    Undo.RecordObject(rigRoot, "Deactivate XRI Rig");
                    rigRoot.SetActive(false);
                    Debug.Log($"{k_LogPrefix} Deactivated XRI rig \"{rigRoot.name}\" (not deleted).", rigRoot);
                }
            }

            if (origins.Length == 0)
                Debug.Log($"{k_LogPrefix} No XRI rig found, nothing to deactivate.");

            return spawnPose;
        }

        static OVRCameraRig FindOrCreateCameraRig(Pose spawnPose)
        {
            OVRCameraRig cameraRig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (cameraRig != null)
            {
                Debug.Log($"{k_LogPrefix} OVRCameraRig already in scene, reusing it.", cameraRig);
                return cameraRig;
            }

            GameObject instance = InstantiatePrefab(k_OVRCameraRigGuid, null);
            instance.transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
            cameraRig = instance.GetComponent<OVRCameraRig>();

            // Same OVRManager defaults as Meta's wizard: floor-level tracking origin and
            // controller-driven hand poses that conform to the controller.
            if (instance.TryGetComponent(out OVRManager ovrManager))
            {
                var serializedManager = new SerializedObject(ovrManager);
                SerializedProperty trackingOrigin = serializedManager.FindProperty("_trackingOriginType");
                if (trackingOrigin != null)
                    trackingOrigin.intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                ovrManager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
                EditorUtility.SetDirty(ovrManager);
            }

            Debug.Log($"{k_LogPrefix} Added OVRCameraRig.", instance);
            return cameraRig;
        }

        static void FindOrCreateInteractionRig(OVRCameraRig cameraRig)
        {
            OVRCameraRigRef rigRef = cameraRig.GetComponentInChildren<OVRCameraRigRef>(true);
            if (rigRef == null)
            {
                GameObject instance = InstantiatePrefab(k_OVRInteractionComprehensiveGuid, cameraRig.transform);
                // Let the Interaction SDK auto-wire its references, as its quick action does.
                UnityObjectAddedBroadcaster.HandleObjectWasAdded(instance);
                rigRef = instance.GetComponentInChildren<OVRCameraRigRef>(true);
                Debug.Log($"{k_LogPrefix} Added OVRInteractionComprehensive under OVRCameraRig.", instance);
            }
            else
            {
                Debug.Log($"{k_LogPrefix} OVRInteractionComprehensive already present, reusing it.", rigRef);
            }

            // Make sure the interaction rig points at this camera rig, even if auto-wiring skipped it.
            if (rigRef != null)
            {
                var serializedRef = new SerializedObject(rigRef);
                SerializedProperty rigProperty = serializedRef.FindProperty("_ovrCameraRig");
                if (rigProperty != null && rigProperty.objectReferenceValue != cameraRig)
                {
                    rigProperty.objectReferenceValue = cameraRig;
                    serializedRef.ApplyModifiedProperties();
                }
            }
        }

        /// <summary>
        /// The OVRCameraRig hands draw their own mesh; the interaction rig draws synthetic hands.
        /// Disable the camera rig's copy so each hand is drawn once (same as Meta's wizard).
        /// </summary>
        static void DisableDuplicateHandVisuals(OVRCameraRig cameraRig)
        {
            // trackingSpace is only resolved at runtime, so search the whole rig in edit mode.
            foreach (OVRHand hand in cameraRig.GetComponentsInChildren<OVRHand>(true))
            {
                DisableIfPresent<OVRSkeletonRenderer>(hand);
                DisableIfPresent<OVRMesh>(hand);
                DisableIfPresent<OVRMeshRenderer>(hand);
                if (hand.TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer) && skinnedMeshRenderer.enabled)
                {
                    Undo.RecordObject(skinnedMeshRenderer, "Disable Duplicate Hand Visual");
                    skinnedMeshRenderer.enabled = false;
                }
            }
        }

        static void DisableIfPresent<T>(Component owner) where T : Behaviour
        {
            if (owner.TryGetComponent(out T behaviour) && behaviour.enabled)
            {
                Undo.RecordObject(behaviour, "Disable Duplicate Hand Visual");
                behaviour.enabled = false;
            }
        }

        static GameObject InstantiatePrefab(string guid, Transform parent)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new System.InvalidOperationException($"{k_LogPrefix} Prefab not found for GUID {guid}. Is the Meta XR SDK installed?");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Add " + prefab.name);
            return instance;
        }

        /// <summary>
        /// OVRCameraRig only tracks under Unity OpenXR when the "Meta XR" OpenXR feature is enabled
        /// for the platform. Warn (do not change project settings here).
        /// </summary>
        static void WarnIfMetaXRFeatureDisabled()
        {
            foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null)
                    continue;

                bool enabled = settings.GetFeatures()
                    .Any(feature => feature != null && feature.GetType().Name == "MetaXRFeature" && feature.enabled);
                if (!enabled)
                {
                    Debug.LogWarning($"{k_LogPrefix} OpenXR feature \"Meta XR\" is disabled for {group}. " +
                        "Run Meta > Tools > Project Setup Tool and apply the fixes, or the OVRCameraRig will not track.");
                }
            }
        }
    }
}
