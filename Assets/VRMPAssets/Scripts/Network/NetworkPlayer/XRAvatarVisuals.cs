using System;
using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer
{
    [RequireComponent(typeof(XRINetworkPlayer))]
    public class XRAvatarVisuals : MonoBehaviour
    {
        /// <summary>
        /// Head Renderers to change rendering mode for local players.
        /// </summary>
        [Header("Renderer References"), SerializeField, Tooltip("Head Renderers to change rendering mode for local players.")]
        protected Renderer[] m_HeadRends;

        /// <summary>
        /// Head Renderer to control the blend shape for mouth movement. Also updates shirt color based on <see cref="playerColor"/>.
        /// </summary>
        [SerializeField, Tooltip("Head Renderer to drive mouth movement blendshape and player shirt color.")]
        protected SkinnedMeshRenderer m_headRend;

        /// <summary>
        /// GameObject to enable to show what player is the Room Host.
        /// </summary>
        [Header("Host Visuals"), SerializeField, Tooltip("GameObject that gets enabled for the Host only.")]
        protected GameObject m_HostVisuals;

        /// <summary>
        /// GameObject to enable to show what player is the Room Host.
        /// </summary>
        [SerializeField, Tooltip("Show Host Visuals.")]
        protected bool m_ShowHostVisuals = true;

        /// <summary>
        /// Materials to swap for the local player.
        /// </summary>
        [Header("Local Player Material Swap"), SerializeField]
        protected LocalPlayerMaterialSwap m_LocalPlayerMaterialSwap;

        /// <summary>
        /// Reference to the attached XRINetworkPlayerAvatar component.
        /// </summary>
        protected XRINetworkPlayer m_NetworkPlayer;

        public virtual void Awake()
        {
            if (!TryGetComponent(out m_NetworkPlayer))
            {
                Utils.LogError("XRAvatarVisuals requires a XRINetworkPlayerAvatar component to be attached to the same GameObject. Disabling this component now.");
                enabled = false;
                return;
            }

            m_NetworkPlayer.onSpawnedLocal += PlayerSpawnedLocal;
            m_NetworkPlayer.onSpawnedAll += PlayerSpawnedAll;
            m_NetworkPlayer.onColorUpdated += SetPlayerColor;
            XRINetworkGameManager.Instance.OnSessionOwnerPromoted += HostUpdated;
        }

        public virtual void OnDestroy()
        {
            m_NetworkPlayer.onSpawnedLocal -= PlayerSpawnedLocal;
            m_NetworkPlayer.onSpawnedAll -= PlayerSpawnedAll;
            m_NetworkPlayer.onColorUpdated -= SetPlayerColor;

            XRINetworkGameManager.Instance.OnSessionOwnerPromoted -= HostUpdated;
        }

        public virtual void Update()
        {
            UpdateMouth();
        }

        public virtual void UpdateMouth()
        {
            if (m_headRend != null)
                m_headRend.SetBlendShapeWeight(0, 100 - (m_NetworkPlayer.playerVoiceAmp * 100));
        }

        public virtual void PlayerSpawnedLocal()
        {
            m_LocalPlayerMaterialSwap.SwapMaterials();
            int layer = LayerMask.NameToLayer("Mirror");
            foreach (var r in m_HeadRends)
            {
                if (r == null) continue; // unassigned slot on part-based avatars
                r.gameObject.layer = layer;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        public virtual void PlayerSpawnedAll()
        {
            if (m_HostVisuals == null) return;
            m_HostVisuals.SetActive(m_ShowHostVisuals && m_NetworkPlayer.NetworkObject.OwnerClientId == NetworkManager.Singleton.CurrentSessionOwner);
        }

        public virtual void SetPlayerColor(Color newColor)
        {
            // The template head mesh had the color on material slot 2; part-based avatars may have no head
            // renderer here or fewer slots, so skip instead of throwing during player setup.
            if (m_headRend == null || m_headRend.sharedMaterials.Length <= 2) return;
            m_headRend.materials[2].SetColor("_BaseColor", newColor);
        }

        public virtual void HostUpdated(ulong newHostId)
        {
            if (m_HostVisuals == null) return;
            m_HostVisuals.SetActive(m_ShowHostVisuals && m_NetworkPlayer.NetworkObject.OwnerClientId == newHostId);
        }
    }
}

[Serializable]

/// <summary>
/// Helper class for swapping the local player to standard materials from the dithering materials.
/// </summary>
public class LocalPlayerMaterialSwap
{
    public Renderer headRend;
    public Renderer hmdRend;
    public Renderer hostRend;
    public Renderer[] hands;
    public Material[] headMaterials;
    public Material[] hmdMaterials;
    public Material hostMaterial;
    public Material handMaterial;


    public void SwapMaterials()
    {
        // Part-based avatars leave some of these renderers unassigned (e.g. no HMD mesh). Skip missing ones
        // instead of throwing: an exception here aborts XRINetworkPlayer.onSpawnedLocal for later listeners.
        if (hands != null)
        {
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i] != null) hands[i].material = handMaterial;
            }
        }

        if (hmdRend != null) hmdRend.materials = hmdMaterials;
        if (headRend != null) headRend.materials = headMaterials;
        if (hostRend != null) hostRend.material = hostMaterial;
    }
}
