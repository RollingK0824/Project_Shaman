using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerInventory), typeof(PlayerTalismanEffectController))]
public class PlayerTalismanCollection : MonoBehaviour
{
    private PlayerInventory _inventory;
    private PlayerTalismanEffectController _effects;
    private readonly Dictionary<TalismanItemData, List<TalismanDrawingData>> _instances = new();
    private readonly Dictionary<TalismanItemData, string> _selected = new();
    private readonly HashSet<string> _savedIds = new();
    private bool _busy;
    private long _snapshotVersion = -1;
    private Func<bool> _authorityCheck;
    public event Action SelectionChanged;
    public event Action<TalismanItemData, TalismanDrawingData> DrawingSaved;
    public event Action<TalismanItemData, TalismanSaveData> SaveRequested;
    public event Action<string> UseRequested;
    // Presentation notification after effect + consumption commit, never on a request.
    public event Action<string> UseCommitted;
    public IEnumerable<TalismanItemData> ItemTypes => _instances.Keys;
    public int DrawnEffectCount
    {
        get { int count = 0; foreach (var pair in _instances) if (pair.Key.IsEffectTalisman) count += pair.Value.Count; return count; }
    }
    public void SetAuthorityCheck(Func<bool> check) => _authorityCheck = check;
    public bool CanExecute => isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) &&
        (!NetworkClient.active && !NetworkServer.active ? _authorityCheck == null || _authorityCheck() :
        NetworkServer.active && _authorityCheck != null && _authorityCheck());
    public PlayerVillagerTracking Tracking => GetComponent<PlayerVillagerTracking>() ?? gameObject.AddComponent<PlayerVillagerTracking>();
    private void Awake() { _inventory = GetComponent<PlayerInventory>(); _effects = GetComponent<PlayerTalismanEffectController>(); }
    private void OnEnable()
    {
        _inventory.ItemUnitsChanged += HandleUnits;
        var types = new HashSet<TalismanItemData>(_instances.Keys);
        foreach (var item in _inventory.Items) if (item is TalismanItemData talisman) types.Add(talisman);
        foreach (var type in types) if (!type.IsEffectTalisman) HandleUnits(type, _inventory.GetItemCount(type) - GetInstances(type).Count);
    }
    private void OnDisable() => _inventory.ItemUnitsChanged -= HandleUnits;
    public TalismanDrawingData CreateDraft(TalismanItemData item) => item != null && item.AllowDrawing && _inventory.Contains(item)
        ? new TalismanDrawingData(TalismanType.Effect) : null;
    public bool Save(TalismanItemData item, TalismanDrawingData draft, string name) => Save(item, draft, name, draft?.EffectId);
    public bool Save(TalismanItemData item, TalismanDrawingData draft, string name, string effectId)
    {
        if (_busy || !CanExecute || item == null || !item.AllowDrawing || draft == null ||
            !TalismanDrawingData.ValidName(name) || !draft.IsValidDrawing() || item.FindEffect(effectId) == null || _savedIds.Contains(draft.Id)) return false;
        var saved = draft.CreateDraft(); saved.Commit(draft, name); saved.SetEffect(effectId);
        GetInstances(item);
        _busy = true;
        try
        {
            // Atomic blank -1 + unique instance +1, before inventory observers run.
            return _inventory.TryExchangePersonalItems(new Dictionary<ItemData, int> { [item] = 1 }, null, 0,
                () => { _instances[item].Add(saved); _selected[item] = saved.Id; _savedIds.Add(saved.Id); },
                () =>
                {
                    try { DrawingSaved?.Invoke(item, saved); } catch (Exception e) { Debug.LogException(e, this); }
                    try { SelectionChanged?.Invoke(); } catch (Exception e) { Debug.LogException(e, this); }
                });
        }
        finally { _busy = false; }
    }
    public bool RequestSave(TalismanItemData item, TalismanDrawingData draft, string name, string effectId)
    {
        if (CanExecute) return Save(item, draft, name, effectId);
        if (!_busy && isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) && draft != null && item != null &&
            _inventory.Contains(item) && TalismanDrawingData.ValidName(name) && draft.IsValidDrawing() && item.FindEffect(effectId) != null)
            SaveRequested?.Invoke(item, TalismanSaveData.From(draft, name, effectId));
        return false; // Pending request is not success. Adapter closes UI after confirmation.
    }
    public bool SaveFinal(TalismanItemData item, TalismanSaveData payload) => payload != null &&
        Save(item, payload.ReadDrawing(), payload.CustomName, payload.EffectId);
    public bool TryUse(string id)
    {
        if (_busy || !CanExecute || (_inventory.GetComponent<PlayerItemController>() is var items && items != null && !items.CanUseItems)) return false;
        var drawing = Find(id, out var item);
        var definition = item != null ? item.FindEffect(drawing?.EffectId) : null;
        if (drawing == null || definition == null || !_effects.CanApply(definition)) return false;
        _busy = true;
        try
        {
            if (!_effects.TryApply(definition)) return false;
            if (definition.ConsumeOnUse) { _instances[item].Remove(drawing); _selected.Remove(item); }
            else _selected[item] = id;
            try { PlayerItemActions.ReportUse(gameObject, item); } catch (Exception e) { Debug.LogException(e, this); }
            try { _effects.PublishChanged(); SelectionChanged?.Invoke(); UseCommitted?.Invoke(id); }
            catch (Exception e) { Debug.LogException(e, this); }
            return true;
        }
        finally { _busy = false; }
    }
    public bool RequestUse(string id)
    {
        var record = Find(id, out var template);
        if (template != null && template.FindEffect(record?.EffectId)?.EffectType == TalismanEffectType.VillagerTracking)
            return Tracking.RequestAttach(id);
        if (CanExecute) return TryUse(id);
        if (isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) && Find(id, out _) != null) UseRequested?.Invoke(id);
        return false;
    }
    // Trusted server adapter entry. Validation precedes the same unique-instance consumption commit.
    public bool TryUseTracking(string id, ProjectShaman.AI.AI_Core target, Vector3 origin, Vector3 direction)
    {
        if (_busy || !CanExecute) return false;
        var drawing = Find(id, out var item);
        var definition = item != null ? item.FindEffect(drawing?.EffectId) : null;
        if (drawing == null || definition == null || definition.EffectType != TalismanEffectType.VillagerTracking ||
            !Tracking.CanAttach(id, target, origin, direction)) return false;
        _busy = true;
        try
        {
            Tracking.Commit(target);
            if (definition.ConsumeOnUse) { _instances[item].Remove(drawing); _selected.Remove(item); }
            try { PlayerItemActions.ReportUse(gameObject, item); } catch (Exception e) { Debug.LogException(e, this); }
            try { SelectionChanged?.Invoke(); UseCommitted?.Invoke(id); } catch (Exception e) { Debug.LogException(e, this); }
            Tracking.Publish();
            return true;
        }
        finally { _busy = false; }
    }
    public TalismanDrawingData Find(string id, out TalismanItemData item)
    {
        foreach (var pair in _instances) foreach (var drawing in pair.Value)
            if (drawing.Id == id) { item = pair.Key; return drawing; }
        item = null; return null;
    }
    public IReadOnlyList<TalismanDrawingData> GetInstances(TalismanItemData item)
    {
        if (item == null) return Array.Empty<TalismanDrawingData>();
        if (!_instances.TryGetValue(item, out var list)) { list = new List<TalismanDrawingData>(); _instances.Add(item, list); }
        return list.AsReadOnly();
    }
    public TalismanDrawingData GetSelected(TalismanItemData item)
    {
        var list = GetInstances(item);
        if (list.Count == 0) return null;
        if (_selected.TryGetValue(item, out var id)) foreach (var instance in list) if (instance.Id == id) return instance;
        _selected[item] = list[0].Id; return list[0];
    }
    public bool Select(TalismanItemData item, string id)
    {
        foreach (var instance in GetInstances(item))
            if (instance.Id == id) { _selected[item] = id; SelectionChanged?.Invoke(); return true; }
        return false;
    }
    public TalismanSaveData[] Export(TalismanItemData item)
    {
        var result = new List<TalismanSaveData>();
        foreach (var drawing in GetInstances(item)) result.Add(TalismanSaveData.From(drawing, drawing.CustomName, drawing.EffectId));
        return result.ToArray();
    }
    // Trusted adapter only: confirmed monotonic snapshot on client replica.
    public bool ApplyReplicatedSnapshot(long version, TalismanItemData item, IReadOnlyList<TalismanSaveData> records)
    {
        if (_busy || !NetworkClient.active || NetworkServer.active || version <= _snapshotVersion || item == null || !item.IsEffectTalisman || records == null || records.Count > 512) return false;
        var next = new List<TalismanDrawingData>(); var ids = new HashSet<string>();
        foreach (var record in records)
        {
            var drawing = record?.ReadDrawing();
            if (drawing == null || item.FindEffect(record.EffectId) == null || !ids.Add(drawing.Id)) return false;
            drawing.Commit(drawing, record.CustomName); drawing.SetEffect(record.EffectId); next.Add(drawing);
        }
        _instances[item] = next; _snapshotVersion = version; SelectionChanged?.Invoke(); return true;
    }
    public void ResetRound()
    {
        _instances.Clear(); _selected.Clear(); _savedIds.Clear(); _snapshotVersion = -1; _effects.ResetEffects(); GetComponent<PlayerVillagerTracking>()?.ResetRoundTracking(); SelectionChanged?.Invoke();
    }
    private void HandleUnits(ItemData item, int delta)
    {
        // Effect blanks live only in PlayerInventory. Barrier keeps physical-sheet accounting.
        if (!(item is TalismanItemData talisman) || talisman.IsEffectTalisman || delta == 0) return;
        GetInstances(talisman); var list = _instances[talisman];
        for (int i = 0; i < -delta && list.Count > 0; i++) { list.Remove(GetSelected(talisman)); _selected.Remove(talisman); }
        for (int i = 0; i < delta; i++) list.Add(new TalismanDrawingData(talisman.TalismanType));
        SelectionChanged?.Invoke();
    }
}
