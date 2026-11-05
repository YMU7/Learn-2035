using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// A push button that puts every <see cref="ExhibitResettable"/> piece of an exhibit back where it started.
    /// Press it by poking it with a finger or controller tip, or by pointing at it and selecting.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    [DisallowMultipleComponent]
    public sealed class ExhibitResetButton : MonoBehaviour
    {
        [SerializeField, Tooltip("The exhibit whose pieces this button resets.")]
        Transform m_ExhibitRoot;

        [SerializeField, Tooltip("The moving part of the button. It dips briefly when pressed.")]
        Transform m_Cap;

        [SerializeField, Tooltip("How far (m) the cap dips when pressed.")]
        float m_PressDepth = 0.006f;

        XRSimpleInteractable m_Interactable;
        Vector3 m_CapRestPosition;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            if (m_Cap != null)
                m_CapRestPosition = m_Cap.localPosition;
        }

        void OnEnable() => m_Interactable.selectEntered.AddListener(OnPressed);

        void OnDisable() => m_Interactable.selectEntered.RemoveListener(OnPressed);

        void OnPressed(SelectEnterEventArgs args)
        {
            ExhibitResettable.ResetExhibit(m_ExhibitRoot);
            if (m_Cap != null)
            {
                StopAllCoroutines();
                StartCoroutine(Dip());
            }
        }

        IEnumerator Dip()
        {
            m_Cap.localPosition = m_CapRestPosition + Vector3.down * m_PressDepth;
            yield return new WaitForSeconds(0.2f);
            m_Cap.localPosition = m_CapRestPosition;
        }
    }
}
