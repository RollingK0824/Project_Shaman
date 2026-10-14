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
        if (!talisman.AllowDrawing) { UseRequested?.Invoke(this); Debug.Log("[Talisman] 결계 부적 사용 요청 (판정 연동 대기)", this); return; }
        var drawing = Owner.GetComponent<PlayerTalismanDrawingController>();
        if (drawing != null) { drawing.Open(talisman); return; }
        var collection = Owner.GetComponent<PlayerTalismanCollection>();
        if (collection == null || collection.GetSelected(talisman) == null || _drawingPanelPrefab == null) return;
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
