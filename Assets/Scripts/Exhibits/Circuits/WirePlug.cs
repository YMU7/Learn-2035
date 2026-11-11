using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// One end of a wire. Grab it and let go near a battery or bulb post and it clips on; grab it again to
    /// unplug it. Locked plugs (on pre-built example circuits) start clipped on and cannot be grabbed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    [DisallowMultipleComponent]
    public sealed class WirePlug : MonoBehaviour
    {
        [SerializeField, Tooltip("Post this plug starts clipped onto (and returns to on reset). Optional.")]
        CircuitTerminal m_StartTerminal;

        [SerializeField, Tooltip("Let go within this distance (m) of a post to clip on.")]
        float m_SnapRadius = 0.05f;

        [SerializeField, Tooltip("Locked plugs stay on their start post and cannot be grabbed.")]
        bool m_Locked;

        [SerializeField, Tooltip("Where the cable leaves the plug.")]
        Transform m_CableEnd;

        Rigidbody m_Body;
        XRGrabInteractable m_Grab;

        /// <summary>The post this plug is clipped onto, or null.</summary>
        public CircuitTerminal attached { get; private set; }

        public Transform cableEnd => m_CableEnd != null ? m_CableEnd : transform;

        public Rigidbody body => m_Body;

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
            {
                resettable.resetting += Detach;
                resettable.resetCompleted += ClipOntoStart;
            }
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrabbed);
            m_Grab.selectExited.RemoveListener(OnReleased);
            if (TryGetComponent(out ExhibitResettable resettable))
            {
                resettable.resetting -= Detach;
                resettable.resetCompleted -= ClipOntoStart;
            }
        }

        void Start()
        {
            if (m_Locked)
                m_Grab.enabled = false;
            ClipOntoStart();
        }

        void ClipOntoStart()
        {
            if (m_StartTerminal != null)
                ClipOnto(m_StartTerminal);
        }

        void OnGrabbed(SelectEnterEventArgs args) => Detach();

        void OnReleased(SelectExitEventArgs args)
        {
            var terminal = CircuitTerminal.FindNearestFree(transform.position, m_SnapRadius);
            if (terminal != null)
                ClipOnto(terminal);
            else
                DropToSurface();
        }

        /// <summary>
        /// Plugs never collide with anything (they are kinematic and moved straight to the hand), so they cannot
        /// snag on the bench, posts or bulbs. When let go away from a post, a plug drops straight down onto
        /// whatever is below it and rests there upright.
        /// </summary>
        void DropToSurface()
        {
            m_Body.isKinematic = true;
            var origin = transform.position + Vector3.up * 0.02f;
            float nearest = float.MaxValue;
            Vector3 point = transform.position;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<WirePlug>() != null || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                point = hit.point;
            }
            var upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            m_Body.position = point;
            m_Body.rotation = upright;
            transform.SetPositionAndRotation(point, upright);
        }

        /// <summary>Clips this plug onto a post (also used by test hooks).</summary>
        public void ClipOnto(CircuitTerminal terminal)
        {
            Detach();
            var slot = terminal.Attach(this);
            if (slot == null)
                return;
            attached = terminal;
            m_Body.isKinematic = true;
            m_Body.position = slot.position;
            m_Body.rotation = slot.rotation;
            transform.SetPositionAndRotation(slot.position, slot.rotation);
        }

        void Detach()
        {
            if (attached == null)
                return;
            attached.Detach(this);
            attached = null;
        }
    }
}
