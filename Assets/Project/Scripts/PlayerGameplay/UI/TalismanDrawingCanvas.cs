using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class TalismanDrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private TalismanDrawingData _data;
    private bool _drawing;
    private bool _segment;
    public bool Eraser { get; set; }
    public void Bind(TalismanDrawingData data) { EndDrawing(); _data = data; }
    public bool TryGetPoint(Vector2 screenPoint, Camera camera, out Vector2 uv)
    {
        var rect = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, camera, out var p);
        uv = new Vector2((p.x - rect.rect.xMin) / rect.rect.width, (p.y - rect.rect.yMin) / rect.rect.height);
        return TalismanDrawingData.Inside(uv);
    }
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || _data == null) return;
        _drawing = true;
        _segment = TryGetPoint(e.position, e.pressEventCamera, out var uv) && _data.BeginStroke(uv, Eraser);
    }
    public void OnDrag(PointerEventData e)
    {
        if (!_drawing || _data == null) return;
        if (!TryGetPoint(e.position, e.pressEventCamera, out var uv)) { _data.EndStroke(); _segment = false; return; }
        if (!_segment) _segment = _data.BeginStroke(uv, Eraser);
        else _data.AppendPoint(uv);
    }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) EndDrawing(); }
    private void OnDisable() => EndDrawing();
    public void EndDrawing() { _data?.EndStroke(); _drawing = false; _segment = false; }
}
