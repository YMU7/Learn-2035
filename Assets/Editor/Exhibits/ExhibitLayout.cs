using UnityEditor;
using UnityEngine;

namespace XRClassroom.EditorTools.Exhibits
{
    /// <summary>
    /// Where the three exhibits stand in DemoClass: one row across the big purple lawn, left to right as seen
    /// from the spawn point (looking toward the building, +z). Each bench faces the spawn side, so visitors
    /// stand south of it. Heights come from the ground under each spot.
    /// </summary>
    static class ExhibitLayout
    {
        /// <summary>All benches face -z (yaw 0): the bench front and control strip are on the south side.</summary>
        public const float Yaw = 0f;

        /// <summary>Left outskirts of the lawn.</summary>
        public static readonly Vector3 PotatoBatteryPosition = new Vector3(-0.3f, 0f, 9f);

        /// <summary>Middle of the lawn.</summary>
        public static readonly Vector3 SeriesParallelPosition = new Vector3(9.3f, 0f, 9f);

        /// <summary>Right side of the lawn.</summary>
        public static readonly Vector3 HandCrankPosition = new Vector3(18.9f, 0f, 9f);

        /// <summary>Rebuilds all three exhibits at their layout spots (moving any that were moved by hand).</summary>
        [MenuItem("XR Classroom/Exhibits/Build All Exhibits (Reset Layout)")]
        static void BuildAll()
        {
            if (!ExhibitBuilderUtility.CheckDemoSceneIsActive("build the exhibits"))
                return;
            PotatoBatteryExhibitBuilder.Build(useDefaultPose: true);
            SeriesParallelExhibitBuilder.Build(useDefaultPose: true);
            HandCrankExhibitBuilder.Build(useDefaultPose: true);
        }

        /// <summary>
        /// In-scene NetworkObjects need an ID that every player's build agrees on. Netcode generates it from the
        /// object's saved scene identity, which only exists once the scene has been saved, so: build, save, run this,
        /// save again. (Netcode also regenerates it whenever the object is edited in the Inspector.)
        /// </summary>
        [MenuItem("XR Classroom/Exhibits/Refresh Network Object IDs")]
        static void RefreshNetworkObjectIds()
        {
            var onValidate = typeof(Unity.Netcode.NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            int count = 0;
            foreach (var networkObject in Object.FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (networkObject.GetComponentInChildren<XRClassroom.Exhibits.ExhibitGuide>(true) == null)
                    continue; // Only the exhibits; the template's own objects are left alone.
                onValidate?.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
                count++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log($"[Exhibits] Refreshed network IDs on {count} exhibits. Save the scene to keep them.");
        }

        [MenuItem("XR Classroom/Exhibits/Build All Exhibits (Reset Layout)", true)]
        static bool CanBuildAll() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
