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


    private void Start()
    {
        SetInventoryOpen(false);
    }


    private void OnDisable()
    {
        if (IsOpen)
        {
            SetInventoryOpen(false);
        }
    }


    // Legacy presentation API only; notebook/UIManager now own keyboard closing.
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
