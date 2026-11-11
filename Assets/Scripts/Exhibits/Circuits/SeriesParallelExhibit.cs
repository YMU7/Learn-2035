using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// The series vs. parallel exhibit. Before each build, two example circuits (A and B) sit on the bench,
    /// unpowered, and the student picks which one will work; then both power up to show why. Then the
    /// student wires their own circuit freely on the board, with live feedback on what they built.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeriesParallelExhibit : MonoBehaviour
    {
        [SerializeField] ExhibitGuide m_Guide;

        [SerializeField, Tooltip("The board the student wires up.")]
        CircuitBoard m_FreeBoard;

        [Header("Example circuits")]
        [SerializeField] GameObject m_SeriesExamples;
        [SerializeField] CircuitBoard m_SeriesA;
        [SerializeField] CircuitBoard m_SeriesB;
        [SerializeField] GameObject m_ParallelExamples;
        [SerializeField] CircuitBoard m_ParallelA;
        [SerializeField] CircuitBoard m_ParallelB;

        void Start()
        {
            m_Guide.SetSteps(BuildSteps());
        }

        void ShowExamples(GameObject examples, CircuitBoard a, CircuitBoard b)
        {
            m_SeriesExamples.SetActive(examples == m_SeriesExamples);
            m_ParallelExamples.SetActive(examples == m_ParallelExamples);
            foreach (var board in new[] { m_SeriesA, m_SeriesB, m_ParallelA, m_ParallelB })
                board.powered = false;
        }

        void PowerExamples(CircuitBoard a, CircuitBoard b)
        {
            a.powered = true;
            b.powered = true;
        }

        string FreeBoardStatus(CircuitBoard.Layout goal)
        {
            switch (m_FreeBoard.layout)
            {
                case CircuitBoard.Layout.Short:
                    return "Short circuit! A wire joins + straight to -, so the current rushes through it and skips the bulbs.";
                case CircuitBoard.Layout.Open:
                    return "Nothing lights yet: there is no complete loop from + all the way back to -.";
                case CircuitBoard.Layout.OneBulb:
                    return "One bulb is lit. Find a way to bring the other bulb into the circuit too.";
                case CircuitBoard.Layout.Series when goal == CircuitBoard.Layout.Series:
                    return "Series circuit! There is only ONE path, so the same current goes through both bulbs. Two bulbs " +
                           "resist more than one, so less current flows: see the slower spheres? Both bulbs glow dimmer.";
                case CircuitBoard.Layout.Series:
                    return "That's the series circuit from before: one path through both bulbs. Now give each bulb its own path.";
                case CircuitBoard.Layout.Parallel when goal == CircuitBoard.Layout.Parallel:
                    return "Parallel circuit! Each bulb has its OWN path, so each gets as much current as a bulb alone: both " +
                           "glow bright. Near the battery the spheres move twice as fast, because it feeds both paths.";
                case CircuitBoard.Layout.Parallel:
                    return "Both bulbs are bright: that's a parallel circuit (each bulb has its own path). We'll get there! " +
                           "For now, make one single loop through both bulbs.";
                default:
                    return "";
            }
        }

        List<GuideStep> BuildSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep
                {
                    title = "Series circuits",
                    prompt = "In a SERIES circuit, the bulbs sit one after another on a single loop. " +
                             "Look at circuits A and B at the back. Which one will light both bulbs?",
                    choices = new[] { "Circuit A", "Circuit B" },
                    choiceFeedback = new[]
                    {
                        "Not quite: in A, the last wire goes back to the same + post it started from. Current must " +
                        "travel from + all the way round to -, so nothing lights. B makes one full loop.",
                        "Yes! B makes one full loop: + to the first bulb, on to the second bulb, then back to -.",
                    },
                    hint = "Start at the battery's + post and follow the wires with your eyes. Can you get all the way to the - post, going through both bulbs?",
                    onEnter = () => ShowExamples(m_SeriesExamples, m_SeriesA, m_SeriesB),
                    onAnswered = _ => PowerExamples(m_SeriesA, m_SeriesB),
                },
                new GuideStep
                {
                    title = "Build a series circuit",
                    prompt = "Now build your own on the front board. Grab a wire's plug and let go on a metal post to clip " +
                             "it on. Make ONE loop that goes through both bulbs.",
                    hint = "You need three wires: + to the first bulb, the first bulb to the second bulb, and the second bulb back to -.",
                    onEnter = () => ShowExamples(null, null, null),
                    isComplete = () => m_FreeBoard.layout == CircuitBoard.Layout.Series,
                    liveFeedback = () => FreeBoardStatus(CircuitBoard.Layout.Series),
                },
                new GuideStep
                {
                    title = "Parallel circuits",
                    prompt = "In a PARALLEL circuit, each bulb gets its own path to the battery. " +
                             "Look at circuits A and B at the back. Which one will light both bulbs?",
                    choices = new[] { "Circuit A", "Circuit B" },
                    choiceFeedback = new[]
                    {
                        "Yes! In A, each bulb has its own loop from + to -. Notice both bulbs are as bright as a bulb on its own.",
                        "Not quite: B has an extra wire straight from + to -. That's a SHORT CIRCUIT: the current takes the " +
                        "easy path and skips the bulbs, so they stay dark. Real batteries get hot when you do this!",
                    },
                    hint = "Look for a wire that joins + directly to - with no bulb in between. Current always prefers the easiest path.",
                    onEnter = () => ShowExamples(m_ParallelExamples, m_ParallelA, m_ParallelB),
                    onAnswered = _ => PowerExamples(m_ParallelA, m_ParallelB),
                },
                new GuideStep
                {
                    title = "Build a parallel circuit",
                    prompt = "Rewire the front board so each bulb has its own path from + to -. " +
                             "A post can hold two plugs, so wires can branch.",
                    hint = "Connect the left posts of both bulbs to +, and the right posts of both bulbs to -. You can also chain: + to bulb 1, bulb 1 to bulb 2 (same side).",
                    onEnter = () => ShowExamples(null, null, null),
                    isComplete = () => m_FreeBoard.layout == CircuitBoard.Layout.Parallel,
                    liveFeedback = () => FreeBoardStatus(CircuitBoard.Layout.Parallel),
                },
                new GuideStep
                {
                    title = "Compare and explore",
                    prompt = "Try unplugging one wire from a bulb. In series, both bulbs go out (the only path is broken). " +
                             "In parallel, the other bulb stays on. Which way do you think your home's lights are wired?",
                    hint = "If one lamp in your house burns out, do all the others go dark? That tells you which kind of circuit it is.",
                    onEnter = () => ShowExamples(null, null, null),
                    liveFeedback = () => FreeBoardStatus(m_FreeBoard.layout),
                },
            };
        }

        /// <summary>For testing without a headset: wires the front board as a series circuit.</summary>
        [ContextMenu("Test: Wire Series")]
        public void TestWireSeries()
        {
            var w = m_FreeBoard.wires;
            var b = m_FreeBoard.bulbs;
            Connect(w[0], m_FreeBoard.batteryPositive, b[0].terminalA);
            Connect(w[1], b[0].terminalB, b[1].terminalA);
            Connect(w[2], b[1].terminalB, m_FreeBoard.batteryNegative);
        }

        /// <summary>For testing without a headset: wires the front board as a parallel circuit.</summary>
        [ContextMenu("Test: Wire Parallel")]
        public void TestWireParallel()
        {
            var w = m_FreeBoard.wires;
            var b = m_FreeBoard.bulbs;
            Connect(w[0], m_FreeBoard.batteryPositive, b[0].terminalA);
            Connect(w[1], b[0].terminalA, b[1].terminalA);
            Connect(w[2], b[0].terminalB, m_FreeBoard.batteryNegative);
            Connect(w[3], b[1].terminalB, b[0].terminalB);
        }

        static void Connect(CircuitWireLink wire, CircuitTerminal from, CircuitTerminal to)
        {
            wire.plugA.ClipOnto(from);
            wire.plugB.ClipOnto(to);
        }

        /// <summary>For testing without a headset: answers the current question with choice 0 (A).</summary>
        [ContextMenu("Test: Answer A")]
        public void TestAnswerA() => m_Guide.Answer(0);

        /// <summary>For testing without a headset: presses Next.</summary>
        [ContextMenu("Test: Next")]
        public void TestNext() => m_Guide.PressNext();
    }
}
