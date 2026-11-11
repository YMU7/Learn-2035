using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using XRMultiplayer;

namespace XRClassroom
{
    /// <summary>
    /// Lobby -> room flow. The lobby scene is single player; the room scene is the multiplayer session.
    ///
    /// - Lobby (<see cref="k_LobbyScene"/>): the "Join Online" button no longer connects. Pressing it loads the
    ///   room scene. Trigger: the button's onClick (its scene-serialized calls are switched off at runtime).
    /// - Room (<see cref="k_RoomScene"/>): as soon as UGS sign-in finishes (ConnectionState == Authenticated),
    ///   quick join runs once: join any open session of this scene, else create one. Players on different
    ///   networks meet through the UGS cloud (Distributed Authority), no shared Wi-Fi needed.
    /// - Room: when a connected player disconnects (Disconnect button or lost connection, i.e. ConnectionState
    ///   goes from Connected to anything else), they are sent back to the lobby.
    ///
    /// Network authority: nothing here is networked. Each client loads scenes locally and joins the session on
    /// its own; the UGS session decides who is session owner (first player in; promoted if they leave).
    ///
    /// Installed at runtime for every scene load, so neither scene file has to be edited.
    /// </summary>
    public class LobbyRoomFlow : MonoBehaviour
    {
        // Tune here: scene names (must be enabled in Build Settings).
        const string k_LobbyScene = "XRClassroom";
        const string k_RoomScene = "DemoClass";

        // Room: auto quick join when the room loads. Set false to require pressing "Join Online" in the room.
        static readonly bool k_AutoJoinInRoom = true;
        // Room: go back to the lobby after leaving the session.
        static readonly bool k_ReturnToLobbyOnDisconnect = true;
        // Room: after a disconnect, wait until the UGS session leave has finished (currentSession == null) before
        // loading the lobby, but at most this many seconds. The state already reads "Authenticated" when the
        // NetworkManager stops, while the leave request is still running; unloading then would leave twice
        // ("SessionException: lobby not found").
        const float k_ReturnToLobbyMaxWait = 5f;
        // Room: if the auto join fails (state goes Connecting -> Authenticated without reaching Connected),
        // try again up to this many extra times, waiting k_RetryDelay seconds in between. UGS session
        // creation occasionally fails on the service side ("No join code returned from lobby").
        const int k_MaxJoinRetries = 3;
        const float k_RetryDelay = 3f;

        const string k_LogPrefix = "[LobbyRoomFlow] ";

        static bool s_Switching;

        bool m_IsRoom;
        bool m_AutoJoinStarted;
        bool m_WasConnected;
        bool m_SawConnecting;
        bool m_Leaving;
        int m_JoinRetries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // AfterSceneLoad runs after the first scene's sceneLoaded event, so handle the first scene here.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (scene.name != k_LobbyScene && scene.name != k_RoomScene) return;
            if (FindFirstObjectByType<LobbyRoomFlow>() != null) return;

            // Lives in the loaded scene, so it is destroyed with it.
            var go = new GameObject(nameof(LobbyRoomFlow));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<LobbyRoomFlow>().m_IsRoom = scene.name == k_RoomScene;
        }

        void Start()
        {
            s_Switching = false;

            if (m_IsRoom)
            {
                XRINetworkGameManager.CurrentConnectionState.SubscribeAndUpdate(OnRoomConnectionState);
            }
            else
            {
                RepurposeLobbyJoinButton();
            }
        }

        void OnDestroy()
        {
            if (m_IsRoom)
                XRINetworkGameManager.CurrentConnectionState.Unsubscribe(OnRoomConnectionState);
        }

        // ---------- Lobby ----------

        void RepurposeLobbyJoinButton()
        {
            var ui = FindFirstObjectByType<ConnectionUIController>(FindObjectsInactive.Include);
            if (ui == null || ui.JoinButton == null)
            {
                Debug.LogWarning($"{k_LogPrefix}No ConnectionUIController / Join button in lobby; cannot enter room.");
                return;
            }

            // Switch off the scene-serialized calls (QuickJoinLobby etc.) so the lobby stays offline.
            var onClick = ui.JoinButton.onClick;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
                onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            onClick.AddListener(EnterRoom);
            Debug.Log($"{k_LogPrefix}Lobby: Join Online now loads '{k_RoomScene}'.");
        }

        /// <summary>Lobby: load the room scene (the room then joins the session by itself).</summary>
        public void EnterRoom()
        {
            Debug.Log($"{k_LogPrefix}Entering room '{k_RoomScene}'.");
            SwitchScene(k_RoomScene);
        }

        // ---------- Room ----------

        void OnRoomConnectionState(XRINetworkGameManager.ConnectionState state)
        {
            Debug.Log($"{k_LogPrefix}Room: connection state = {state}");

            // Trigger: sign-in done and not yet tried -> quick join once.
            if (k_AutoJoinInRoom && !m_AutoJoinStarted && state == XRINetworkGameManager.ConnectionState.Authenticated
                && XRINetworkGameManager.Instance != null)
            {
                m_AutoJoinStarted = true;
                // Set here too: the Connecting broadcast can fire before our subscription is registered
                // (SubscribeAndUpdate calls back first, then subscribes).
                m_SawConnecting = true;
                Debug.Log($"{k_LogPrefix}Room: signed in, quick joining.");
                XRINetworkGameManager.Instance.QuickJoinLobby();
                return;
            }

            if (state == XRINetworkGameManager.ConnectionState.Connecting)
            {
                m_SawConnecting = true;
                return;
            }

            if (state == XRINetworkGameManager.ConnectionState.Connected)
            {
                m_WasConnected = true;
                m_JoinRetries = 0;
                return;
            }

            // Trigger: a join attempt failed (Connecting -> Authenticated, never Connected) -> retry after a delay.
            if (k_AutoJoinInRoom && !m_Leaving && !m_WasConnected && m_SawConnecting
                && state == XRINetworkGameManager.ConnectionState.Authenticated)
            {
                m_SawConnecting = false;
                if (m_JoinRetries < k_MaxJoinRetries)
                {
                    m_JoinRetries++;
                    Debug.Log($"{k_LogPrefix}Room: join failed, retry {m_JoinRetries}/{k_MaxJoinRetries} in {k_RetryDelay} s.");
                    StartCoroutine(RetryJoinAfterDelay());
                }
                else
                {
                    Debug.LogWarning($"{k_LogPrefix}Room: join failed {k_MaxJoinRetries + 1} times; press Join Online to try again.");
                }
                return;
            }

            // Trigger: was connected, now not (and not just reconnecting) -> back to lobby.
            if (k_ReturnToLobbyOnDisconnect && m_WasConnected && state != XRINetworkGameManager.ConnectionState.Connecting)
            {
                m_WasConnected = false;
                Debug.Log($"{k_LogPrefix}Room: left session ({state}), returning to lobby.");
                StartCoroutine(ReturnToLobbyAfterDelay());
            }
        }

        /// <summary>
        /// Room: leave the session (same as the Disconnect button); the state change then returns us to the lobby.
        /// Also a test hook: EditorCommandQueue "call LobbyRoomFlow LobbyRoomFlow LeaveRoom".
        /// </summary>
        [ContextMenu("Leave Room")]
        public void LeaveRoom()
        {
            if (!m_IsRoom) return;
            m_Leaving = true;
            StopAllCoroutines(); // cancel a pending join retry

            if (m_WasConnected && XRINetworkGameManager.Instance != null)
                XRINetworkGameManager.Instance.Disconnect(); // state change then returns us to the lobby
            else
                SwitchScene(k_LobbyScene); // never connected: go back directly
        }

        IEnumerator RetryJoinAfterDelay()
        {
            yield return new WaitForSeconds(k_RetryDelay);
            // Skip if the player pressed Join Online / Leave in the meantime.
            if (m_Leaving || XRINetworkGameManager.Instance == null ||
                XRINetworkGameManager.CurrentConnectionState.Value != XRINetworkGameManager.ConnectionState.Authenticated)
                yield break;
            m_SawConnecting = true;
            XRINetworkGameManager.Instance.QuickJoinLobby();
        }

        IEnumerator ReturnToLobbyAfterDelay()
        {
            float waitUntil = Time.realtimeSinceStartup + k_ReturnToLobbyMaxWait;
            while (Time.realtimeSinceStartup < waitUntil && XRINetworkGameManager.Instance != null
                   && XRINetworkGameManager.Instance.sessionManager.currentSession != null)
                yield return null;
            SwitchScene(k_LobbyScene);
        }

        // ---------- Scene switch ----------

        /// <summary>
        /// Both scenes carry their own network + game manager. NetworkManager marks itself DontDestroyOnLoad, so
        /// a plain LoadScene would leave two of them. Instead: destroy the old managers and scene, wait one frame
        /// so every OnDestroy has run, reset the template's static bindings, then load the new scene fresh.
        /// UGS sign-in survives (UnityServices is global), so the new scene signs in instantly.
        /// </summary>
        static void SwitchScene(string sceneName)
        {
            if (s_Switching) return;
            s_Switching = true;

            var runner = new GameObject("LobbyRoomFlow.SceneSwitch").AddComponent<SceneSwitchRunner>();
            DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(runner.Run(sceneName));
        }

        class SceneSwitchRunner : MonoBehaviour
        {
            public IEnumerator Run(string sceneName)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null)
                {
                    if (nm.IsListening) nm.Shutdown();
                    Destroy(nm.gameObject);
                }

                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    Destroy(root);

                yield return null; // all OnDestroy calls run at the end of this frame

                XRINetworkGameManager.ResetBindingsForSceneChange();
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

                yield return null;
                Destroy(gameObject);
            }
        }
    }
}
