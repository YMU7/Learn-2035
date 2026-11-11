using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// Batteries, bulbs and wires on a board. Every frame it works out how current flows through whatever the
    /// student has wired up, lights each bulb by the current through it, and shows the current in every wire,
    /// bulb and battery with moving red spheres.
    ///
    /// Under the hood it is an ordinary resistor network (each post is a node; bulbs are resistors; wires are
    /// near-zero resistors; each battery is a source with a small internal resistance), solved by node voltages.
    /// None of the numbers are shown: only "more current" (faster spheres, brighter bulb) and "less".
    /// </summary>
    [DefaultExecutionOrder(210)] // After the cables have their shapes, before the visualizers move spheres.
    [DisallowMultipleComponent]
    public sealed class CircuitBoard : MonoBehaviour
    {
        [Serializable]
        public sealed class Battery
        {
            public CircuitTerminal positive;
            public CircuitTerminal negative;

            [Tooltip("Path inside the battery from - to +, for its current spheres.")]
            public Transform[] path;

            public CurrentFlowVisualizer flow;
        }

        const float k_Voltage = 1f;
        const float k_BulbResistance = 1f;
        const float k_WireResistance = 0.02f;
        const float k_BatteryResistance = 0.02f;
        const double k_Leak = 1e-6; // Keeps unconnected posts from making the equations unsolvable.

        [SerializeField] Battery[] m_Batteries;
        [SerializeField] LightBulb[] m_Bulbs;
        [SerializeField] CircuitWireLink[] m_Wires;

        [SerializeField, Tooltip("Unpowered boards show nothing.")]
        bool m_Powered = true;

        readonly Dictionary<CircuitTerminal, int> m_Nodes = new Dictionary<CircuitTerminal, int>();
        readonly List<Vector3> m_Points = new List<Vector3>();
        double[,] m_Matrix; // Doubles: a battery not wired to the first one floats, which float precision handles poorly.
        double[] m_Rhs;
        double[] m_Voltages;
        float[] m_BulbCurrents;
        int[] m_Nets;

        /// <summary>Current from one bulb alone on one battery. Brightness and sphere speed are relative to it.</summary>
        static float referenceCurrent => k_Voltage / (k_BulbResistance + k_BatteryResistance + 2f * k_WireResistance);

        public bool powered
        {
            get => m_Powered;
            set => m_Powered = value;
        }

        public IReadOnlyList<LightBulb> bulbs => m_Bulbs;

        public IReadOnlyList<CircuitWireLink> wires => m_Wires;

        public IReadOnlyList<Battery> batteries => m_Batteries;

        /// <summary>True while some battery's + post is wired straight to its - post (a short circuit).</summary>
        public bool hasShort { get; private set; }

        /// <summary>How many bulbs are lit right now.</summary>
        public int litCount { get; private set; }

        void Awake()
        {
            // Node 0 is the reference (0 V): the first battery's - post.
            foreach (var battery in m_Batteries)
            {
                AddNode(battery.negative);
                AddNode(battery.positive);
            }
            foreach (var bulb in m_Bulbs)
            {
                AddNode(bulb.terminalA);
                AddNode(bulb.terminalB);
            }
            int unknowns = m_Nodes.Count - 1;
            m_Matrix = new double[unknowns, unknowns];
            m_Rhs = new double[unknowns];
            m_Voltages = new double[m_Nodes.Count];
            m_BulbCurrents = new float[m_Bulbs.Length];
            m_Nets = new int[m_Nodes.Count];
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
                Array.Clear(m_Voltages, 0, m_Voltages.Length);
            FindNets();

            float reference = referenceCurrent;

            hasShort = false;
            foreach (var battery in m_Batteries)
            {
                float across = Voltage(battery.positive) - Voltage(battery.negative);
                float relative = m_Powered ? (k_Voltage - across) / k_BatteryResistance / reference : 0f;
                if (relative > 3f)
                    hasShort = true;
                SetFlow(battery.flow, battery.path, relative);
            }

            int lit = 0;
            for (int i = 0; i < m_Bulbs.Length; i++)
            {
                var bulb = m_Bulbs[i];
                float relative = (Voltage(bulb.terminalA) - Voltage(bulb.terminalB)) / k_BulbResistance / reference;
                m_BulbCurrents[i] = relative;
                bulb.SetBrightness(Mathf.Abs(relative));
                if (bulb.flow != null)
                {
                    bulb.flow.speedScale = relative;
                    bulb.flow.flowing = Mathf.Abs(relative) > 0.05f;
                    bulb.flow.SetPath(bulb.path);
                }
                if (IsLit(i))
                    lit++;
            }
            litCount = lit;

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
        }

        bool IsLit(int bulb) => Mathf.Abs(m_BulbCurrents[bulb]) > 0.2f;

        /// <summary>
        /// True if the two bulbs are lit in series: one after another on a single path, each carrying the same,
        /// reduced current.
        /// </summary>
        public bool AreInSeries(LightBulb a, LightBulb b)
        {
            int i = Array.IndexOf(m_Bulbs, a), j = Array.IndexOf(m_Bulbs, b);
            if (i < 0 || j < 0 || !IsLit(i) || !IsLit(j))
                return false;
            float ia = Mathf.Abs(m_BulbCurrents[i]), ib = Mathf.Abs(m_BulbCurrents[j]);
            bool shareOnePost = SameNet(a.terminalA, b.terminalA) || SameNet(a.terminalA, b.terminalB) ||
                                SameNet(a.terminalB, b.terminalA) || SameNet(a.terminalB, b.terminalB);
            return shareOnePost && Mathf.Abs(ia - ib) < 0.1f * Mathf.Max(ia, ib) && ia < 0.75f;
        }

        /// <summary>True if the two bulbs are lit in parallel: both connected across the same two points.</summary>
        public bool AreInParallel(LightBulb a, LightBulb b)
        {
            int i = Array.IndexOf(m_Bulbs, a), j = Array.IndexOf(m_Bulbs, b);
            if (i < 0 || j < 0 || !IsLit(i) || !IsLit(j))
                return false;
            bool sameEnds = (SameNet(a.terminalA, b.terminalA) && SameNet(a.terminalB, b.terminalB)) ||
                            (SameNet(a.terminalA, b.terminalB) && SameNet(a.terminalB, b.terminalA));
            return sameEnds;
        }

        bool SameNet(CircuitTerminal a, CircuitTerminal b) =>
            m_Nodes.TryGetValue(a, out int x) && m_Nodes.TryGetValue(b, out int y) && m_Nets[x] == m_Nets[y];

        /// <summary>Groups posts joined by wires (union-find), so we can tell which bulbs share connection points.</summary>
        void FindNets()
        {
            for (int i = 0; i < m_Nets.Length; i++)
                m_Nets[i] = i;
            foreach (var wire in m_Wires)
            {
                if (!wire.isConnected || !m_Nodes.TryGetValue(wire.plugA.attached, out int a) || !m_Nodes.TryGetValue(wire.plugB.attached, out int b))
                    continue;
                int ra = Root(a), rb = Root(b);
                if (ra != rb)
                    m_Nets[ra] = rb;
            }
            for (int i = 0; i < m_Nets.Length; i++)
                m_Nets[i] = Root(i);
        }

        int Root(int i)
        {
            while (m_Nets[i] != i)
                i = m_Nets[i] = m_Nets[m_Nets[i]];
            return i;
        }

        float Voltage(CircuitTerminal terminal) => terminal != null && m_Nodes.TryGetValue(terminal, out int node) ? (float)m_Voltages[node] : 0f;

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

        /// <summary>Node-voltage analysis: G V = I, with the first battery's - post as 0 V.</summary>
        void Solve()
        {
            int size = m_Rhs.Length;
            Array.Clear(m_Matrix, 0, m_Matrix.Length);
            Array.Clear(m_Rhs, 0, size);
            for (int i = 0; i < size; i++)
                m_Matrix[i, i] = k_Leak;

            // Each battery as its Norton equivalent: internal conductance plus a current source into its + post.
            foreach (var battery in m_Batteries)
            {
                int plus = m_Nodes[battery.positive];
                int minus = m_Nodes[battery.negative];
                AddConductance(plus, minus, 1f / k_BatteryResistance);
                AddCurrent(plus, k_Voltage / k_BatteryResistance);
                AddCurrent(minus, -k_Voltage / k_BatteryResistance);
            }

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
                    if (Math.Abs(m_Matrix[row, col]) > Math.Abs(m_Matrix[pivot, col]))
                        pivot = row;
                }
                if (pivot != col)
                {
                    for (int k = 0; k < size; k++)
                        (m_Matrix[col, k], m_Matrix[pivot, k]) = (m_Matrix[pivot, k], m_Matrix[col, k]);
                    (m_Rhs[col], m_Rhs[pivot]) = (m_Rhs[pivot], m_Rhs[col]);
                }
                double diagonal = m_Matrix[col, col];
                if (Math.Abs(diagonal) < 1e-12)
                    continue;
                for (int row = col + 1; row < size; row++)
                {
                    double factor = m_Matrix[row, col] / diagonal;
                    if (factor == 0.0)
                        continue;
                    for (int k = col; k < size; k++)
                        m_Matrix[row, k] -= factor * m_Matrix[col, k];
                    m_Rhs[row] -= factor * m_Rhs[col];
                }
            }
            for (int row = size - 1; row >= 0; row--)
            {
                double sum = m_Rhs[row];
                for (int k = row + 1; k < size; k++)
                    sum -= m_Matrix[row, k] * m_Voltages[k + 1];
                m_Voltages[row + 1] = Math.Abs(m_Matrix[row, row]) > 1e-12 ? sum / m_Matrix[row, row] : 0.0;
            }
            m_Voltages[0] = 0.0;
        }

        // Node 0 is the reference, so it has no row: unknown index = node - 1.
        void AddConductance(int a, int b, double g)
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

        void AddCurrent(int node, double amps)
        {
            if (node > 0)
                m_Rhs[node - 1] += amps;
        }
    }
}
