using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Shows electric current as small red spheres moving along a path, in the direction conventional
    /// current flows (from + to -). Their speed shows how much current there is: <see cref="speedScale"/> 1 is
    /// one bulb on one battery, 0.5 half that, and a negative value reverses the direction.
    /// A closed path loops around; an open path (one wire or one bulb) restarts at its first point, so
    /// visualizers on neighbouring parts of a circuit join up into one stream. Reusable by any exhibit.
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

        [SerializeField, Tooltip("How fast (m/s) the spheres travel at speed scale 1.")]
        float m_Speed = 0.1f;

        [SerializeField, Tooltip("True if the path is a whole loop (its last point joins its first).")]
        bool m_ClosedLoop = true;

        [SerializeField, Tooltip("Upper limit on spheres, for very long circuits.")]
        int m_MaxCharges = 120;

        readonly List<Vector3> m_Path = new List<Vector3>();
        readonly List<float> m_Cumulative = new List<float>();
        readonly List<Transform> m_Charges = new List<Transform>();
        float m_Travelled;

        /// <summary>When false the spheres are hidden (no current).</summary>
        public bool flowing { get; set; }

        /// <summary>Current relative to one bulb on one battery. Negative flows from the path's end to its start.</summary>
        public float speedScale { get; set; } = 1f;

        public bool closedLoop
        {
            get => m_ClosedLoop;
            set => m_ClosedLoop = value;
        }

        public Material chargeMaterial
        {
            get => m_ChargeMaterial;
            set => m_ChargeMaterial = value;
        }

        /// <summary>Sets the path, from the end current leaves to the end it arrives at (for positive speed).</summary>
        public void SetPath(IReadOnlyList<Vector3> points)
        {
            m_Path.Clear();
            for (int i = 0; i < points.Count; i++)
                m_Path.Add(points[i]);
        }

        void LateUpdate()
        {
            if (!flowing || m_Path.Count < 2 || Mathf.Abs(speedScale) < 0.01f)
            {
                SetVisibleCount(0);
                return;
            }

            int segments = m_ClosedLoop ? m_Path.Count : m_Path.Count - 1;
            m_Cumulative.Clear();
            m_Cumulative.Add(0f);
            float total = 0f;
            for (int i = 1; i <= segments; i++)
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
            float step = total / count; // Even spacing that wraps seamlessly.
            m_Travelled = Mathf.Repeat(m_Travelled + m_Speed * speedScale * Time.deltaTime, step);

            SetVisibleCount(count);
            int segment = 0;
            for (int i = 0; i < count; i++)
            {
                float distance = m_Travelled + i * step;
                while (segment < segments - 1 && m_Cumulative[segment + 1] < distance)
                    segment++;
                float segmentLength = m_Cumulative[segment + 1] - m_Cumulative[segment];
                float t = segmentLength > 0f ? Mathf.Clamp01((distance - m_Cumulative[segment]) / segmentLength) : 0f;
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
