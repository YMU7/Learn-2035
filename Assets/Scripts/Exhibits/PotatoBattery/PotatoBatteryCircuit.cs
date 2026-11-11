using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace XRClassroom.Exhibits.PotatoBattery
{
    /// <summary>
    /// Runs the potato battery experiment. The circuit is complete when both electrodes are pushed into the
    /// potato without touching each other. Then the LED lights and red charge spheres show conventional
    /// current: out of the copper (+) terminal, along the red wire, through the LED, back along the black
    /// wire to the zinc (-) terminal, and through the potato from zinc to copper to close the loop.
    /// </summary>
    [DefaultExecutionOrder(210)] // After the wires have updated their shapes this frame.
    [DisallowMultipleComponent]
    public sealed class PotatoBatteryCircuit : MonoBehaviour
    {
        enum State
        {
            NoneInserted,
            OneInserted,
            Touching,
            Complete,
        }

        [Header("Parts")]
        [SerializeField] Electrode m_Zinc;
        [SerializeField] Electrode m_Copper;

        [SerializeField, Tooltip("Wire from the copper (+) clip to the LED's + terminal.")]
        CircuitWire m_PositiveWire;

        [SerializeField, Tooltip("Wire from the LED's - terminal to the zinc (-) clip.")]
        CircuitWire m_NegativeWire;

        [Header("LED")]
        [SerializeField] Transform m_LedPositiveTerminal;
        [SerializeField] Transform m_LedBulbTop;
        [SerializeField] Transform m_LedNegativeTerminal;
        [SerializeField] Renderer m_LedBulb;
        [SerializeField] Material m_LedOffMaterial;
        [SerializeField] Material m_LedOnMaterial;
        [SerializeField] Light m_LedLight;

        [Header("Feedback")]
        [SerializeField] CurrentFlowVisualizer m_CurrentFlow;

        [SerializeField, Tooltip("Runs the exhibit's steps on the backsplash and its Hint / Next / Reset buttons.")]
        ExhibitGuide m_Guide;

        [SerializeField, Tooltip("The metals short out if their tips are closer than this (m).")]
        float m_MinElectrodeGap = 0.02f;

        [Header("Events")]
        [Tooltip("Raised with true when the circuit closes and false when it opens.")]
        public UnityEvent<bool> onCircuitChanged = new UnityEvent<bool>();

        readonly List<Vector3> m_Path = new List<Vector3>();
        State m_State = (State)(-1);

        /// <summary>True while current flows.</summary>
        public bool isComplete => m_State == State.Complete;

        void LateUpdate()
        {
            State state = Evaluate();
            if (state != m_State)
            {
                bool wasComplete = isComplete;
                m_State = state;
                ApplyState();
                if (wasComplete != isComplete)
                    onCircuitChanged.Invoke(isComplete);
            }

            if (m_CurrentFlow != null)
            {
                m_CurrentFlow.flowing = isComplete;
                if (isComplete)
                {
                    BuildCurrentPath();
                    m_CurrentFlow.SetPath(m_Path);
                }
            }
        }

        /// <summary>
        /// Completes the circuit without a headset: pushes both electrodes into the potato, 6 cm apart.
        /// Run it from this component's context menu in Play mode.
        /// </summary>
        [ContextMenu("Test: Push Both Electrodes Into Potato")]
        public void TestPushElectrodesIn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play mode to test the circuit.", this);
                return;
            }
            Transform potato = m_Zinc.potato.transform;
            Vector3 across = potato.right * 0.03f;
            m_Zinc.PushInto(potato.position - across);
            m_Copper.PushInto(potato.position + across);
        }

        State Evaluate()
        {
            bool zincIn = m_Zinc.isInserted;
            bool copperIn = m_Copper.isInserted;
            if (!zincIn && !copperIn)
                return State.NoneInserted;
            if (!zincIn || !copperIn)
                return State.OneInserted;
            if (Vector3.Distance(m_Zinc.tip.position, m_Copper.tip.position) < m_MinElectrodeGap)
                return State.Touching;
            return State.Complete;
        }

        void ApplyState()
        {
            bool on = isComplete;
            if (m_LedBulb != null)
                m_LedBulb.sharedMaterial = on ? m_LedOnMaterial : m_LedOffMaterial;
            if (m_LedLight != null)
                m_LedLight.enabled = on;
        }

        /// <summary>What the circuit is doing right now: a neutral observation, never the explanation.</summary>
        public string statusMessage => m_State switch
        {
            State.NoneInserted => "The LED is off. Neither metal is in the potato.",
            State.OneInserted => "The LED is off. One metal is in the potato.",
            State.Touching => "The LED is off. The two metals are touching.",
            _ => "The LED is on.",
        };

        void Start()
        {
            if (m_Guide != null)
                m_Guide.SetSteps(BuildSteps());
        }

        List<GuideStep> BuildSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep
                {
                    title = "A potato battery?",
                    prompt = "On the bench: a potato, a zinc nail and a copper strip, wired to a small light (an LED). " +
                             "What do you think will happen if you push both metals into the potato?",
                    hint = "Think about what a battery is made of. Which of those things can you find on this bench?",
                },
                new GuideStep
                {
                    title = "Try it",
                    prompt = "Grab the zinc nail and the copper strip by their clips and push their pointed ends into the potato. " +
                             "Watch the LED and the red spheres.",
                    hint = "Hold a metal by its coloured clip and push the pointed end into the potato. Try it with both metals.",
                    isComplete = () => isComplete,
                    liveFeedback = () => statusMessage,
                },
                new GuideStep
                {
                    title = "What do you think?",
                    prompt = "Why do you think the LED lit up? What do you think will happen if you pull one metal out, " +
                             "or let the two metals touch? Try it and watch the red spheres.",
                    hint = "Compare what the spheres do when the LED is on with what they do when it goes off.",
                    liveFeedback = () => statusMessage,
                },
            };
        }

        /// <summary>The whole loop in the direction of conventional current, starting at the copper clip.</summary>
        void BuildCurrentPath()
        {
            m_Path.Clear();
            // Copper (+) clip -> red wire -> LED + terminal.
            m_Path.AddRange(m_PositiveWire.points);
            // Through the LED.
            if (m_LedBulbTop != null)
                m_Path.Add(m_LedBulbTop.position);
            // LED - terminal -> black wire -> zinc (-) clip.
            m_Path.AddRange(m_NegativeWire.points);
            // Down the zinc, across the potato's juice to the copper, up the copper. The loop closes at the clip.
            m_Path.Add(m_Zinc.tip.position);
            m_Path.Add(m_Copper.tip.position);
        }
    }
}
