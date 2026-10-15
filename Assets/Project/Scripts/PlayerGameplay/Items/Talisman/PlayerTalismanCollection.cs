using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerTalismanCollection : MonoBehaviour
{
    private PlayerInventory _inventory;
    private readonly Dictionary<TalismanItemData, List<TalismanDrawingData>> _instances = new();
    private readonly Dictionary<TalismanItemData, string> _selected = new();
    public event Action SelectionChanged;
    public event Action<TalismanItemData, TalismanDrawingData> DrawingSaved;
    public bool Save(TalismanItemData item, TalismanDrawingData draft, string name)
    {
        if (item == null || !item.AllowDrawing || draft == null || !PlayerActionGuard.CanAct(gameObject)) return false;
        foreach (var instance in GetInstances(item))
            if (instance.Id == draft.Id)
            {
                instance.Commit(draft, name); DrawingSaved?.Invoke(item, instance);
                SelectionChanged?.Invoke(); return true;
            }
        return false;
    }
    private void Awake() { _inventory = GetComponent<PlayerInventory>(); }
    private void OnEnable()
    {
        _inventory.ItemUnitsChanged += HandleUnits;
        // Accommodate an adapter adding this component after its inventory snapshot.
        var types = new HashSet<TalismanItemData>(_instances.Keys);
        foreach (var item in _inventory.Items) if (item is TalismanItemData talisman) types.Add(talisman);
        foreach (var type in types) HandleUnits(type, _inventory.GetItemCount(type) - GetInstances(type).Count);
    }
    private void OnDisable() { _inventory.ItemUnitsChanged -= HandleUnits; }
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
        if (_selected.TryGetValue(item, out var id))
            foreach (var instance in list) if (instance.Id == id) return instance;
        _selected[item] = list[0].Id;
        return list[0];
    }
    public bool Select(TalismanItemData item, string id)
    {
        foreach (var instance in GetInstances(item))
            if (instance.Id == id) { GetSelected(item)?.EndStroke(); _selected[item] = id; SelectionChanged?.Invoke(); return true; }
        return false;
    }
    private void HandleUnits(ItemData item, int delta)
    {
        if (!(item is TalismanItemData talisman) || delta == 0) return;
        GetInstances(talisman);
        var list = _instances[talisman];
        // Consumption removes the selected physical sheet first, then the next sheet.
        for (int i = 0; i < -delta && list.Count > 0; i++)
        {
            var selected = GetSelected(talisman);
            selected?.EndStroke();
            list.Remove(selected);
            _selected.Remove(talisman);
        }
        for (int i = 0; i < delta; i++) list.Add(new TalismanDrawingData(talisman.TalismanType));
        SelectionChanged?.Invoke();
    }
}
