using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// A wire with a plug on each end. It conducts when both plugs are clipped onto posts. The plugs move
    /// freely (no collisions, no joints); the drawn cable sags when slack and pulls straight when stretched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CircuitWireLink : MonoBehaviour
    {
        [SerializeField] WirePlug m_PlugA;
        [SerializeField] WirePlug m_PlugB;

        [SerializeField, Tooltip("The drawn cable between the two plugs.")]
        CircuitWire m_Cable;

        [SerializeField, Tooltip("Red spheres showing the current in this wire.")]
        CurrentFlowVisualizer m_Flow;

        [SerializeField, Tooltip("Wire length (m), used for how much the drawn cable sags.")]
        float m_Length = 0.4f;

        public WirePlug plugA => m_PlugA;

        public WirePlug plugB => m_PlugB;

        public CircuitWire cable => m_Cable;

        public CurrentFlowVisualizer flow => m_Flow;

        /// <summary>True when both ends are clipped onto two different posts.</summary>
        public bool isConnected => m_PlugA.attached != null && m_PlugB.attached != null && m_PlugA.attached != m_PlugB.attached;

    }
}
