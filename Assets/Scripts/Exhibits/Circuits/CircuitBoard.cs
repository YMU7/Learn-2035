using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// A battery, some bulbs and some wires on a board. Every frame it works out how current flows through
    /// whatever the student has wired up, lights each bulb by the current through it, and shows the current
    /// in every wire, bulb and the battery with moving red spheres.
    ///
    /// Under the hood it is an ordinary resistor network (each post is a node; bulbs are resistors; wires are
    /// near-zero resistors; the battery is a source with a small internal resistance), solved by node voltages.
    /// None of the numbers are shown: only "more current" (faster spheres, brighter bulb) and "less".
    /// </summary>
    [DefaultExecutionOrder(210)] // After the cables have their shapes, before the visualizers move spheres.
    [DisallowMultipleComponent]
    public sealed class CircuitBoard : MonoBehaviour
    {
        public enum Layout
        {
            /// <summary>No complete loop: nothing lights.</summary>
            Open,
            /// <summary>A path from + to - with no bulb in it: the current skips the bulbs.</summary>
            Short,
            /// <summary>Exactly one bulb lit.</summary>
            OneBulb,
            /// <summary>Both bulbs lit on one shared path (each dimmer).</summary>
            Series,
            /// <summary>Both bulbs lit, each on its own path (each as bright as a single bulb).</summary>
            Parallel,
        }

        const float k_Voltage = 1f;
        const float k_BulbResistance = 1f;
        const float k_WireResistance = 0.02f;
        const float k_BatteryResistance = 0.02f;
        const float k_Leak = 1e-6f; // Keeps unconnected posts from making the equations unsolvable.

        [SerializeField] CircuitTerminal m_BatteryPositive;
        [SerializeField] CircuitTerminal m_BatteryNegative;

        [SerializeField, Tooltip("Path inside the battery from - to +, for its current spheres.")]
        Transform[] m_BatteryPath;

        [SerializeField] CurrentFlowVisualizer m_BatteryFlow;

        [SerializeField] LightBulb[] m_Bulbs;

        [SerializeField] CircuitWireLink[] m_Wires;

        [SerializeField, Tooltip("Unpowered boards show nothing (used to hide answers until the student picks one).")]
        bool m_Powered = true;

        readonly Dictionary<CircuitTerminal, int> m_Nodes = new Dictionary<CircuitTerminal, int>();
        readonly List<Vector3> m_Points = new List<Vector3>();
        float[,] m_Matrix;
        float[] m_Rhs;
        float[] m_Voltages;

        /// <summary>Current from one bulb alone on the battery. Brightness and sphere speed are relative to it.</summary>
        static float referenceCurrent => k_Voltage / (k_BulbResistance + k_BatteryResistance + 2f * k_WireResistance);

        public bool powered
        {
            get => m_Powered;
            set => m_Powered = value;
        }

        public Layout layout { get; private set; }

        /// <summary>Battery current relative to one bulb alone (2 = feeding two bulbs at full brightness).</summary>
        public float batteryCurrent { get; private set; }

        public IReadOnlyList<LightBulb> bulbs => m_Bulbs;

        public IReadOnlyList<CircuitWireLink> wires => m_Wires;

        public CircuitTerminal batteryPositive => m_BatteryPositive;

        public CircuitTerminal batteryNegative => m_BatteryNegative;

        void Awake()
        {
            AddNode(m_BatteryNegative); // Node 0 is the reference (0 V).
            AddNode(m_BatteryPositive);
            foreach (var bulb in m_Bulbs)
            {
                AddNode(bulb.terminalA);
                AddNode(bulb.terminalB);
            }
            int unknowns = m_Nodes.Count - 1;
            m_Matrix = new float[unknowns, unknowns];
            m_Rhs = new float[unknowns];
            m_Voltages = new float[m_Nodes.Count];
        }

        void AddNode(CircuitTerminal terminal)
        {
            if (terminal != null && !m_Nodes.ContainsKey(terminal))
                m_Nodes.Add(terminal, m_Nodes.Count);
        }

        void LateUpdate()
        {
            if (m_Powered)
                Solve();
            else
                System.Array.Clear(m_Voltages, 0, m_Voltages.Length);

            float reference = referenceCurrent;

            // Battery: the current it pushes out of + (and in at -).
            float positive = m_Voltages[m_Nodes[m_BatteryPositive]];
            float batteryAmps = m_Powered ? (k_Voltage - positive) / k_BatteryResistance : 0f;
            batteryCurrent = batteryAmps / reference;
            SetFlow(m_BatteryFlow, m_BatteryPath, batteryCurrent);

            // Bulbs: brightness and spheres follow the current through each, from post A to post B.
            int lit = 0;
            float dimmest = float.MaxValue;
            foreach (var bulb in m_Bulbs)
            {
                float amps = (Voltage(bulb.terminalA) - Voltage(bulb.terminalB)) / k_BulbResistance;
                float relative = amps / reference;
                bulb.SetBrightness(Mathf.Abs(relative));
                if (bulb.flow != null)
                {
                    bulb.flow.speedScale = relative;
                    bulb.flow.flowing = Mathf.Abs(relative) > 0.05f;
                    bulb.flow.SetPath(bulb.path);
                }
                if (Mathf.Abs(relative) > 0.2f)
                {
                    lit++;
                    dimmest = Mathf.Min(dimmest, Mathf.Abs(relative));
                }
            }

            // Wires: spheres along each connected cable, from plug A to plug B.
            foreach (var wire in m_Wires)
            {
                float relative = 0f;
                if (m_Powered && wire.isConnected && m_Nodes.ContainsKey(wire.plugA.attached) && m_Nodes.ContainsKey(wire.plugB.attached))
                    relative = (Voltage(wire.plugA.attached) - Voltage(wire.plugB.attached)) / k_WireResistance / reference;
                if (wire.flow != null)
                {
                    wire.flow.speedScale = Mathf.Clamp(relative, -4f, 4f);
                    wire.flow.flowing = Mathf.Abs(relative) > 0.05f;
                    if (wire.cable != null)
                        wire.flow.SetPath(wire.cable.points);
                }
            }

            if (batteryCurrent > 3f)
                layout = Layout.Short;
            else if (lit == 0)
                layout = Layout.Open;
            else if (lit == 1 || lit < m_Bulbs.Length)
                layout = Layout.OneBulb;
            else
                layout = dimmest < 0.75f ? Layout.Series : Layout.Parallel;
        }

        float Voltage(CircuitTerminal terminal) => m_Nodes.TryGetValue(terminal, out int node) ? m_Voltages[node] : 0f;

        void SetFlow(CurrentFlowVisualizer flow, Transform[] path, float relative)
        {
            if (flow == null || path == null)
                return;
            flow.speedScale = Mathf.Clamp(relative, -4f, 4f);
            flow.flowing = Mathf.Abs(relative) > 0.05f;
            m_Points.Clear();
            foreach (var point in path)
                m_Points.Add(point.position);
            flow.SetPath(m_Points);
        }

        /// <summary>Node-voltage analysis: G V = I, with the battery's - post as 0 V.</summary>
        void Solve()
        {
            int size = m_Rhs.Length;
            System.Array.Clear(m_Matrix, 0, m_Matrix.Length);
            System.Array.Clear(m_Rhs, 0, size);
            for (int i = 0; i < size; i++)
                m_Matrix[i, i] = k_Leak;

            // Battery as its Norton equivalent: internal conductance plus a current source into the + post.
            int plus = m_Nodes[m_BatteryPositive];
            int minus = m_Nodes[m_BatteryNegative];
            AddConductance(plus, minus, 1f / k_BatteryResistance);
            AddCurrent(plus, k_Voltage / k_BatteryResistance);
            AddCurrent(minus, -k_Voltage / k_BatteryResistance);

            foreach (var bulb in m_Bulbs)
                AddConductance(m_Nodes[bulb.terminalA], m_Nodes[bulb.terminalB], 1f / k_BulbResistance);

            foreach (var wire in m_Wires)
            {
                if (!wire.isConnected)
                    continue;
                if (m_Nodes.TryGetValue(wire.plugA.attached, out int a) && m_Nodes.TryGetValue(wire.plugB.attached, out int b))
                    AddConductance(a, b, 1f / k_WireResistance);
            }

            // Gaussian elimination with partial pivoting (a handful of nodes, so this is tiny).
            for (int col = 0; col < size; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < size; row++)
                {
                    if (Mathf.Abs(m_Matrix[row, col]) > Mathf.Abs(m_Matrix[pivot, col]))
                        pivot = row;
                }
                if (pivot != col)
                {
                    for (int k = 0; k < size; k++)
                        (m_Matrix[col, k], m_Matrix[pivot, k]) = (m_Matrix[pivot, k], m_Matrix[col, k]);
                    (m_Rhs[col], m_Rhs[pivot]) = (m_Rhs[pivot], m_Rhs[col]);
                }
                float diagonal = m_Matrix[col, col];
                if (Mathf.Abs(diagonal) < 1e-12f)
                    continue;
                for (int row = col + 1; row < size; row++)
                {
                    float factor = m_Matrix[row, col] / diagonal;
                    if (factor == 0f)
                        continue;
                    for (int k = col; k < size; k++)
                        m_Matrix[row, k] -= factor * m_Matrix[col, k];
                    m_Rhs[row] -= factor * m_Rhs[col];
                }
            }
            for (int row = size - 1; row >= 0; row--)
            {
                float sum = m_Rhs[row];
                for (int k = row + 1; k < size; k++)
                    sum -= m_Matrix[row, k] * m_Voltages[k + 1];
                m_Voltages[row + 1] = Mathf.Abs(m_Matrix[row, row]) > 1e-12f ? sum / m_Matrix[row, row] : 0f;
            }
            m_Voltages[0] = 0f;
        }

        // Node 0 (battery -) is the reference, so it has no row: unknown index = node - 1.
        void AddConductance(int a, int b, float g)
        {
            if (a == b)
                return;
            if (a > 0) m_Matrix[a - 1, a - 1] += g;
            if (b > 0) m_Matrix[b - 1, b - 1] += g;
            if (a > 0 && b > 0)
            {
                m_Matrix[a - 1, b - 1] -= g;
                m_Matrix[b - 1, a - 1] -= g;
            }
        }

        void AddCurrent(int node, float amps)
        {
            if (node > 0)
                m_Rhs[node - 1] += amps;
        }
    }
}
