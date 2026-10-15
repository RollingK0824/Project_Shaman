using System.Collections.Generic;
using UnityEngine;

public class InventoryPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private Transform _itemGrid;
    [SerializeField] private InventoryItemSlotUI _itemSlotPrefab;
    private readonly Dictionary<ItemData, InventoryItemSlotUI> _slots = new();
    private readonly List<ItemData> _removed = new();
    private readonly Dictionary<string, InventoryItemSlotUI> _customSlots = new();
    private PlayerTalismanCollection _talismans;
    [SerializeField] private EffectTalismanInventoryView _effectViewPrefab;
    private EffectTalismanInventoryView _effectView;
    public bool IsEffectViewOpen => _effectView != null && _effectView.gameObject.activeSelf;
    public bool BackToMain()
    {
        if (!IsEffectViewOpen) return false;
        _effectView.gameObject.SetActive(false); _itemGrid.gameObject.SetActive(true); Refresh(); return true;
    }
    public void OpenEffectGroup()
    {
        // Scene-authored panels predating the grouped inventory may not have this new field.
        // Resolve the same UI prefab from the bound Player, without changing scene overrides.
        if (_effectViewPrefab == null && _inventory != null)
            _effectViewPrefab = _inventory.GetComponent<PlayerTalismanDrawingController>()?.EffectInventoryPrefab;
        if (_talismans == null || _talismans.DrawnEffectCount == 0 || _effectViewPrefab == null) return;
        if (_effectView == null) _effectView = Instantiate(_effectViewPrefab, transform);
        _effectView.Bind(_talismans, () => BackToMain()); _itemGrid.gameObject.SetActive(false); _effectView.gameObject.SetActive(true);
    }

    public void Bind(PlayerInventory inventory)
    {
        if (_inventory != null) _inventory.InventoryChanged -= Refresh;
        if (_talismans != null) _talismans.SelectionChanged -= Refresh;
        _inventory = inventory;
        _talismans = inventory != null ? inventory.GetComponent<PlayerTalismanCollection>() : null;
        if (isActiveAndEnabled && _talismans != null) _talismans.SelectionChanged += Refresh;
        if (isActiveAndEnabled && _inventory != null) _inventory.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnEnable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.InventoryChanged += Refresh;
        _talismans = _inventory.GetComponent<PlayerTalismanCollection>();
        if (_talismans != null) _talismans.SelectionChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.InventoryChanged -= Refresh;
        if (_talismans != null) _talismans.SelectionChanged -= Refresh;
        BackToMain();
    }

    public void Refresh()
    {
        if (_itemGrid == null ||
            _itemSlotPrefab == null)
        {
            return;
        }

        _removed.Clear();
        foreach (var row in _customSlots.Values) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        _customSlots.Clear();
        foreach (var entry in _slots)
        {
            if (_inventory != null && _inventory.Contains(entry.Key) && !(entry.Key is TalismanItemData effect && effect.IsEffectTalisman)) continue;
            if (entry.Value != null) { entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject); }
            _removed.Add(entry.Key);
        }
        foreach (var item in _removed) _slots.Remove(item);
        if (_inventory == null) return;

        // PlayerInventory에는 같은 ItemData가 수량만큼 들어 있으므로
        // 같은 종류는 한 번만 슬롯을 생성한다.
        HashSet<ItemData> displayedItems = new HashSet<ItemData>();
        int blankEffects = 0;
        TalismanItemData blankTemplate = null;

        var visibleItems = new List<ItemData>(_inventory.Items);
        if (_inventory.SharedMaterials != null) visibleItems.AddRange(_inventory.SharedMaterials.Counts.Keys);
        foreach (ItemData itemData in visibleItems)
        {
            if (itemData == null ||
                displayedItems.Contains(itemData))
            {
                continue;
            }

            displayedItems.Add(itemData);

            int count = _inventory.GetItemCount(itemData);
            // Legacy/test data references may coexist with the canonical asset after migration.
            // Their blank quantities still occupy one visible group, while ownership keeps its original references.
            if (itemData is TalismanItemData blank && blank.IsEffectTalisman && _talismans != null)
            {
                blankEffects += count; blankTemplate ??= blank; continue;
            }

            if (!_slots.TryGetValue(itemData, out var slot) || slot == null)
            {
                slot = Instantiate(_itemSlotPrefab, _itemGrid);
                _slots[itemData] = slot;
            }

            slot.Setup(itemData, count);
            slot.gameObject.SetActive(true);
            if (itemData is TalismanItemData talisman && _talismans != null)
            {
                int plain = 0; string plainId = null;
                foreach (var sheet in _talismans.GetInstances(talisman))
                {
                    if (!sheet.IsCustom) { plain++; plainId ??= sheet.Id; continue; }
                    var row = Instantiate(_itemSlotPrefab, _itemGrid);
                    row.Setup(itemData, 1);
                    string id = sheet.Id;
                    row.SetInstance(string.IsNullOrEmpty(sheet.CustomName) ? talisman.DisplayName : sheet.CustomName, () => UseTalisman(talisman, id));
                    _customSlots[id] = row;
                }
                slot.Setup(itemData, plain); slot.gameObject.SetActive(plain > 0);
                slot.SetUse(() => UseTalisman(talisman, plainId));
            }
        }
        if (blankTemplate != null && blankEffects > 0)
        {
            var row = Instantiate(_itemSlotPrefab, _itemGrid);
            row.Setup(blankTemplate, blankEffects); row.SetLabel("미사용 효과 부적"); row.SetUse(() => UseTalisman(blankTemplate, null));
            _customSlots["effect-blanks"] = row;
        }
        if (_talismans != null && _talismans.DrawnEffectCount > 0)
        {
            TalismanItemData template = null;
            foreach (var type in _talismans.ItemTypes) if (type.IsEffectTalisman && _talismans.GetInstances(type).Count > 0) { template = type; break; }
            var row = Instantiate(_itemSlotPrefab, _itemGrid);
            row.Setup(template, _talismans.DrawnEffectCount); row.SetLabel("드로잉된 효과 부적"); row.SetUse(OpenEffectGroup);
            _customSlots["effect-group"] = row;
        }
        if (IsEffectViewOpen) _effectView.Refresh();
    }
    private void UseTalisman(TalismanItemData item, string id)
    {
        var drawing = _inventory.GetComponent<PlayerTalismanDrawingController>();
        var ui = _inventory.GetComponentInChildren<InventoryUIController>(true);
        if (ui == null) ui = GetComponentInParent<InventoryUIController>();
        if (ui == null)
            foreach (var candidate in FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
                if (candidate.OwnsPanel(transform)) { ui = candidate; break; }
        drawing?.OpenFromInventory(item, id, ui);
    }
}
