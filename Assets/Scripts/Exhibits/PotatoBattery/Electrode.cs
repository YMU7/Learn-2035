using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRClassroom.Exhibits.PotatoBattery
{
    public enum ElectrodeMetal
    {
        /// <summary>Negative terminal (anode): zinc gives up electrons.</summary>
        Zinc,

        /// <summary>Positive terminal (cathode): electrons are taken up at the copper.</summary>
        Copper,
    }

    /// <summary>
    /// A grabbable metal electrode wired to the LED. It passes into the potato instead of bouncing off it,
    /// and if released with its tip inside the potato it stays stuck there (a fixed joint), so the potato
    /// can be picked up with the electrodes in it. Its wire is a joint that stops it being carried further
    /// from the LED than the wire's length.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    [DisallowMultipleComponent]
    public sealed class Electrode : MonoBehaviour
    {
        [SerializeField]
        ElectrodeMetal m_Metal;

        [SerializeField, Tooltip("The pointed end that goes into the potato.")]
        Transform m_Tip;

        [SerializeField, Tooltip("The clip end where the wire attaches.")]
        Transform m_Terminal;

        [SerializeField]
        Potato m_Potato;

        [SerializeField, Tooltip("Where the other end of this electrode's wire is fixed (an LED terminal).")]
        Transform m_WireAnchor;

        [SerializeField, Tooltip("Wire length (m): the farthest the clip can get from the wire anchor.")]
        float m_WireLength = 0.6f;

        Rigidbody m_Body;
        XRGrabInteractable m_Grab;
        FixedJoint m_InsertJoint;

        public ElectrodeMetal metal => m_Metal;

        public Transform tip => m_Tip;

        public Transform terminal => m_Terminal;

        public Potato potato => m_Potato;

        /// <summary>True while the tip is inside the potato.</summary>
        public bool isInserted => m_Potato != null && m_Tip != null && m_Potato.Contains(m_Tip.position);

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Grab = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            m_Grab.selectEntered.AddListener(OnGrabbed);
            m_Grab.selectExited.AddListener(OnReleased);
            if (TryGetComponent(out ExhibitResettable resettable))
                resettable.resetting += Unstick;
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrabbed);
            m_Grab.selectExited.RemoveListener(OnReleased);
            if (TryGetComponent(out ExhibitResettable resettable))
                resettable.resetting -= Unstick;
        }

        void Start()
        {
            // Let the metal slide into the potato instead of colliding with its surface.
            if (m_Potato != null && m_Potato.bodyCollider != null)
            {
                foreach (var ownCollider in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(ownCollider, m_Potato.bodyCollider);
            }

            CreateWireTether();

            if (isInserted)
                StickToPotato();
        }

        void OnGrabbed(SelectEnterEventArgs args) => Unstick();

        void OnReleased(SelectExitEventArgs args)
        {
            // Runs after the grab interactable has restored the rigidbody, so the joint is the last word.
            if (isInserted)
                StickToPotato();
        }

        /// <summary>
        /// Puts the electrode upright with its tip at <paramref name="tipPosition"/> and sticks it there,
        /// as if a player had pushed it in. For testing without a headset.
        /// </summary>
        public void PushInto(Vector3 tipPosition)
        {
            Unstick();
            Quaternion upright = Quaternion.identity; // Local y up: tip at the bottom, clip on top.
            Vector3 position = tipPosition - upright * m_Tip.localPosition;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Body.position = position;
            m_Body.rotation = upright;
            transform.SetPositionAndRotation(position, upright);
            if (isInserted)
                StickToPotato();
        }

        void StickToPotato()
        {
            if (m_InsertJoint != null)
                return;
            m_InsertJoint = gameObject.AddComponent<FixedJoint>();
            m_InsertJoint.connectedBody = m_Potato.body;
            m_InsertJoint.enableCollision = false;
        }

        void Unstick()
        {
            if (m_InsertJoint == null)
                return;
            Destroy(m_InsertJoint);
            m_InsertJoint = null;
        }

        void CreateWireTether()
        {
            if (m_WireAnchor == null || m_Terminal == null)
                return;

            // All three linear axes limited to the same distance = a rope: free within the wire's reach.
            var tether = gameObject.AddComponent<ConfigurableJoint>();
            tether.autoConfigureConnectedAnchor = false;
            tether.anchor = transform.InverseTransformPoint(m_Terminal.position);

            var anchorBody = m_WireAnchor.GetComponentInParent<Rigidbody>();
            if (anchorBody != null)
            {
                tether.connectedBody = anchorBody;
                tether.connectedAnchor = anchorBody.transform.InverseTransformPoint(m_WireAnchor.position);
            }
            else
            {
                tether.connectedAnchor = m_WireAnchor.position;
            }

            tether.xMotion = ConfigurableJointMotion.Limited;
            tether.yMotion = ConfigurableJointMotion.Limited;
            tether.zMotion = ConfigurableJointMotion.Limited;
            tether.angularXMotion = ConfigurableJointMotion.Free;
            tether.angularYMotion = ConfigurableJointMotion.Free;
            tether.angularZMotion = ConfigurableJointMotion.Free;
            tether.linearLimit = new SoftJointLimit { limit = m_WireLength };
            tether.enableCollision = true;
        }
    }
}
