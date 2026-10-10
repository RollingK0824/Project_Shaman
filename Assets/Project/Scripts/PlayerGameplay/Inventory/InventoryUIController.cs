using UnityEngine;

public class InventoryUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerInputReader _inputReader;

    [SerializeField]
    private GameObject _inventoryPanel;

    public bool IsOpen { get; private set; }


    private void OnEnable()
    {
        if (_inputReader == null)
        {
            return;
        }

        _inputReader.InventoryPressed +=
            ToggleInventory;

        _inputReader.CancelPressed +=
            HandleCancel;
    }


    private void Start()
    {
        SetInventoryOpen(false);
    }


    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.InventoryPressed -=
                ToggleInventory;

            _inputReader.CancelPressed -=
                HandleCancel;
        }

        if (IsOpen)
        {
            SetInventoryOpen(false);
        }
    }


    // =========================================================
    // Input
    // =========================================================

    private void ToggleInventory()
    {
        SetInventoryOpen(
            !IsOpen
        );
    }


    private void HandleCancel()
    {
        if (!IsOpen)
        {
            return;
        }

        SetInventoryOpen(false);
    }


    // =========================================================
    // Open / Close
    // =========================================================

    public void SetInventoryOpen(
        bool open)
    {
        IsOpen =
            open;

        if (_inventoryPanel != null)
        {
            _inventoryPanel.SetActive(
                open
            );
        }

        if (_inputReader != null)
        {
            _inputReader.SetGameplayInputBlocked(
                open
            );
        }

        UpdateCursorState(open);
    }


    private void UpdateCursorState(
        bool inventoryOpen)
    {
        Cursor.visible =
            inventoryOpen;

        Cursor.lockState =
            inventoryOpen
                ? CursorLockMode.None
                : CursorLockMode.Locked;
    }
}