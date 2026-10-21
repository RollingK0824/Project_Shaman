using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TalismanStroke
{
    [SerializeField] private List<Vector2> _points = new();
    [SerializeField] private bool _eraser;
    [SerializeField] private int _radius;
    [SerializeField] private int _brushStyle;
    // Zero is the legacy format: old drawings retain their original widths.
    public int Radius => _radius == 0 ? (_eraser ? 6 : 1) : Mathf.Clamp(_radius, 1, 16);
    public int BrushStyle => _brushStyle;
    public bool HasValidBrush => _radius >= 0 && _radius <= 16 && _brushStyle >= 0 && _brushStyle <= 2;
    public bool Eraser => _eraser;
    public bool CoversPixel(int dx, int dy, int x, int y)
    {
        if (dx * dx + dy * dy > Radius * Radius) return false;
        if (_eraser || _brushStyle == 0) return true;
        // Paper-space grain is deterministic across redraws, thumbnails and saved instances.
        uint grain = unchecked((uint)(x * 374761393 + y * 668265263));
        grain = (grain ^ (grain >> 13)) * 1274126177u;
        return grain % 100 >= (_brushStyle == 1 ? 12u : 35u);
    }
    public TalismanStroke(bool eraser = false, int radius = 0, int brushStyle = 0) { _eraser = eraser; _radius = Mathf.Clamp(radius, 0, 16); _brushStyle = Mathf.Clamp(brushStyle, 0, 2); }
    public IReadOnlyList<Vector2> Points => _points;
    internal void Add(Vector2 point) => _points.Add(point);
}

// Runtime instance data: owned by inventory, never by the spawned held visual or UI.
[Serializable]
public class TalismanDrawingData
{
    public const int MaxPoints = 8192;
    public const int MaxStrokes = 512;
    [SerializeField] private string _id = Guid.NewGuid().ToString("N");
    [SerializeField] private List<TalismanStroke> _strokes = new();
    private TalismanStroke _active;
    private int _pointCount;
    [SerializeField] private string _customName;
    [SerializeField] private string _effectId;
    public string EffectId => _effectId;
    public const int MaxNameLength = 12;
    [SerializeField] private TalismanType _talismanType;
    [NonSerialized] private List<string> _undo;
    public string Id => _id;
    public string InstanceId => _id;
    public string CustomName => _customName;
    public TalismanType TalismanType => _talismanType;
    public bool HasDrawing => _strokes.Exists(s => !s.Eraser && s.Points.Count > 0);
    public bool IsCustom => !string.IsNullOrEmpty(_customName) || HasDrawing;
    public TalismanDrawingData(TalismanType type = TalismanType.Registry) { _talismanType = type; }
    public TalismanDrawingData CreateDraft()
    {
        var copy = JsonUtility.FromJson<TalismanDrawingData>(JsonUtility.ToJson(this));
        copy.Recount(); return copy;
    }
    public void Commit(TalismanDrawingData draft, string customName)
    {
        if (draft == null || draft.Id != Id) throw new ArgumentException("Draft belongs to another talisman");
        _strokes = draft.CreateDraft()._strokes;
        _customName = (customName ?? string.Empty).Trim();
        if (_customName.Length > MaxNameLength) _customName = _customName.Substring(0, MaxNameLength);
        Recount(); Changed?.Invoke();
    }
    private void Recount() { _active = null; _pointCount = 0; foreach(var stroke in _strokes) _pointCount += stroke.Points.Count; }
    internal void SetEffect(string effectId) => _effectId = effectId;
    public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength &&
        !System.Array.Exists(name.ToCharArray(), char.IsControl);
    public bool IsValidDrawing()
    {
        if (string.IsNullOrEmpty(_id) || _id.Length > 64 || _strokes == null || _strokes.Count > MaxStrokes) return false;
        int count = 0;
        foreach (var stroke in _strokes)
        {
            if (stroke == null || stroke.Points == null || !stroke.HasValidBrush) return false;
            foreach (var point in stroke.Points) if (!Inside(point) || ++count > MaxPoints) return false;
        }
        return HasDrawing && HasVisibleInk();
    }
    // Match the existing surface region/resolution/brush widths. Fully erased ink is empty.
    private bool HasVisibleInk()
    {
        const int width = 256, height = 512;
        var ink = new bool[width * height]; var region = new Rect(.23f, .07f, .53f, .86f);
        foreach (var stroke in _strokes)
            for (int i = 0; i < stroke.Points.Count; i++)
            {
                Vector2 a = region.min + Vector2.Scale(stroke.Points[Mathf.Max(0, i - 1)], region.size);
                Vector2 b = region.min + Vector2.Scale(stroke.Points[i], region.size);
                a = Vector2.Scale(a, new Vector2(width - 1, height - 1)); b = Vector2.Scale(b, new Vector2(width - 1, height - 1));
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b))), radius = stroke.Radius;
                for (int step = 0; step <= steps; step++)
                {
                    Vector2 p = Vector2.Lerp(a, b, step / (float)steps);
                    int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                    for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                        if (stroke.CoversPixel(dx, dy, x + dx, y + dy) && x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height &&
                            region.Contains(new Vector2((x + dx) / (float)(width - 1), (y + dy) / (float)(height - 1))))
                            ink[(y + dy) * width + x + dx] = !stroke.Eraser;
                }
            }
        return Array.Exists(ink, pixel => pixel);
    }
    private void Remember() { _undo ??= new List<string>(); if (_undo.Count >= 64) _undo.RemoveAt(0); _undo.Add(JsonUtility.ToJson(this)); }
    public bool Undo()
    {
        if (_undo == null || _undo.Count == 0) return false;
        var snapshot = JsonUtility.FromJson<TalismanDrawingData>(_undo[_undo.Count - 1]);
        _undo.RemoveAt(_undo.Count - 1);
        _strokes = snapshot._strokes; Recount(); Changed?.Invoke(); return true;
    }
    public int PointCount => _pointCount;
    public IReadOnlyList<TalismanStroke> Strokes => _strokes;
    public event Action Changed;
    public bool BeginStroke(Vector2 point, bool eraser = false, int radius = 0, int brushStyle = 0)
    {
        EndStroke();
        if (!Inside(point) || _strokes.Count >= MaxStrokes || _pointCount >= MaxPoints) return false;
        Remember();
        _active = new TalismanStroke(eraser, radius, brushStyle);
        _strokes.Add(_active);
        return AppendPoint(point);
    }
    public bool AppendPoint(Vector2 point)
    {
        if (!Inside(point)) { EndStroke(); return false; }
        if (_active == null || _pointCount >= MaxPoints) return false;
        if (_active.Points.Count > 0 && (point - _active.Points[_active.Points.Count - 1]).sqrMagnitude < .000001f) return true;
        _active.Add(point);
        _pointCount++;
        Changed?.Invoke();
        return true;
    }
    public void EndStroke() => _active = null;
    public void Clear() { Remember(); _active = null; _strokes.Clear(); _pointCount = 0; Changed?.Invoke(); }
    public static bool Inside(Vector2 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) &&
        p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;
}
