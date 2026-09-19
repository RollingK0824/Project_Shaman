using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExorcismFlagInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField]
    private string _interactionPrompt =
        "의식 깃발 활성화";

    [Header("State")]
    [SerializeField] private bool _ritualActive;

    public bool IsActivated { get; private set; }

    public bool IsRitualActive => _ritualActive;

    public string InteractionPrompt
    {
        get
        {
            if (IsActivated)
            {
                return "이미 활성화된 깃발";
            }

            if (!_ritualActive)
            {
                return "현재 활성화할 수 없음";
            }

            return _interactionPrompt;
        }
    }

    public event Action<
        ExorcismFlagInteractable,
        GameObject
    > Activated;

    public bool CanInteract(GameObject interactor)
    {
        return
            _ritualActive &&
            !IsActivated;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        Activate(interactor);
    }

    public void SetRitualActive(bool isActive)
    {
        _ritualActive = isActive;

        Debug.Log(
            $"[ExorcismFlag] {gameObject.name} / " +
            $"RitualActive = {_ritualActive}",
            gameObject
        );
    }

    public void ResetFlag()
    {
        IsActivated = false;

        Debug.Log(
            $"[ExorcismFlag] 초기화: {gameObject.name}",
            gameObject
        );
    }

    private void Activate(GameObject activator)
    {
        if (IsActivated)
        {
            return;
        }

        IsActivated = true;

        string activatorName =
            activator != null
                ? activator.name
                : "Unknown";

        Debug.Log(
            $"[ExorcismFlag] 활성화: " +
            $"{gameObject.name} / " +
            $"Player = {activatorName}",
            gameObject
        );

        Activated?.Invoke(
            this,
            activator
        );
    }
}