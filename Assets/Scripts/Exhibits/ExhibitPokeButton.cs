using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// A round push button on an exhibit's control strip (Hint, Next, answer choices, Reset).
    /// Press it by poking it with a finger or controller tip, or by pointing at it and selecting.
    /// Disabled buttons turn grey and ignore presses.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    [DisallowMultipleComponent]
    public sealed class ExhibitPokeButton : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("The whole button (cap, base and label). Hidden by SetVisible.")]
        GameObject m_Root;

        [SerializeField, Tooltip("Renderer of the cap, recoloured when the button is disabled.")]
        Renderer m_CapRenderer;

        [SerializeField, Tooltip("Text printed next to the button.")]
        TMP_Text m_Label;

        [SerializeField]
        Color m_Color = new Color(0.85f, 0.12f, 0.1f);

        [SerializeField]
        Color m_DisabledColor = new Color(0.55f, 0.55f, 0.57f);

        [SerializeField, Tooltip("How far (m) the cap dips when pressed.")]
        float m_PressDepth = 0.006f;

        XRSimpleInteractable m_Interactable;
        Vector3 m_RestPosition;
        bool m_IsEnabled = true;
        float m_LastPress = -10f;
        MaterialPropertyBlock m_Block;

        /// <summary>Raised when the button is pressed while enabled.</summary>
        public event Action pressed;

        /// <summary>Disabled buttons are grey and do nothing when pressed.</summary>
        public bool interactable
        {
            get => m_IsEnabled;
            set
            {
                if (m_IsEnabled == value && m_Block != null)
                    return;
                m_IsEnabled = value;
                ApplyColor();
            }
        }

        public void SetLabel(string text)
        {
            if (m_Label != null && m_Label.text != text)
                m_Label.text = text;
        }

        public void SetVisible(bool visible)
        {
            var root = m_Root != null ? m_Root : gameObject;
            if (root.activeSelf != visible)
                root.SetActive(visible);
        }

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            m_RestPosition = transform.localPosition;
            ApplyColor();
        }

        void OnEnable() => m_Interactable.selectEntered.AddListener(OnSelected);

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelected);
            transform.localPosition = m_RestPosition;
        }

        void OnSelected(SelectEnterEventArgs args) => Press();

        /// <summary>
        /// Presses the button: a poke, a select (grip) or a trigger pull while pointing at it
        /// (see <see cref="RayButtonActivator"/>), or code such as a test hook.
        /// </summary>
        public void Press()
        {
            // A poke can select several times while the finger wobbles on the cap; one press per 0.3 s.
            if (!m_IsEnabled || Time.unscaledTime - m_LastPress < 0.3f)
                return;
            m_LastPress = Time.unscaledTime;
            if (isActiveAndEnabled)
            {
                StopAllCoroutines();
                StartCoroutine(Dip());
            }
            pressed?.Invoke();
        }

        IEnumerator Dip()
        {
            transform.localPosition = m_RestPosition + transform.localRotation * Vector3.down * m_PressDepth;
            yield return new WaitForSeconds(0.2f);
            transform.localPosition = m_RestPosition;
        }

        void ApplyColor()
        {
            if (m_CapRenderer == null)
                return;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(k_BaseColor, m_IsEnabled ? m_Color : m_DisabledColor);
            m_CapRenderer.SetPropertyBlock(m_Block);
        }
    }
}
