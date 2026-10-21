using System;
using UnityEngine;

public class TalismanItem : ItemBase
{
    [SerializeField] private TalismanDrawingPanel _drawingPanelPrefab;
    private TalismanDrawingPanel _panel;
    public bool IsDrawing => (_panel != null && _panel.IsOpen) || (Owner != null && Owner.GetComponent<PlayerTalismanDrawingController>() is var drawing && drawing != null && drawing.IsDrawing);
    public event Action<TalismanItem> UseRequested;
    public override void OnEquipped()
    {
        foreach (var surface in GetComponentsInChildren<TalismanDrawingSurface>(true)) surface.BindItemVisual(Data, Owner);
    }
    public override void OnUseStarted()
    {
        if (!PlayerActionGuard.CanAct(Owner) || IsDrawing) return;
        if (!(Data is TalismanItemData talisman)) return;
        if (talisman.TalismanType == TalismanType.Barrier)
        {
            var camera = Owner.GetComponentInChildren<Camera>();
            if (!(Data.UseAction is BarrierTalismanUseAction action) || camera == null ||
                !BarrierTalismanUseAction.IsEquipped(Owner, Data) ||
                !action.TryGetPlacement(new ItemUseContext(Owner, Data, camera.transform.position, camera.transform.forward), out _, out _))
            { Owner.GetComponent<PlayerItemFeedback>()?.Unavailable("가까운 벽이나 바닥을 바라보세요"); return; }
            if (!TryBuildUseRequest(out var request)) return;
            // Compatibility notification only. The ItemUseRequested judge owns commit/ReportUse.
            UseRequested?.Invoke(this);
            PlayerEvents.RaiseItemUseRequested(request);
            return;
        }
        if (!talisman.AllowDrawing) return;
        var items = Owner.GetComponent<PlayerItemController>();
        string id = items != null ? items.EquippedTalismanInstanceId : null;
        if (!string.IsNullOrEmpty(id))
        {
            var owned = Owner.GetComponent<PlayerTalismanCollection>();
            var inventory = Owner.GetComponent<PlayerInventory>();
            if (owned != null && inventory != null && items.CanUseItems && items.EquippedItemData == Data &&
                inventory.GetQuickSlotInstanceId(items.SelectedSlotIndex) == id &&
                owned.Find(id, out var template) != null && template == talisman)
                owned.RequestUse(id);
            return;
        }
        var drawing = Owner.GetComponent<PlayerTalismanDrawingController>();
        if (drawing != null) { drawing.Open(talisman); return; }
        var collection = Owner.GetComponent<PlayerTalismanCollection>();
        if (collection == null || collection.CreateDraft(talisman) == null || _drawingPanelPrefab == null) return;
        if (_panel == null) _panel = Instantiate(_drawingPanelPrefab);
        _panel.Open(Owner, collection, talisman);
    }
    public override void OnUnequipped() => CloseDrawing();
    private void OnDisable() => CloseDrawing();
    private void CloseDrawing()
    {
        if (Owner != null) Owner.GetComponent<PlayerTalismanDrawingController>()?.Close();
        if (_panel == null) return;
        _panel.Close(); Destroy(_panel.gameObject); _panel = null;
    }
}
