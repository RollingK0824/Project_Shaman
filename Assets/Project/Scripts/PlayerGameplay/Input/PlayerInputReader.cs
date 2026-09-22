using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool SprintHeld { get; private set; }

    public event Action InteractPressed;
    public event Action CancelPressed;
    public event Action NotebookPressed;
    public event Action UseItemStarted;
    public event Action UseItemCanceled;
    public event Action<int> SlotSelected;

    private ShamanInput _input;

    private void Awake()
    {
        _input = new ShamanInput();
    }

    private void OnEnable()
    {
        _input.Player.Enable();

        _input.Player.Interact.performed += OnInteract;
        _input.Player.Cancel.performed += OnCancel;
        _input.Player.Notebook.performed += OnNotebook;

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
        MoveInput = _input.Player.Move.ReadValue<Vector2>();
        LookInput = _input.Player.Look.ReadValue<Vector2>();
        SprintHeld = _input.Player.Sprint.IsPressed();
    }

    private void OnDisable()
    {
        if (_input == null)
        {
            return;
        }

        _input.Player.Interact.performed -= OnInteract;
        _input.Player.Cancel.performed -= OnCancel;
        _input.Player.Notebook.performed -= OnNotebook;

        _input.Player.UseItem.started -= OnUseItemStarted;
        _input.Player.UseItem.canceled -= OnUseItemCanceled;

        _input.Player.Slot1.performed -= OnSlot1;
        _input.Player.Slot2.performed -= OnSlot2;
        _input.Player.Slot3.performed -= OnSlot3;
        _input.Player.Slot4.performed -= OnSlot4;
        _input.Player.Slot5.performed -= OnSlot5;
        _input.Player.Slot6.performed -= OnSlot6;

        _input.Player.Disable();

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        SprintHeld = false;
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        InteractPressed?.Invoke();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        CancelPressed?.Invoke();
    }

    private void OnNotebook(InputAction.CallbackContext context)
    {
        NotebookPressed?.Invoke();
    }

    private void OnUseItemStarted(InputAction.CallbackContext context)
    {
        UseItemStarted?.Invoke();
    }

    private void OnUseItemCanceled(InputAction.CallbackContext context)
    {
        UseItemCanceled?.Invoke();
    }

    private void OnSlot1(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(0);
    }

    private void OnSlot2(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(1);
    }

    private void OnSlot3(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(2);
    }

    private void OnSlot4(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(3);
    }

    private void OnSlot5(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(4);
    }

    private void OnSlot6(InputAction.CallbackContext context)
    {
        SlotSelected?.Invoke(5);
    }
}