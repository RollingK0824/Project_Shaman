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

    // 의식 활성 / 깃발 활성 상태가 바뀔때 (네트워크 동기화)
    public event Action<ExorcismFlagInteractable> StateChanged;

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
            PlayerActionGuard.CanAct(interactor) &&
            _ritualActive &&
            !IsActivated;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        // 직접 활성화 x
        PlayerEvents.RaiseFlagActivateRequested(interactor, this);

        //Activate(interactor);
    }

    public void SetRitualActive(bool isActive)
    {
        _ritualActive = isActive;

        Debug.Log(
            $"[ExorcismFlag] {gameObject.name} / " +
            $"RitualActive = {_ritualActive}",
            gameObject
        );

        StateChanged?.Invoke(this);
    }

    public void ResetFlag()
    {
        IsActivated = false;

        Debug.Log(
            $"[ExorcismFlag] 초기화: {gameObject.name}",
            gameObject
        );

        StateChanged?.Invoke(this);
    }

    // [판정자 전용] 서버 또는 오프라인 판정기가 활성화를 확정할 때 호출
    public bool TryActivate(GameObject activator)
    {
        if (!CanInteract(activator))
        {
            return false;
        }

        Activate(activator);
        return true;
    }

    private void Activate(GameObject activator)
    {
        if (IsActivated)
        {
            return;
        }

        IsActivated = true;
        StateChanged?.Invoke(this);

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

    public void ApplyState(bool ritualActive, bool activated)
    {
        _ritualActive = ritualActive;
        IsActivated = activated;
        StateChanged?.Invoke(this);
    }
}