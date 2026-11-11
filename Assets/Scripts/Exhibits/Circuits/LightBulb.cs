using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// A small light bulb on a holder with two posts. Its glow follows the current through it:
    /// brightness 1 is one bulb on one battery, 0.5 is half that current, 0 is off.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightBulb : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] CircuitTerminal m_TerminalA;
        [SerializeField] CircuitTerminal m_TerminalB;

        [SerializeField, Tooltip("The glass, which glows.")]
        Renderer m_Glass;

        [SerializeField] Light m_Light;

        [SerializeField, Tooltip("Path the current takes through the bulb: post A, up through the filament, post B.")]
        Transform[] m_Path;

        [SerializeField, Tooltip("Red spheres showing the current through the bulb.")]
        CurrentFlowVisualizer m_Flow;

        [SerializeField] Color m_OffColor = new Color(0.85f, 0.85f, 0.8f);
        [SerializeField] Color m_GlowColor = new Color(1f, 0.82f, 0.45f);

        [SerializeField, Tooltip("Emission at brightness 1. Keep near 1: the scene's bloom flares above that.")]
        float m_MaxEmission = 0.85f;

        [SerializeField] float m_MaxLightIntensity = 0.06f;

        readonly List<Vector3> m_Points = new List<Vector3>();
        MaterialPropertyBlock m_Block;
        float m_Brightness = -1f;

        public CircuitTerminal terminalA => m_TerminalA;

        public CircuitTerminal terminalB => m_TerminalB;

        public CurrentFlowVisualizer flow => m_Flow;

        public float brightness => Mathf.Max(0f, m_Brightness);

        void Awake() => SetBrightness(0f);

        /// <summary>World-space path from post A through the filament to post B.</summary>
        public IReadOnlyList<Vector3> path
        {
            get
            {
                m_Points.Clear();
                foreach (var point in m_Path)
                    m_Points.Add(point.position);
                return m_Points;
            }
        }

        public void SetBrightness(float value)
        {
            value = Mathf.Max(0f, value);
            if (Mathf.Abs(value - m_Brightness) < 0.005f)
                return;
            m_Brightness = value;

            float glow = Mathf.Clamp01(value);
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(k_BaseColor, Color.Lerp(m_OffColor, m_GlowColor, glow));
            m_Block.SetColor(k_EmissionColor, m_GlowColor * (m_MaxEmission * Mathf.Min(value, 1.3f)));
            if (m_Glass != null)
                m_Glass.SetPropertyBlock(m_Block);

            if (m_Light != null)
            {
                m_Light.enabled = value > 0.02f;
                m_Light.intensity = m_MaxLightIntensity * Mathf.Min(value, 1.3f);
            }
        }
    }
}
