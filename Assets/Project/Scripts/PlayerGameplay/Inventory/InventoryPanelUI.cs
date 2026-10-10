using System.Collections.Generic;
using UnityEngine;

public class InventoryPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private Transform _itemGrid;
    [SerializeField] private InventoryItemSlotUI _itemSlotPrefab;

    private void OnEnable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.ItemCountChanged += HandleItemCountChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.ItemCountChanged -= HandleItemCountChanged;
    }

    private void HandleItemCountChanged(ItemData itemData, int count)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_inventory == null ||
            _itemGrid == null ||
            _itemSlotPrefab == null)
        {
            return;
        }

        // 기존 슬롯 제거
        for (int i = _itemGrid.childCount - 1; i >= 0; i--)
        {
            Destroy(_itemGrid.GetChild(i).gameObject);
        }

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

            InventoryItemSlotUI slot =
                Instantiate(_itemSlotPrefab, _itemGrid);

            slot.Setup(itemData, count);
        }
    }
}