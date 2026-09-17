using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private const int QUICK_SLOT_COUNT = 6;

    [Header("Inventory")]
    [SerializeField] private int _inventoryCapacity = 12;

    private readonly List<ItemData> _items =
        new List<ItemData>();

    private ItemData[] _quickSlots;

    public IReadOnlyList<ItemData> Items => _items;

    public event Action<ItemData> ItemAdded;
    public event Action<int, ItemData> QuickSlotChanged;

    private void Awake()
    {
        _quickSlots =
            new ItemData[QUICK_SLOT_COUNT];
    }

    public bool TryAddItem(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (_items.Count >= _inventoryCapacity)
        {
            Debug.Log(
                "[Inventory] 인벤토리가 가득 찼습니다."
            );

            return false;
        }

        _items.Add(itemData);

        ItemAdded?.Invoke(itemData);

        Debug.Log(
            $"[Inventory] 획득: " +
            $"{itemData.DisplayName} " +
            $"({_items.Count}/{_inventoryCapacity})"
        );

        TryAssignFirstEmptyQuickSlot(itemData);

        return true;
    }

    public ItemData GetQuickSlotItem(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= QUICK_SLOT_COUNT)
        {
            return null;
        }

        return _quickSlots[slotIndex];
    }

    public bool AssignQuickSlot(
        int slotIndex,
        ItemData itemData)
    {
        if (slotIndex < 0 ||
            slotIndex >= QUICK_SLOT_COUNT)
        {
            return false;
        }

        if (itemData != null &&
            !_items.Contains(itemData))
        {
            return false;
        }

        _quickSlots[slotIndex] = itemData;

        QuickSlotChanged?.Invoke(
            slotIndex,
            itemData
        );

        return true;
    }

    private void TryAssignFirstEmptyQuickSlot(
        ItemData itemData)
    {
        for (int i = 0;
             i < QUICK_SLOT_COUNT;
             i++)
        {
            if (_quickSlots[i] != null)
            {
                continue;
            }

            _quickSlots[i] = itemData;

            QuickSlotChanged?.Invoke(
                i,
                itemData
            );

            Debug.Log(
                $"[Inventory] " +
                $"{itemData.DisplayName} → " +
                $"슬롯 {i + 1} 자동 등록"
            );

            return;
        }

        Debug.Log(
            $"[Inventory] {itemData.DisplayName} 획득. " +
            "비어 있는 퀵슬롯은 없습니다."
        );
    }
}