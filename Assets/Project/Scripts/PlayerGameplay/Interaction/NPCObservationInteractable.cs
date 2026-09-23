using UnityEngine;

public class NPCObservationInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string _interactionPrompt = "집중 관찰하기";
    [SerializeField] private bool _canInteract = true;

    [Header("Observation")]
    [SerializeField] private Transform _focusPoint;

    public string InteractionPrompt => _interactionPrompt;
    public Transform FocusPoint => _focusPoint != null ? _focusPoint : transform;

    public bool CanInteract(GameObject interactor)
    {
        return _canInteract;
    }

    public void Interact(GameObject interactor)
    {
        PlayerViewModeController viewModeController =
            interactor.GetComponent<PlayerViewModeController>();

        if (viewModeController == null)
        {
            Debug.LogWarning(
                $"[Observation] {interactor.name}에 PlayerViewModeController가 없습니다.",
                interactor
            );
            return;
        }

        viewModeController.BeginObservation(this);
    }
}
