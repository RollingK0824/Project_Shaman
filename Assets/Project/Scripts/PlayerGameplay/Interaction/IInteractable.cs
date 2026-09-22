using UnityEngine;

public interface IInteractable
{
    string InteractionPrompt { get; }

    bool CanInteract(GameObject interactor);

    void Interact(GameObject interactor);
}