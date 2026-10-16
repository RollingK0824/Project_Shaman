using System;
using UnityEngine;

// Final save payload only. No live strokes, ScriptableObjects or prefab references on the wire.
// The transport adapter authenticates the sender; ReadDrawing validates only the drawing payload.
[Serializable]
public class TalismanSaveData
{
    public string InstanceId;
    public string CustomName;
    public string EffectId;
    public string DrawingJson;
    public static TalismanSaveData From(TalismanDrawingData drawing, string name, string effectId) => new TalismanSaveData
    { InstanceId = drawing.Id, CustomName = name, EffectId = effectId, DrawingJson = JsonUtility.ToJson(drawing) };
    public TalismanDrawingData ReadDrawing()
    {
        if (string.IsNullOrEmpty(DrawingJson) || DrawingJson.Length > 600000 || !TalismanDrawingData.ValidName(CustomName)) return null;
        try
        {
            var drawing = JsonUtility.FromJson<TalismanDrawingData>(DrawingJson);
            return drawing != null && drawing.Id == InstanceId && drawing.IsValidDrawing() ? drawing : null;
        }
        catch (Exception) { return null; }
    }
}
