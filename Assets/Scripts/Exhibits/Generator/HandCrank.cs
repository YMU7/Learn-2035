using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XRClassroom.Exhibits.Generator
{
    /// <summary>
    /// A crank handle that turns around an axle. Grab the knob and move your hand in a circle: the crank
    /// follows your hand around the axle (local x axis of the pivot). Let go and it coasts to a stop like
    /// a flywheel. Uses the rig's normal select (grip) through an <see cref="XRSimpleInteractable"/>, so
    /// nothing is physically pulled off its axle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandCrank : MonoBehaviour
    {
        [SerializeField, Tooltip("Rotates around its local x axis. The arm and knob are its children.")]
        Transform m_Pivot;

        [SerializeField, Tooltip("The knob the student grabs.")]
        XRSimpleInteractable m_Knob;

        [SerializeField, Tooltip("How quickly a released crank slows down (1/s).")]
        float m_Friction = 1.5f;

        Quaternion m_RestRotation;
        float m_Angle;
        float m_GrabOffset;
        float m_Velocity;
        IXRSelectInteractor m_Hand;

        /// <summary>Total crank angle in degrees (keeps counting past 360).</summary>
        public float angle => m_Angle;

        /// <summary>Signed turning speed in revolutions per second.</summary>
        public float revolutionsPerSecond => m_Velocity / 360f;

        public bool isHeld => m_Hand != null;

        void Awake() => m_RestRotation = m_Pivot.localRotation;

        void OnEnable()
        {
            m_Knob.selectEntered.AddListener(OnGrabbed);
            m_Knob.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Knob.selectEntered.RemoveListener(OnGrabbed);
            m_Knob.selectExited.RemoveListener(OnReleased);
            m_Hand = null;
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            m_Hand = args.interactorObject;
            m_GrabOffset = Mathf.DeltaAngle(HandAngle(), m_Angle);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject == m_Hand)
                m_Hand = null;
        }

        /// <summary>Angle of the hand around the axle, in the pivot's parent space.</summary>
        float HandAngle()
        {
            Vector3 hand = m_Hand.GetAttachTransform(m_Knob).position;
            Vector3 local = m_Pivot.parent.InverseTransformPoint(hand) - m_Pivot.localPosition;
            local = Quaternion.Inverse(m_RestRotation) * local;
            // Rotating (0, 1, 0) by +angle about x gives (0, cos, sin).
            return Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (m_Hand != null)
            {
                float target = HandAngle() + m_GrabOffset;
                float delta = Mathf.DeltaAngle(m_Angle, target);
                m_Angle += delta;
                // Smooth the measured speed a little so the light does not flicker with hand jitter.
                m_Velocity = Mathf.Lerp(m_Velocity, delta / dt, 1f - Mathf.Exp(-10f * dt));
            }
            else if (Time.time < m_DriveUntil || Time.time < m_RemoteHeldUntil)
            {
                // Driven by a test hook, or by another player's hand (see HandCrankSync): keep the speed
                // between updates.
                m_Angle += m_Velocity * dt;
            }
            else
            {
                m_Velocity *= Mathf.Exp(-m_Friction * dt);
                if (Mathf.Abs(m_Velocity) < 5f)
                    m_Velocity = 0f;
                m_Angle += m_Velocity * dt;
            }
            m_Pivot.localRotation = m_RestRotation * Quaternion.Euler(m_Angle, 0f, 0f);
        }

        float m_DriveUntil;
        float m_RemoteHeldUntil; // A remote hand counts as holding until its updates stop for half a second.

        /// <summary>True while this headset is turning the crank (a hand on the knob, or a test hook).</summary>
        public bool isDrivenLocally => m_Hand != null || Time.time < m_DriveUntil;

        /// <summary>
        /// Another player's crank: jump to their angle and speed. Ignored while a local hand holds the knob.
        /// When <paramref name="held"/> is false the crank coasts to a stop on its own, as if released here.
        /// </summary>
        public void ApplyRemote(float remoteAngle, float revolutionsPerSecond, bool held)
        {
            if (isDrivenLocally)
                return;
            m_Angle = remoteAngle;
            m_Velocity = revolutionsPerSecond * 360f;
            m_RemoteHeldUntil = held ? Time.time + 0.5f : 0f;
        }

        /// <summary>For testing without a headset: turns the crank at this many revolutions per second for a while.</summary>
        public void Spin(float revolutionsPerSecond, float seconds = 10f)
        {
            m_Velocity = revolutionsPerSecond * 360f;
            m_DriveUntil = Time.time + seconds;
        }

        /// <summary>Stops the crank where it is.</summary>
        public void Stop()
        {
            m_Velocity = 0f;
            m_DriveUntil = 0f;
        }
    }
}
