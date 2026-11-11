using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Lets either controller button press whatever the ray is pointing at, not just a poke:
    /// <list type="bullet">
    /// <item>Trigger while pointing at a 3D exhibit button presses it (grip already does, by selecting it).</item>
    /// <item>Grip while pointing at a 2D UI button (wardrobe garments, Connect / Disconnect) clicks it
    /// (the trigger already does, through the XR UI input module).</item>
    /// </list>
    /// Added automatically at runtime to every NearFarInteractor on the rig, so no scene or prefab is edited.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RayButtonActivator : MonoBehaviour
    {
        NearFarInteractor m_Interactor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AddToInteractors();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToInteractors();

        static void AddToInteractors()
        {
            foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!interactor.TryGetComponent<RayButtonActivator>(out _))
                    interactor.gameObject.AddComponent<RayButtonActivator>();
            }
        }

        void Awake() => m_Interactor = GetComponent<NearFarInteractor>();

        void Update()
        {
            if (m_Interactor == null || !m_Interactor.isActiveAndEnabled)
                return;

            if (m_Interactor.activateInput != null && m_Interactor.activateInput.ReadWasPerformedThisFrame())
            {
                foreach (var hovered in m_Interactor.interactablesHovered)
                {
                    if (hovered is Component component && component.TryGetComponent(out ExhibitPokeButton button))
                    {
                        button.Press();
                        break;
                    }
                }
            }

            if (m_Interactor.selectInput != null && m_Interactor.selectInput.ReadWasPerformedThisFrame() &&
                !m_Interactor.hasSelection &&
                m_Interactor.TryGetCurrentUIRaycastResult(out var result) && result.gameObject != null)
            {
                var uiButton = result.gameObject.GetComponentInParent<Button>();
                if (uiButton != null && uiButton.IsInteractable())
                    uiButton.onClick.Invoke();
            }
        }
    }
}
