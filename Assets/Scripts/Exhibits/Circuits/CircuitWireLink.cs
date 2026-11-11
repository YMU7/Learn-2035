using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// A wire with a plug on each end. It conducts when both plugs are clipped onto posts. Its length limit
    /// (a rope joint between the plugs) stops one end being dragged further away than the wire is long.
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

        [SerializeField, Tooltip("Wire length (m).")]
        float m_Length = 0.4f;

        public WirePlug plugA => m_PlugA;

        public WirePlug plugB => m_PlugB;

        public CircuitWire cable => m_Cable;

        public CurrentFlowVisualizer flow => m_Flow;

        /// <summary>True when both ends are clipped onto two different posts.</summary>
        public bool isConnected => m_PlugA.attached != null && m_PlugB.attached != null && m_PlugA.attached != m_PlugB.attached;

        void Start()
        {
            // Rope between the plugs: free to move anywhere within the wire's length of each other.
            var rope = m_PlugA.gameObject.AddComponent<ConfigurableJoint>();
            rope.connectedBody = m_PlugB.body;
            rope.autoConfigureConnectedAnchor = false;
            rope.anchor = Vector3.zero;
            rope.connectedAnchor = Vector3.zero;
            rope.xMotion = ConfigurableJointMotion.Limited;
            rope.yMotion = ConfigurableJointMotion.Limited;
            rope.zMotion = ConfigurableJointMotion.Limited;
            rope.angularXMotion = ConfigurableJointMotion.Free;
            rope.angularYMotion = ConfigurableJointMotion.Free;
            rope.angularZMotion = ConfigurableJointMotion.Free;
            rope.linearLimit = new SoftJointLimit { limit = m_Length };
            rope.enableCollision = true;
        }
    }
}
