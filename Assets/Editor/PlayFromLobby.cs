using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XRClassroom.EditorTools
{
    /// <summary>
    /// Makes Play mode always start in the lobby scene (XRClassroom) when the open scene is the room (DemoClass),
    /// so the editor follows the same lobby -> Join Online -> room flow as a build (see XRClassroom.LobbyRoomFlow).
    /// Trigger: entering Play mode while k_RoomScenePath is the active scene. Other scenes play as they are.
    /// Uses EditorSceneManager.playModeStartScene; the open scene itself is not changed.
    /// Toggle: menu "XR Classroom/Play From Lobby" (on by default, stored per machine in EditorPrefs).
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromLobby
    {
        const string k_LobbyScenePath = "Assets/Scenes/XRClassroom.unity";
        const string k_RoomScenePath = "Assets/Scenes/DemoClass.unity";
        const string k_MenuPath = "XR Classroom/Play From Lobby";
        const string k_PrefKey = "XRClassroom.PlayFromLobby";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(k_PrefKey, true);
            set => EditorPrefs.SetBool(k_PrefKey, value);
        }

        static PlayFromLobby()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;

            bool inRoom = EditorSceneManager.GetActiveScene().path == k_RoomScenePath;
            if (Enabled && inRoom)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(k_LobbyScenePath);
                Debug.Log($"[PlayFromLobby] Room scene is open: Play starts in {k_LobbyScenePath}.");
            }
            else
            {
                EditorSceneManager.playModeStartScene = null; // play the open scene
            }
        }

        [MenuItem(k_MenuPath)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(k_MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(k_MenuPath, Enabled);
            return true;
        }
    }
}
