using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Shows electric current as small red spheres moving along a closed circuit path.
    /// The owning circuit hands it a path each frame (in the direction conventional current flows,
    /// from + to -) and turns <see cref="flowing"/> on when the circuit is complete.
    /// Reusable for any exhibit that has a circuit.
    /// </summary>
    [DefaultExecutionOrder(220)] // After the circuit has set this frame's path.
    [DisallowMultipleComponent]
    public sealed class CurrentFlowVisualizer : MonoBehaviour
    {
        [SerializeField, Tooltip("Material of the moving charge spheres.")]
        Material m_ChargeMaterial;

        [SerializeField, Tooltip("Diameter (m) of each charge sphere.")]
        float m_ChargeDiameter = 0.014f;

        [SerializeField, Tooltip("Distance (m) between neighbouring spheres along the path.")]
        float m_Spacing = 0.04f;

        [SerializeField, Tooltip("How fast (m/s) the spheres travel along the circuit.")]
        float m_Speed = 0.1f;

        [SerializeField, Tooltip("Upper limit on spheres, for very long circuits.")]
        int m_MaxCharges = 120;

        readonly List<Vector3> m_Path = new List<Vector3>();
        readonly List<float> m_Cumulative = new List<float>();
        readonly List<Transform> m_Charges = new List<Transform>();
        float m_Travelled;

        /// <summary>When false the spheres are hidden (open circuit, no current).</summary>
        public bool flowing { get; set; }

        /// <summary>
        /// Sets the circuit path in the direction current flows. The path is treated as a closed loop:
        /// spheres leaving the last point continue from the first.
        /// </summary>
        public void SetPath(List<Vector3> points)
        {
            m_Path.Clear();
            m_Path.AddRange(points);
        }

        void LateUpdate()
        {
            if (!flowing || m_Path.Count < 2)
            {
                SetVisibleCount(0);
                return;
            }

            // Cumulative distance along the closed loop (last point connects back to the first).
            m_Cumulative.Clear();
            float total = 0f;
            m_Cumulative.Add(0f);
            for (int i = 1; i <= m_Path.Count; i++)
            {
                total += Vector3.Distance(m_Path[i - 1], m_Path[i % m_Path.Count]);
                m_Cumulative.Add(total);
            }
            if (total < 0.001f)
            {
                SetVisibleCount(0);
                return;
            }

            int count = Mathf.Clamp(Mathf.FloorToInt(total / m_Spacing), 1, m_MaxCharges);
            float step = total / count; // Even spacing that wraps seamlessly around the loop.
            m_Travelled = (m_Travelled + m_Speed * Time.deltaTime) % step;

            SetVisibleCount(count);
            int segment = 0;
            for (int i = 0; i < count; i++)
            {
                float distance = m_Travelled + i * step;
                while (segment < m_Path.Count - 1 && m_Cumulative[segment + 1] < distance)
                    segment++;
                float segmentLength = m_Cumulative[segment + 1] - m_Cumulative[segment];
                float t = segmentLength > 0f ? (distance - m_Cumulative[segment]) / segmentLength : 0f;
                m_Charges[i].position = Vector3.Lerp(m_Path[segment], m_Path[(segment + 1) % m_Path.Count], t);
            }
        }

        void SetVisibleCount(int count)
        {
            while (m_Charges.Count < count)
                m_Charges.Add(CreateCharge());

            for (int i = 0; i < m_Charges.Count; i++)
            {
                bool visible = i < count;
                if (m_Charges[i].gameObject.activeSelf != visible)
                    m_Charges[i].gameObject.SetActive(visible);
            }
        }

        Transform CreateCharge()
        {
            var charge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            charge.name = "Charge";
            Destroy(charge.GetComponent<Collider>());
            var renderer = charge.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m_ChargeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            charge.transform.SetParent(transform, false);
            charge.transform.localScale = Vector3.one * m_ChargeDiameter;
            return charge.transform;
        }
    }
}
