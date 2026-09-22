using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string _interactionPrompt = "조사하기";
    [SerializeField] private bool _canInteract = true;

    public string InteractionPrompt => _interactionPrompt;

    public bool CanInteract(GameObject interactor)
    {
        return _canInteract;
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log(
            $"[Interaction] {interactor.name} → " +
            $"{gameObject.name} / {_interactionPrompt}",
            gameObject
        );
    }
}