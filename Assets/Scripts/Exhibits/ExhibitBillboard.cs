using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// A blank white display panel beside an exhibit, for lesson slides or topic info.
    /// To show an image, import it as a texture and drag it into <b>Slide</b>. Leave it empty for a plain white panel.
    /// The screen is 16:9 (1.6 m x 0.9 m), so 16:9 slide exports fit without stretching.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ExhibitBillboard : MonoBehaviour
    {
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Image shown on the screen. Leave empty for a blank white panel. The screen is 16:9.")]
        Texture m_Slide;

        [SerializeField, Tooltip("Renderer of the screen quad that shows the slide.")]
        Renderer m_Screen;

        /// <summary>Image shown on the screen, or null for a blank panel.</summary>
        public Texture slide
        {
            get => m_Slide;
            set
            {
                m_Slide = value;
                Apply();
            }
        }

        /// <summary>Renderer of the screen quad.</summary>
        public Renderer screen
        {
            get => m_Screen;
            set
            {
                m_Screen = value;
                Apply();
            }
        }

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        void Apply()
        {
            if (m_Screen == null)
                return;

            // A property block keeps the shared white material untouched, so every billboard can show its own image.
            var block = new MaterialPropertyBlock();
            if (m_Slide != null)
                block.SetTexture(k_BaseMap, m_Slide);
            block.SetColor(k_BaseColor, Color.white);
            m_Screen.SetPropertyBlock(block);
        }
    }
}
