using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DashboardInterface : MonoBehaviour, IInteractable
{
    public string InteractionPrompt => "게시판 열기";

    private bool _isInteractable = true;

    public bool CanInteract(GameObject interactor)
    {
        return
            _isInteractable;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        Debug.Log("Interacted with Dashboard");
    }
}
