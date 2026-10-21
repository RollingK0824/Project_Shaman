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
    private InputAction _cycleSlot;
    private float _nextScrollTime;
    private PlayerInventory _inventory;
    private PlayerItemController _items;
    private UIManager _cancelUIManager;
    private bool _menuWasOpen;
    private int _menuClosedFrame = -1;
    private int _cancelHandledFrame = -1;

    // UIManager may close its last panel before our Cancel callback runs.
    public bool IsCancelReservedForUI
    {
        get
        {
            TrackCancelUIManager();
            return (_cancelUIManager != null && _cancelUIManager.IsMenuOpen) ||
                _menuClosedFrame == Time.frameCount;
        }
    }
    public bool CancelHandledThisFrame => _cancelHandledFrame == Time.frameCount;
    public void MarkCancelHandled() => _cancelHandledFrame = Time.frameCount;

    private void TrackCancelUIManager()
    {
        var manager = UIManager.Existing;
        if (manager != null && !manager.isActiveAndEnabled) manager = null;
        if (_cancelUIManager == manager) return;
        if (_cancelUIManager != null) _cancelUIManager.MenuOpenChanged -= OnMenuOpenChanged;
        _cancelUIManager = manager;
        _menuWasOpen = manager != null && manager.IsMenuOpen;
        if (manager != null) manager.MenuOpenChanged += OnMenuOpenChanged;
    }

    private void OnMenuOpenChanged(bool open)
    {
        if (_menuWasOpen && !open) _menuClosedFrame = Time.frameCount;
        _menuWasOpen = open;
    }

    private void OnDestroy()
    {
        if (_cancelUIManager != null) _cancelUIManager.MenuOpenChanged -= OnMenuOpenChanged;
    }


    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _inventory = GetComponent<PlayerInventory>();
        _items = GetComponent<PlayerItemController>();
    }


    private void OnEnable()
    {
        TrackCancelUIManager();
        _input = InputManager.Instance.Actions;
        _cycleSlot = _input.asset.FindAction("Player/CycleSlot", false);

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
        TrackCancelUIManager();
        if (GameplayInputBlocked)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            SprintHeld = false;

            return;
        }

        if (_cycleSlot != null && _cycleSlot.enabled)
            CycleQuickSlot(_cycleSlot.ReadValue<float>());

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

    private bool CanUseSpectatorInput => isActiveAndEnabled && PlayerLocalPresentation.IsLocal(gameObject) && !GameplayInputBlocked && _health != null && _health.IsDead;
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
        if (!PlayerLocalPresentation.IsLocal(gameObject)) return;
        if (IsCancelReservedForUI)
        {
            MarkCancelHandled();
            return;
        }
        // Non-UIPanel modes still need Cancel while gameplay input is blocked.
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


    // Reuse SlotSelected so item equip and HUD selection follow the number-key path.
    public void CycleQuickSlot(float scroll)
    {
        if (!isActiveAndEnabled || GameplayInputBlocked || !PlayerLocalPresentation.IsLocal(gameObject) ||
            float.IsNaN(scroll) || float.IsInfinity(scroll) || Mathf.Abs(scroll) < .01f || Time.unscaledTime < _nextScrollTime) return;
        _nextScrollTime = Time.unscaledTime + .12f;
        if (CanUseSpectatorInput)
        {
            if (scroll > 0) PreviousSpectateTargetPressed?.Invoke();
            else NextSpectateTargetPressed?.Invoke();
            return;
        }
        if (WorldInputBlocked || _inventory == null || _items == null || !_items.CanUseItems) return;
        int count = _inventory.QuickSlotCount;
        if (count == 0) return;
        int current = _items.SelectedSlotIndex;
        int next = current < 0 ? (scroll > 0 ? count - 1 : 0) : (current + (scroll > 0 ? -1 : 1) + count) % count;
        SelectSlot(next);
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
