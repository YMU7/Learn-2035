using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Makes an exhibit's floor ring and its halo breathe gently, so the zone boundary reads as light
    /// rather than paint. Purely visual; the zone has no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitZoneGlow : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer[] m_Renderers;
        [SerializeField] Color m_Color = new Color(0.3f, 0.8f, 1f, 1f);

        [SerializeField, Tooltip("Seconds per breath.")]
        float m_Period = 3.5f;

        [SerializeField, Range(0f, 1f), Tooltip("How much dimmer the glow gets at the low point.")]
        float m_Depth = 0.35f;

        MaterialPropertyBlock m_Block;

        void Update()
        {
            m_Block ??= new MaterialPropertyBlock();
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / m_Period);
            var color = m_Color;
            color.a *= 1f - m_Depth * wave;
            m_Block.SetColor(k_BaseColor, color);
            foreach (var renderer in m_Renderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
