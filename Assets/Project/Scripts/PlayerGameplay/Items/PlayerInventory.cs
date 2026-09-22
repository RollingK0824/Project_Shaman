using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private const int QUICK_SLOT_COUNT = 6;

    private readonly List<ItemData> _items =
        new List<ItemData>();

    private ItemData[] _quickSlots;

    public IReadOnlyList<ItemData> Items => _items;

    public int QuickSlotCount => QUICK_SLOT_COUNT;

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

        _items.Add(itemData);

        ItemAdded?.Invoke(itemData);

        Debug.Log(
            $"[Inventory] 획득: {itemData.DisplayName}"
        );

        TryAssignFirstEmptyQuickSlot(itemData);

        return true;
    }

    public ItemData GetQuickSlotItem(int slotIndex)
    {
        if (!IsValidQuickSlotIndex(slotIndex))
        {
            return null;
        }

        return _quickSlots[slotIndex];
    }

    public bool AssignQuickSlot(
        int slotIndex,
        ItemData itemData)
    {
        if (!IsValidQuickSlotIndex(slotIndex))
        {
            return false;
        }

        if (itemData != null &&
            !_items.Contains(itemData))
        {
            Debug.LogWarning(
                $"[Inventory] 보유하지 않은 아이템은 " +
                $"슬롯에 등록할 수 없습니다: " +
                $"{itemData.DisplayName}"
            );

            return false;
        }

        _quickSlots[slotIndex] = itemData;

        QuickSlotChanged?.Invoke(
            slotIndex,
            itemData
        );

        return true;
    }

    public bool Contains(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        return _items.Contains(itemData);
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
            "현재 비어 있는 퀵슬롯은 없습니다."
        );
    }

    private bool IsValidQuickSlotIndex(
        int slotIndex)
    {
        return
            slotIndex >= 0 &&
            slotIndex < QUICK_SLOT_COUNT;
    }
}