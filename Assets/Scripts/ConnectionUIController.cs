using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

/// <summary>
/// Controls the big Join Online / Disconnect buttons.
///
/// Both buttons are driven straight from <see cref="XRINetworkGameManager.CurrentConnectionState"/>,
/// so the Join button stays up (showing "Connecting...", greyed out) during the gap between pressing
/// it and actually being connected, instead of vanishing with nothing in its place.
///
/// Supersedes ConnectionToggler for these two buttons.
/// aubrey, august 2026
/// </summary>
public class ConnectionUIController : MonoBehaviour
{
    [SerializeField] Button joinButton;
    [SerializeField] GameObject disconnectButton;

    [Tooltip("Optional. The Join button's label, so it can read \"Connecting...\" while connecting.")]
    [SerializeField] TMP_Text joinButtonLabel;
    [SerializeField] string joinText = "Join Online";
    [SerializeField] string connectingText = "Connecting...";

    /// <summary>The Join Online button. Read by XRClassroom.LobbyRoomFlow to repurpose it in the lobby scene.</summary>
    public Button JoinButton => joinButton;

    /// <summary>The Disconnect button object. Read by XRClassroom.LobbyRoomFlow to tell intentional leaves from drops.</summary>
    public GameObject DisconnectButton => disconnectButton;

    void OnEnable()
    {
        // Fires immediately with the current state, so the buttons start out correct.
        XRINetworkGameManager.CurrentConnectionState.SubscribeAndUpdate(OnConnectionStateChanged);
    }

    void OnDisable()
    {
        XRINetworkGameManager.CurrentConnectionState.Unsubscribe(OnConnectionStateChanged);
    }

    void OnConnectionStateChanged(XRINetworkGameManager.ConnectionState state)
    {
        bool connected = state == XRINetworkGameManager.ConnectionState.Connected;
        bool connecting = state == XRINetworkGameManager.ConnectionState.Connecting;

        joinButton.gameObject.SetActive(!connected);
        joinButton.interactable = !connecting;
        disconnectButton.SetActive(connected);

        if (joinButtonLabel != null)
            joinButtonLabel.text = connecting ? connectingText : joinText;
    }
}
