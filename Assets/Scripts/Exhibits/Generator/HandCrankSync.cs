using Unity.Netcode;
using UnityEngine;

namespace XRClassroom.Exhibits.Generator
{
    /// <summary>
    /// Shares the hand crank with everyone in the session: whoever is turning it sends its angle and speed to
    /// the other players about 20 times a second, and one last update when they let go so everyone's crank
    /// coasts to a stop together. The bulb, magnet and current spheres follow the crank on each headset.
    ///
    /// Network authority: none needed. The player whose hand is on the knob is the source; updates go to
    /// everyone else (SendTo.NotMe). If two players grab at once, each sees their own hand win locally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandCrankSync : NetworkBehaviour
    {
        const float k_SendInterval = 0.05f;

        [SerializeField] HandCrank m_Crank;

        float m_NextSend;
        bool m_WasDriving;

        void Update()
        {
            if (!IsSpawned)
                return;

            bool driving = m_Crank.isDrivenLocally;
            if (driving && Time.time >= m_NextSend)
            {
                m_NextSend = Time.time + k_SendInterval;
                CrankMovedRpc(m_Crank.angle, m_Crank.revolutionsPerSecond);
            }
            else if (!driving && m_WasDriving)
            {
                CrankReleasedRpc(m_Crank.angle, m_Crank.revolutionsPerSecond);
            }
            m_WasDriving = driving;
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        void CrankMovedRpc(float angle, float revolutionsPerSecond) =>
            m_Crank.ApplyRemote(angle, revolutionsPerSecond, held: true);

        [Rpc(SendTo.NotMe)]
        void CrankReleasedRpc(float angle, float revolutionsPerSecond) =>
            m_Crank.ApplyRemote(angle, revolutionsPerSecond, held: false);
    }
}
