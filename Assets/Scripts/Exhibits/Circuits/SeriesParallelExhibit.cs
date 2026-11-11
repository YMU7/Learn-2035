using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// The series vs. parallel exhibit. One wiring board has a SERIES side and a PARALLEL side, each with its own
    /// battery and two bulbs, plus loose wires. Students wire both freely and compare them side by side. The
    /// guide asks open questions and only reports what it observes (which bulbs are lit), never the explanation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeriesParallelExhibit : MonoBehaviour
    {
        [SerializeField] ExhibitGuide m_Guide;

        [SerializeField, Tooltip("The board the student wires up.")]
        CircuitBoard m_Board;

        [Header("Series side")]
        [SerializeField] LightBulb m_SeriesBulb1;
        [SerializeField] LightBulb m_SeriesBulb2;

        [Header("Parallel side")]
        [SerializeField] LightBulb m_ParallelBulb1;
        [SerializeField] LightBulb m_ParallelBulb2;

        bool seriesBuilt => m_Board.AreInSeries(m_SeriesBulb1, m_SeriesBulb2);

        bool parallelBuilt => m_Board.AreInParallel(m_ParallelBulb1, m_ParallelBulb2);

        void Start() => m_Guide.SetSteps(BuildSteps());

        /// <summary>What the board is doing right now: neutral observations only.</summary>
        string Observations()
        {
            if (m_Board.hasShort)
                return "Careful: a wire joins a battery's + post straight to its - post.";
            string series = seriesBuilt ? "Series side: built." : $"Series side: {Lit(m_SeriesBulb1, m_SeriesBulb2)} of 2 bulbs lit.";
            string parallel = parallelBuilt ? "Parallel side: built." : $"Parallel side: {Lit(m_ParallelBulb1, m_ParallelBulb2)} of 2 bulbs lit.";
            return $"{series}     {parallel}";
        }

        static int Lit(LightBulb a, LightBulb b) => (a.brightness > 0.2f ? 1 : 0) + (b.brightness > 0.2f ? 1 : 0);

        List<GuideStep> BuildSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep
                {
                    title = "Two ways to connect",
                    prompt = "This board has a SERIES side (the bulbs share one path) and a PARALLEL side (each bulb gets its own path). " +
                             "What do you think will be different about the two?",
                    hint = "Think about the path the current takes from the battery's + post back to its - post.",
                },
                new GuideStep
                {
                    title = "Build a series circuit",
                    prompt = "On the SERIES side, make one single loop: from + through bulb 1, then bulb 2, and back to -. " +
                             "Grab a plug and let go on a metal post to clip it on.",
                    hint = "You need three wires on the series side: + to the first bulb, first bulb to second bulb, second bulb back to -.",
                    isComplete = () => seriesBuilt,
                    liveFeedback = Observations,
                },
                new GuideStep
                {
                    title = "Build a parallel circuit",
                    prompt = "On the PARALLEL side, give each bulb its own path from + to -. " +
                             "A post can hold two plugs, so wires can branch.",
                    hint = "Each bulb needs a connection to + on one post and to - on its other post. You can chain from one bulb's post to the other bulb's.",
                    isComplete = () => parallelBuilt,
                    liveFeedback = Observations,
                },
                new GuideStep
                {
                    title = "Compare",
                    prompt = "Compare the two sides. What do you notice about the bulbs? About the red spheres? " +
                             "What do you think will happen if you unplug one bulb on each side? Try it.",
                    hint = "Look at how fast the spheres move in each wire, and near each battery.",
                    liveFeedback = Observations,
                },
                new GuideStep
                {
                    title = "Why does it matter?",
                    prompt = "Why do you think building things in series or parallel matters?",
                    hint = "Think about the lights and plugs in your home, or a string of holiday lights.",
                },
            };
        }

        // ---------- Test hooks (Play mode, no headset) ----------

        /// <summary>Wires the series side: + to bulb 1, bulb 1 to bulb 2, bulb 2 to -.</summary>
        [ContextMenu("Test: Wire Series")]
        public void TestWireSeries()
        {
            var battery = m_Board.batteries[0];
            var w = m_Board.wires;
            Connect(w[0], battery.positive, m_SeriesBulb1.terminalA);
            Connect(w[1], m_SeriesBulb1.terminalB, m_SeriesBulb2.terminalA);
            Connect(w[2], m_SeriesBulb2.terminalB, battery.negative);
        }

        /// <summary>Wires the parallel side: both bulbs across the parallel battery.</summary>
        [ContextMenu("Test: Wire Parallel")]
        public void TestWireParallel()
        {
            var battery = m_Board.batteries[1];
            var w = m_Board.wires;
            Connect(w[3], battery.positive, m_ParallelBulb1.terminalA);
            Connect(w[4], m_ParallelBulb1.terminalA, m_ParallelBulb2.terminalA);
            Connect(w[5], m_ParallelBulb1.terminalB, battery.negative);
            Connect(w[6], m_ParallelBulb2.terminalB, m_ParallelBulb1.terminalB);
        }

        static void Connect(CircuitWireLink wire, CircuitTerminal from, CircuitTerminal to)
        {
            wire.plugA.ClipOnto(from);
            wire.plugB.ClipOnto(to);
        }

        /// <summary>Presses Next.</summary>
        [ContextMenu("Test: Next")]
        public void TestNext() => m_Guide.PressNext();
    }
}
