using UnityEngine;

public class NPCObservationInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField]
    private string _interactionPrompt =
        "집중 관찰하기";

    [SerializeField] private bool _canInteract = true;

    [Header("Observation")]
    [SerializeField] private Transform _focusPoint;

    public string InteractionPrompt =>
        _interactionPrompt;

    public Transform FocusPoint =>
        _focusPoint != null
            ? _focusPoint
            : transform;

    public bool CanInteract(GameObject interactor)
    {
        return _canInteract;
    }

    public void Interact(GameObject interactor)
    {
        PlayerObservationController observationController =
            interactor.GetComponent<PlayerObservationController>();

        if (observationController == null)
        {
            Debug.LogWarning(
                $"[Observation] {interactor.name}에 " +
                "PlayerObservationController가 없습니다.",
                interactor
            );

            return;
        }

        observationController.BeginObservation(this);
    }
}