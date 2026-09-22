using UnityEngine;

public class InspectInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField]
    private string _interactionPrompt =
        "자세히 조사하기";

    [SerializeField] private bool _canInteract = true;

    [Header("Inspect Pose")]
    [SerializeField]
    private Vector3 _inspectLocalPosition =
        Vector3.zero;

    [SerializeField]
    private Vector3 _inspectLocalRotation =
        Vector3.zero;

    [SerializeField]
    private Vector3 _inspectLocalScale =
        Vector3.one;

    public string InteractionPrompt =>
        _interactionPrompt;

    public Vector3 InspectLocalPosition =>
        _inspectLocalPosition;

    public Vector3 InspectLocalRotation =>
        _inspectLocalRotation;

    public Vector3 InspectLocalScale =>
        _inspectLocalScale;

    public bool CanInteract(GameObject interactor)
    {
        return _canInteract;
    }

    public void Interact(GameObject interactor)
    {
        PlayerInspectController inspectController =
            interactor.GetComponent<PlayerInspectController>();

        if (inspectController == null)
        {
            Debug.LogWarning(
                $"[Inspect] {interactor.name}에 " +
                "PlayerInspectController가 없습니다.",
                interactor
            );

            return;
        }

        inspectController.BeginInspection(this);
    }
}