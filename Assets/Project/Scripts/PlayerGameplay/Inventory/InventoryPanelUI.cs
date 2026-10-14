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
    }

    private void HandleItemCountChanged(ItemData itemData, int count)
    {
        Refresh();
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
            if (_inventory != null && _inventory.Contains(entry.Key)) continue;
            if (entry.Value != null) { entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject); }
            _removed.Add(entry.Key);
        }
        foreach (var item in _removed) _slots.Remove(item);
        if (_inventory == null) return;

        // PlayerInventory에는 같은 ItemData가 수량만큼 들어 있으므로
        // 같은 종류는 한 번만 슬롯을 생성한다.
        HashSet<ItemData> displayedItems = new HashSet<ItemData>();

        foreach (ItemData itemData in _inventory.Items)
        {
            if (itemData == null ||
                displayedItems.Contains(itemData))
            {
                continue;
            }

            displayedItems.Add(itemData);

            int count = _inventory.GetItemCount(itemData);

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
