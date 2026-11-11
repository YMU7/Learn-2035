using System.Collections.Generic;
using UnityEngine;
using XRClassroom.Exhibits.Circuits;

namespace XRClassroom.Exhibits.Generator
{
    /// <summary>
    /// The hand crank generator exhibit. Turning the crank spins a magnet between two coils inside a box;
    /// the faster it turns, the more current flows to the bulb. The last step opens the box and lights up
    /// an energy sign: mechanical energy (your arm) to electrical energy (moving charges) to light.
    /// It behaves like a simple DC generator: turning backwards makes the current flow the other way.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrankGeneratorExhibit : MonoBehaviour
    {
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] ExhibitGuide m_Guide;
        [SerializeField] HandCrank m_Crank;
        [SerializeField] LightBulb m_Bulb;

        [SerializeField, Tooltip("Spins with the crank (around its local x axis).")]
        Transform m_Magnet;

        [SerializeField, Tooltip("Magnet turns per crank turn (a gear inside the box).")]
        float m_GearRatio = 3f;

        [Header("Current")]
        [SerializeField, Tooltip("Wire from the generator's + post to the bulb.")]
        CircuitWire m_WireOut;

        [SerializeField, Tooltip("Wire from the bulb back to the generator's - post.")]
        CircuitWire m_WireBack;

        [SerializeField, Tooltip("Path through the coils inside the box, from the - post round to the + post.")]
        Transform[] m_CoilPath;

        [SerializeField] CurrentFlowVisualizer m_Flow;

        [Header("Output")]
        [SerializeField, Tooltip("Below this many turns per second the bulb stays dark.")]
        float m_MinRevolutionsPerSecond = 0.15f;

        [SerializeField, Tooltip("Turns per second for full brightness.")]
        float m_FullRevolutionsPerSecond = 1.5f;

        [Header("Reveal")]
        [SerializeField, Tooltip("Box panels hidden in the last step to show the magnet and coils.")]
        GameObject[] m_Cover;

        [SerializeField, Tooltip("Sign shown in the last step: mechanical -> electrical -> light.")]
        GameObject m_EnergySign;

        [SerializeField, Tooltip("Sign panels and arrows that glow while energy flows, in order along the chain.")]
        Renderer[] m_EnergyGlow;

        [SerializeField] Color m_EnergyGlowColor = new Color(1f, 0.75f, 0.3f);

        readonly List<Vector3> m_Path = new List<Vector3>();
        MaterialPropertyBlock m_Block;
        float m_LastAngle;
        float m_Revolutions;
        float m_Output;
        bool m_WasGenerating;

        /// <summary>Brightness of the bulb right now (0 = dark, 1 = full).</summary>
        public float output => m_Output;

        void Start()
        {
            m_LastAngle = m_Crank.angle;
            m_Guide.restarted += () => SetRevealed(false);
            SetRevealed(false);
            m_Guide.SetSteps(BuildSteps());
        }

        void SetRevealed(bool revealed)
        {
            foreach (var panel in m_Cover)
                panel.SetActive(!revealed);
            if (m_EnergySign != null)
                m_EnergySign.SetActive(revealed);
        }

        void LateUpdate()
        {
            float rps = m_Crank.revolutionsPerSecond;
            m_Output = Mathf.Clamp((Mathf.Abs(rps) - m_MinRevolutionsPerSecond) /
                (m_FullRevolutionsPerSecond - m_MinRevolutionsPerSecond), 0f, 1.3f);
            m_Bulb.SetBrightness(m_Output);

            float angle = m_Crank.angle;
            if (m_Output > 0.3f)
                m_Revolutions += Mathf.Abs(angle - m_LastAngle) / 360f;
            m_LastAngle = angle;
            if (m_Output > 0.3f)
                m_WasGenerating = true;

            if (m_Magnet != null)
                m_Magnet.localRotation = Quaternion.Euler(angle * m_GearRatio, 0f, 0f);

            // One loop: + post, out to the bulb, through it, back to the - post, through the coils to +.
            m_Path.Clear();
            m_Path.AddRange(m_WireOut.points);
            m_Path.AddRange(m_Bulb.path);
            m_Path.AddRange(m_WireBack.points);
            foreach (var point in m_CoilPath)
                m_Path.Add(point.position);
            m_Flow.SetPath(m_Path);
            m_Flow.flowing = m_Output > 0.02f;
            m_Flow.speedScale = Mathf.Sign(rps) * Mathf.Max(m_Output, 0.3f);

            if (m_EnergySign != null && m_EnergySign.activeInHierarchy)
            {
                m_Block ??= new MaterialPropertyBlock();
                float glow = Mathf.Clamp01(m_Output);
                foreach (var renderer in m_EnergyGlow)
                {
                    m_Block.SetColor(k_EmissionColor, m_EnergyGlowColor * glow);
                    renderer.SetPropertyBlock(m_Block);
                }
            }
        }

        string TryItStatus()
        {
            if (m_Output > 0.3f)
                return "It glows! The faster you turn, the brighter the bulb.";
            if (m_Output > 0.02f)
                return "A little faster! Turning slowly makes only a little electricity.";
            if (m_WasGenerating && !m_Crank.isHeld)
                return "You stopped turning, so the electricity stopped and the bulb went dark.";
            return "Grab the knob and turn the crank in circles.";
        }

        List<GuideStep> BuildSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep
                {
                    title = "What will happen?",
                    prompt = "This crank is wired to a light bulb, but there is no battery anywhere. " +
                             "What do you think happens if you turn the crank?",
                    choices = new[] { "The bulb lights up", "Nothing happens", "It lights only while you turn" },
                    choiceFeedback = new[]
                    {
                        "Let's test it. Watch what happens when you stop turning, too.",
                        "Let's test it. You might be surprised!",
                        "Good thinking! Let's test it.",
                    },
                    hint = "A bulb lights when charges flow through it. Could turning something make charges move?",
                    onEnter = () =>
                    {
                        m_Revolutions = 0f;
                        m_WasGenerating = false;
                    },
                },
                new GuideStep
                {
                    title = "Try it",
                    prompt = "Grab the knob on the crank and turn it in circles. Try slowly, then fast. Then let go and watch the bulb.",
                    hint = "Hold the knob and make big circles around the axle, like pedalling a bike with your hand.",
                    onEnter = () =>
                    {
                        m_Revolutions = 0f;
                        m_WasGenerating = false;
                    },
                    isComplete = () => m_Revolutions >= 3f,
                    liveFeedback = TryItStatus,
                },
                new GuideStep
                {
                    title = "Where does it come from?",
                    prompt = "The bulb only glows while you turn the crank. So where does its electricity come from?",
                    choices = new[] { "A hidden battery", "Your arm turning the crank", "The bulb makes it" },
                    choiceFeedback = new[]
                    {
                        "There's no battery. A battery would keep the bulb lit after you stopped turning.",
                        "Yes! The energy comes from you: the machine turns the motion of your arm into electricity.",
                        "A bulb can't make its own electricity: it went dark as soon as you stopped turning.",
                    },
                    hint = "What did you have to keep doing to keep the bulb lit? Where did that effort go?",
                },
                new GuideStep
                {
                    title = "Look inside",
                    prompt = "The box is open! The crank spins a MAGNET between two COILS of copper wire. A moving " +
                             "magnet pushes the charges in the wire along: that's electricity. Turn the crank and follow the sign.",
                    hint = "Watch the red spheres: they only move while the magnet spins. No motion, no electricity. Try turning the other way, too.",
                    onEnter = () => SetRevealed(true),
                    liveFeedback = () => m_Output > 0.02f
                        ? "Your arm (mechanical energy) spins the magnet. That pushes charges round the wire (electrical energy), and the bulb turns it into light."
                        : "Turn the crank and watch the magnet spin past the coils.",
                },
            };
        }

        /// <summary>For testing without a headset: turns the crank at 1.5 turns per second for ten seconds.</summary>
        [ContextMenu("Test: Turn Crank")]
        public void TestTurnCrank() => m_Crank.Spin(1.5f);

        /// <summary>For testing without a headset: answers the current question with choice B.</summary>
        [ContextMenu("Test: Answer B")]
        public void TestAnswerB() => m_Guide.Answer(1);

        /// <summary>For testing without a headset: presses Next.</summary>
        [ContextMenu("Test: Next")]
        public void TestNext() => m_Guide.PressNext();
    }
}
