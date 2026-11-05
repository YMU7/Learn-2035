using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Remembers where an exhibit piece started (relative to the exhibit root) so the experiment can be repeated.
    /// The piece goes back when the exhibit's reset button is pressed, or by itself if it falls far below its start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitResettable : MonoBehaviour
    {
        static readonly List<ExhibitResettable> s_All = new List<ExhibitResettable>();

        [SerializeField, Tooltip("The exhibit this piece belongs to. Start poses are stored relative to it.")]
        Transform m_ExhibitRoot;

        [SerializeField, Tooltip("Reset automatically if the piece ends up this far (m) below its start, e.g. dropped off the bench.")]
        float m_FallDistance = 0.6f;

        Vector3 m_StartLocalPosition;
        Quaternion m_StartLocalRotation;
        Rigidbody m_Body;
        XRBaseInteractable m_Interactable;

        /// <summary>Raised just before the piece is moved back, e.g. so it can release joints.</summary>
        public event Action resetting;

        /// <summary>The exhibit this piece belongs to.</summary>
        public Transform exhibitRoot
        {
            get => m_ExhibitRoot;
            set => m_ExhibitRoot = value;
        }

        /// <summary>Resets every piece that belongs to <paramref name="exhibitRoot"/>.</summary>
        public static void ResetExhibit(Transform exhibitRoot)
        {
            // Copy first: resetting can change grab state, which must not modify the list mid-loop.
            foreach (var resettable in s_All.ToArray())
            {
                if (resettable.m_ExhibitRoot == exhibitRoot)
                    resettable.ResetPose();
            }
        }

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Interactable = GetComponent<XRBaseInteractable>();
            if (m_ExhibitRoot == null)
                m_ExhibitRoot = transform.parent;

            m_StartLocalPosition = m_ExhibitRoot.InverseTransformPoint(transform.position);
            m_StartLocalRotation = Quaternion.Inverse(m_ExhibitRoot.rotation) * transform.rotation;
        }

        void OnEnable() => s_All.Add(this);

        void OnDisable() => s_All.Remove(this);

        void FixedUpdate()
        {
            float startHeight = m_ExhibitRoot.TransformPoint(m_StartLocalPosition).y;
            if (transform.position.y < startHeight - m_FallDistance && !IsHeld())
                ResetPose();
        }

        bool IsHeld() => m_Interactable != null && m_Interactable.isSelected;

        /// <summary>Drops the piece if it is held and puts it back at its start pose, at rest.</summary>
        public void ResetPose()
        {
            if (IsHeld() && m_Interactable.interactionManager != null)
                m_Interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Interactable);

            resetting?.Invoke();

            Vector3 position = m_ExhibitRoot.TransformPoint(m_StartLocalPosition);
            Quaternion rotation = m_ExhibitRoot.rotation * m_StartLocalRotation;

            if (m_Body != null)
            {
                if (!m_Body.isKinematic)
                {
                    m_Body.linearVelocity = Vector3.zero;
                    m_Body.angularVelocity = Vector3.zero;
                }
                m_Body.position = position;
                m_Body.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
