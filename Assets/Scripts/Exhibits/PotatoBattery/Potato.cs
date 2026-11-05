using UnityEngine;

namespace XRClassroom.Exhibits.PotatoBattery
{
    /// <summary>
    /// The potato: a grabbable, egg-shaped body that electrodes can be pushed into.
    /// Its juice is the electrolyte that lets ions move between the zinc and copper.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class Potato : MonoBehaviour
    {
        [SerializeField, Tooltip("The visible potato: a Unity sphere (radius 0.5) scaled into an ellipsoid. It carries the collider.")]
        Transform m_Shape;

        [SerializeField, Tooltip("Lowers the centre of mass (m) so the potato stays upright with electrodes sticking out of it.")]
        float m_CenterOfMassDrop = 0.03f;

        Rigidbody m_Body;
        Collider m_Collider;

        void Awake()
        {
            body.centerOfMass = Vector3.down * m_CenterOfMassDrop;
        }

        public Rigidbody body => m_Body != null ? m_Body : m_Body = GetComponent<Rigidbody>();

        public Collider bodyCollider => m_Collider != null ? m_Collider : m_Collider = m_Shape.GetComponent<Collider>();

        /// <summary>
        /// True if the world-space point is inside the potato. Undoing the shape's scale turns the ellipsoid
        /// back into the unit sphere mesh (radius 0.5), so the test is exact for any scale.
        /// </summary>
        public bool Contains(Vector3 worldPoint)
        {
            return m_Shape.InverseTransformPoint(worldPoint).sqrMagnitude <= 0.25f;
        }
    }
}
