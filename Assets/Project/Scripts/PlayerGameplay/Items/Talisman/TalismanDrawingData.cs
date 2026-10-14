using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TalismanStroke
{
    [SerializeField] private List<Vector2> _points = new();
    [SerializeField] private bool _eraser;
    public bool Eraser => _eraser;
    public TalismanStroke(bool eraser = false) { _eraser = eraser; }
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
        if (_customName.Length > 32) _customName = _customName.Substring(0, 32);
        Recount(); Changed?.Invoke();
    }
    private void Recount() { _active = null; _pointCount = 0; foreach(var stroke in _strokes) _pointCount += stroke.Points.Count; }
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
    public bool BeginStroke(Vector2 point, bool eraser = false)
    {
        EndStroke();
        if (!Inside(point) || _strokes.Count >= MaxStrokes || _pointCount >= MaxPoints) return false;
        Remember();
        _active = new TalismanStroke(eraser);
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
