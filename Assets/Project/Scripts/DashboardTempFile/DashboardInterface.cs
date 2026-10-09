using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DashboardInterface : MonoBehaviour, IInteractable
{
    [SerializeField]
    private VotingPanel _votingPanel;

    private bool _isInteractable = true;

    public string InteractionPrompt => "게시판 열기";
    public bool CanInteract(GameObject interactor)
    {
        return
            VoteManager.Instance.IsVotingOpen;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        UIManager.Instance.Open(_votingPanel);
    }
}
