using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchHeld => isActiveAndEnabled && !WorldInputBlocked && _input.Player.Crouch.IsPressed();

    public bool GameplayInputBlocked { get; private set; }

    public event Action InteractPressed;
    public event Action CancelPressed;
    public event Action NotebookPressed;
    public event Action JumpPressed;
    public event Action ViewTogglePressed;

    public event Action UseItemStarted;
    public event Action UseItemCanceled;

    public event Action<int> SlotSelected;

    public float SpectatorVerticalInput => !isActiveAndEnabled || GameplayInputBlocked ? 0f :
        (_input.Spectator.Up.IsPressed() ? 1f : 0f) - (_input.Spectator.Down.IsPressed() ? 1f : 0f);
    public event Action PreviousSpectateTargetPressed;
    public event Action NextSpectateTargetPressed;
    public event Action ToggleSpectateModePressed;
    private PlayerHealth _health;
    private bool WorldInputBlocked => GameplayInputBlocked || (_health != null && _health.IsDead);
    // InputManager가 소유하는 공용 액션 (Enable/Disable/Dispose는 InputManager가 담당)
    private ShamanInput _input;


    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
    }


    private void OnEnable()
    {
        _input = InputManager.Instance.Actions;

        _input.Spectator.Previous.performed += OnSpectatePrevious;
        _input.Spectator.Next.performed += OnSpectateNext;
        _input.Spectator.ToggleMode.performed += OnSpectateToggle;

        _input.Player.Interact.performed += OnInteract;
        _input.UI.Cancel.performed += OnCancel;
        _input.UI.Notebook.performed += OnNotebook;
        _input.Player.Jump.performed += OnJump;
        _input.Player.ViewToggle.performed += OnViewToggle;

        _input.Player.UseItem.started += OnUseItemStarted;
        _input.Player.UseItem.canceled += OnUseItemCanceled;

        _input.Player.Slot1.performed += OnSlot1;
        _input.Player.Slot2.performed += OnSlot2;
        _input.Player.Slot3.performed += OnSlot3;
        _input.Player.Slot4.performed += OnSlot4;
        _input.Player.Slot5.performed += OnSlot5;
        _input.Player.Slot6.performed += OnSlot6;
    }


    private void Update()
    {
        if (GameplayInputBlocked)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            SprintHeld = false;

            return;
        }

        MoveInput =
            _input.Player.Move.ReadValue<Vector2>();

        LookInput =
            _input.Player.Look.ReadValue<Vector2>();

        SprintHeld =
            _input.Player.Sprint.IsPressed();
    }


    private void OnDisable()
    {
        // 종료 중 InputManager가 새로 생기지 않도록 Instance 대신 캐시된 _input 사용
        if (_input == null)
        {
            return;
        }

        _input.Player.Interact.performed -= OnInteract;
        _input.UI.Cancel.performed -= OnCancel;
        _input.UI.Notebook.performed -= OnNotebook;
        _input.Player.Jump.performed -= OnJump;
        _input.Player.ViewToggle.performed -= OnViewToggle;

        _input.Player.UseItem.started -= OnUseItemStarted;
        _input.Player.UseItem.canceled -= OnUseItemCanceled;

        _input.Player.Slot1.performed -= OnSlot1;
        _input.Player.Slot2.performed -= OnSlot2;
        _input.Player.Slot3.performed -= OnSlot3;
        _input.Player.Slot4.performed -= OnSlot4;
        _input.Player.Slot5.performed -= OnSlot5;
        _input.Player.Slot6.performed -= OnSlot6;

        _input.Spectator.Previous.performed -= OnSpectatePrevious;
        _input.Spectator.Next.performed -= OnSpectateNext;
        _input.Spectator.ToggleMode.performed -= OnSpectateToggle;

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        SprintHeld = false;

        GameplayInputBlocked = false;
    }


    // =========================================================
    // Input Lock
    // =========================================================

    public void SetGameplayInputBlocked(
        bool blocked)
    {
        if (blocked && !GameplayInputBlocked) UseItemCanceled?.Invoke();
        GameplayInputBlocked =
            blocked;

        if (!blocked)
        {
            return;
        }

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        SprintHeld = false;
    }


    // =========================================================
    // General
    // =========================================================

    private bool CanUseSpectatorInput => !GameplayInputBlocked && _health != null && _health.IsDead;
    private void OnSpectatePrevious(InputAction.CallbackContext context)
    {
        if (CanUseSpectatorInput) PreviousSpectateTargetPressed?.Invoke();
    }
    private void OnSpectateNext(InputAction.CallbackContext context)
    {
        if (CanUseSpectatorInput) NextSpectateTargetPressed?.Invoke();
    }
    private void OnSpectateToggle(InputAction.CallbackContext context)
    {
        if (CanUseSpectatorInput) ToggleSpectateModePressed?.Invoke();
    }

    private void OnInteract(
        InputAction.CallbackContext context)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        InteractPressed?.Invoke();
    }


    private void OnCancel(
        InputAction.CallbackContext context)
    {
        // 메뉴를 닫아야 하므로
        // Block 상태에서도 Cancel은 허용한다.
        CancelPressed?.Invoke();
    }


    private void OnNotebook(
        InputAction.CallbackContext context)
    {
        // Keep read-only Notebook open/close available after death or a UI lock.
        NotebookPressed?.Invoke();
    }


    private void OnJump(
    InputAction.CallbackContext context)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        JumpPressed?.Invoke();
    }

    private void OnViewToggle(
        InputAction.CallbackContext context)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        ViewTogglePressed?.Invoke();
    }

    // =========================================================
    // Item Use
    // =========================================================

    private void OnUseItemStarted(
        InputAction.CallbackContext context)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        UseItemStarted?.Invoke();
    }


    private void OnUseItemCanceled(
        InputAction.CallbackContext context)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        UseItemCanceled?.Invoke();
    }


    // =========================================================
    // Quick Slots
    // =========================================================

    private void OnSlot1(
        InputAction.CallbackContext context)
    {
        SelectSlot(0);
    }


    private void OnSlot2(
        InputAction.CallbackContext context)
    {
        SelectSlot(1);
    }


    private void OnSlot3(
        InputAction.CallbackContext context)
    {
        SelectSlot(2);
    }


    private void OnSlot4(
        InputAction.CallbackContext context)
    {
        SelectSlot(3);
    }


    private void OnSlot5(
        InputAction.CallbackContext context)
    {
        SelectSlot(4);
    }


    private void OnSlot6(
        InputAction.CallbackContext context)
    {
        SelectSlot(5);
    }


    private void SelectSlot(
        int slotIndex)
    {
        if (WorldInputBlocked)
        {
            return;
        }

        SlotSelected?.Invoke(
            slotIndex
        );
    }
}
