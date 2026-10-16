using UnityEngine;

// Shared entry point for held-item use and inventory selection. Drafts stay local.
[RequireComponent(typeof(PlayerTalismanCollection))]
public class PlayerTalismanDrawingController : MonoBehaviour
{
    [SerializeField] private TalismanDrawingPanel _panelPrefab;
    [SerializeField] private EffectTalismanInventoryView _effectInventoryPrefab;
    public EffectTalismanInventoryView EffectInventoryPrefab => _effectInventoryPrefab;
    private TalismanDrawingPanel _panel;
    public bool IsDrawing => _panel != null && _panel.IsOpen;
    public bool Open(TalismanItemData item, string instanceId = null)
    {
        if (IsDrawing || item == null || !item.AllowDrawing || !PlayerActionGuard.CanAct(gameObject) || !PlayerLocalPresentation.IsLocal(gameObject)) return false;
        var collection = GetComponent<PlayerTalismanCollection>();
        if (instanceId != null && !collection.Select(item, instanceId)) return false;
        if (_panelPrefab == null) return false;
        if (_panel == null) _panel = Instantiate(_panelPrefab);
        return _panel.Open(gameObject, collection, item);
    }
    public bool OpenFromInventory(TalismanItemData item, string instanceId, InventoryUIController inventoryUI)
    {
        if (IsDrawing) return false;
        if (inventoryUI != null && inventoryUI.IsOpen) inventoryUI.SetInventoryOpen(false);
        return Open(item, instanceId);
    }
    public void Close() { if (_panel != null) _panel.Close(); }
    private void OnDisable() => Close();
    private void OnDestroy() { if (_panel != null) Destroy(_panel.gameObject); }
}
