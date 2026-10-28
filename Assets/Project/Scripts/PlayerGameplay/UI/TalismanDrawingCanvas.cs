using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class TalismanDrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private TalismanDrawingData _data;
    private bool _drawing;
    private bool _segment;
    private PlayerAudioController _audio;
    private Vector2 _lastSoundPoint;
    public void BindAudio(PlayerAudioController audio) { _audio?.StopDrawing(); _audio = audio; }
    private void StrokeSound(Vector2 point)
    {
        if (!Eraser) _audio?.PlayDrawing();
        _lastSoundPoint = point;
    }
    public bool Eraser { get; set; }
    public int PenRadius { get; set; } = 1;
    public int EraserRadius { get; set; } = 6;
    public int BrushStyle { get; set; }
    private bool BeginStroke(Vector2 uv) => _data.BeginStroke(uv, Eraser, Eraser ? EraserRadius : PenRadius, BrushStyle);
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
        _segment = TryGetPoint(e.position, e.pressEventCamera, out var uv) && BeginStroke(uv);
        if (_segment) StrokeSound(uv);
    }
    public void OnDrag(PointerEventData e)
    {
        if (!_drawing || _data == null) return;
        if (!TryGetPoint(e.position, e.pressEventCamera, out var uv)) { _data.EndStroke(); _segment = false; _audio?.StopDrawing(); return; }
        if (!_segment) _segment = BeginStroke(uv);
        else _data.AppendPoint(uv);
        if (_segment && (uv - _lastSoundPoint).sqrMagnitude > .000001f) StrokeSound(uv);
    }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) EndDrawing(); }
    private void OnDisable() => EndDrawing();
    public void EndDrawing() { _data?.EndStroke(); _drawing = false; _segment = false; _audio?.StopDrawing(); }
}
