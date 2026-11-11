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

        /// <summary>A neutral observation of the bulb, never the explanation.</summary>
        string BulbStatus()
        {
            if (m_Output > 0.6f)
                return "The bulb is glowing brightly.";
            if (m_Output > 0.02f)
                return "The bulb is glowing dimly.";
            return "The bulb is dark.";
        }

        List<GuideStep> BuildSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep
                {
                    title = "What do you think?",
                    prompt = "This crank is wired to a light bulb, but there is no battery anywhere. " +
                             "What do you think will happen if you turn the crank?",
                    hint = "What does a bulb need in order to light up?",
                    onEnter = () =>
                    {
                        m_Revolutions = 0f;
                    },
                },
                new GuideStep
                {
                    title = "Try it",
                    prompt = "Grab the knob and turn the crank. Try slowly, then quickly, then let go. What changes?",
                    hint = "Hold the knob and make big circles around the axle, like pedalling a bike with your hand.",
                    onEnter = () =>
                    {
                        m_Revolutions = 0f;
                    },
                    isComplete = () => m_Revolutions >= 3f,
                    liveFeedback = BulbStatus,
                },
                new GuideStep
                {
                    title = "Where does it come from?",
                    prompt = "There is still no battery. Where do you think the bulb's electricity comes from?",
                    hint = "What did you have to keep doing to keep the bulb lit? What happened when you stopped?",
                    liveFeedback = BulbStatus,
                },
                new GuideStep
                {
                    title = "Look inside",
                    prompt = "The box is open. What do you see inside? Turn the crank and watch closely. " +
                             "How do you think the parts inside make the bulb glow?",
                    hint = "Watch the red spheres and the parts that move. When do the spheres move, and when do they stop?",
                    onEnter = () => SetRevealed(true),
                    liveFeedback = BulbStatus,
                },
            };
        }

        /// <summary>For testing without a headset: turns the crank at 1.5 turns per second for ten seconds.</summary>
        [ContextMenu("Test: Turn Crank")]
        public void TestTurnCrank() => m_Crank.Spin(1.5f);

        /// <summary>For testing without a headset: presses Next.</summary>
        [ContextMenu("Test: Next")]
        public void TestNext() => m_Guide.PressNext();
    }
}
