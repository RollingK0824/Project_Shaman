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

        int emptySlotIndex =
            FindFirstEmptyQuickSlot();

        if (emptySlotIndex < 0)
        {
            Debug.LogWarning(
                $"[Inventory] 슬롯이 가득 차서 " +
                $"{itemData.DisplayName}을 획득할 수 없습니다."
            );

            return false;
        }

        _items.Add(itemData);

        _quickSlots[emptySlotIndex] =
            itemData;

        ItemAdded?.Invoke(itemData);

        QuickSlotChanged?.Invoke(
            emptySlotIndex,
            itemData
        );

        Debug.Log(
            $"[Inventory] {itemData.DisplayName} 획득 → " +
            $"슬롯 {emptySlotIndex + 1} 등록"
        );

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

        _quickSlots[slotIndex] =
            itemData;

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

    private int FindFirstEmptyQuickSlot()
    {
        for (int i = 0;
             i < QUICK_SLOT_COUNT;
             i++)
        {
            if (_quickSlots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsValidQuickSlotIndex(
        int slotIndex)
    {
        return
            slotIndex >= 0 &&
            slotIndex < QUICK_SLOT_COUNT;
    }

    internal bool HasEmptyQuickSlot()
    {
        throw new NotImplementedException();
    }
}