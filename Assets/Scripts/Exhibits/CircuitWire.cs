using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Draws a flexible wire between two points with a <see cref="LineRenderer"/>. The wire sags according
    /// to how much slack it has (its length minus the distance between its ends), and lies on the
    /// rest surface instead of passing through it. Purely visual: the physical length limit is a joint on
    /// the grabbable end (see <c>Electrode</c>).
    /// </summary>
    [DefaultExecutionOrder(200)] // After grabbed objects have moved this frame.
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class CircuitWire : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the wire starts. Current in the circuit flows from this end to the other.")]
        Transform m_From;

        [SerializeField, Tooltip("Where the wire ends.")]
        Transform m_To;

        [SerializeField, Tooltip("Length (m) of the wire. Keep it equal to the electrode's tether length.")]
        float m_Length = 0.6f;

        [SerializeField, Tooltip("Optional surface (e.g. the bench top) the wire lies on instead of passing through.")]
        Transform m_RestSurface;

        [SerializeField, Range(4, 64)]
        int m_Segments = 24;

        LineRenderer m_Line;
        readonly List<Vector3> m_Points = new List<Vector3>();

        /// <summary>The wire's current points, from <c>From</c> to <c>To</c>, in world space.</summary>
        public IReadOnlyList<Vector3> points => m_Points;

        public float length
        {
            get => m_Length;
            set => m_Length = value;
        }

        void Awake()
        {
            m_Line = GetComponent<LineRenderer>();
            m_Line.useWorldSpace = true;
        }

        void LateUpdate() => Rebuild();

        /// <summary>Recomputes the wire's shape from its two ends.</summary>
        public void Rebuild()
        {
            if (m_From == null || m_To == null)
                return;

            Vector3 a = m_From.position;
            Vector3 b = m_To.position;
            float distance = Vector3.Distance(a, b);
            float slack = Mathf.Max(0f, m_Length - distance);

            // A shallow parabola of span d and sag s has length of about d + 8s^2 / 3d, so s = sqrt(3 d slack / 8).
            float sag = Mathf.Sqrt(3f * Mathf.Max(distance, 0.05f) * slack / 8f);
            // The control point of a quadratic Bezier sits twice as far down as the curve's midpoint.
            Vector3 control = (a + b) * 0.5f + Vector3.down * (2f * sag);

            float floor = m_RestSurface != null ? m_RestSurface.position.y + 0.004f : float.NegativeInfinity;

            m_Points.Clear();
            for (int i = 0; i <= m_Segments; i++)
            {
                float t = i / (float)m_Segments;
                float u = 1f - t;
                Vector3 point = u * u * a + 2f * u * t * control + t * t * b;
                // Lie on the bench rather than sinking into it, unless an end itself is below the bench.
                float limit = Mathf.Min(floor, Mathf.Min(a.y, b.y));
                if (point.y < limit)
                    point.y = limit;
                m_Points.Add(point);
            }

            if (m_Line == null)
                m_Line = GetComponent<LineRenderer>();
            m_Line.positionCount = m_Points.Count;
            for (int i = 0; i < m_Points.Count; i++)
                m_Line.SetPosition(i, m_Points[i]);
        }
    }
}
