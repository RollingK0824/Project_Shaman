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

    public void Bind(PlayerInventory inventory)
    {
        if (_inventory != null) _inventory.InventoryChanged -= Refresh;
        _inventory = inventory;
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

        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.InventoryChanged -= Refresh;
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
        }
    }
}
