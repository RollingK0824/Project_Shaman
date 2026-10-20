using UnityEngine;

public class InventoryUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerInputReader _inputReader;

    [SerializeField]
    private GameObject _inventoryPanel;

    public bool IsOpen { get; private set; }
    public bool OwnsPanel(Transform panel) => _inventoryPanel != null &&
        (panel == _inventoryPanel.transform || panel.IsChildOf(_inventoryPanel.transform));


    private void OnEnable()
    {
        if (_inputReader == null)
        {
            return;
        }

        // Inventory is a notebook page now; there is no separate I key.
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

    private void HandleCancel()
    {
        if (!IsOpen)
        {
            return;
        }

        var panel = _inventoryPanel != null ? _inventoryPanel.GetComponentInChildren<InventoryPanelUI>(true) : null;
        if (panel != null && panel.BackToMain()) return;
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
