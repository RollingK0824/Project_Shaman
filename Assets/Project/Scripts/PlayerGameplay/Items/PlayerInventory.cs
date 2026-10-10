using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private const int QUICK_SLOT_COUNT = 6;

    // 같은 ItemData가 여러 번 들어 있으면
    // 해당 아이템을 여러 개 보유한 것으로 취급한다.
    private readonly List<ItemData> _items =
        new List<ItemData>();

    private ItemData[] _quickSlots;

    public IReadOnlyList<ItemData> Items => _items;

    public int QuickSlotCount => QUICK_SLOT_COUNT;

    public event Action<ItemData> ItemAdded;
    public event Action<ItemData> ItemRemoved;

    public event Action<ItemData, int> ItemCountChanged;

    public event Action<int, ItemData> QuickSlotChanged;
    public event Action InventoryChanged;
    private bool _publishingTransaction;


    private void Awake()
    {
        _quickSlots =
            new ItemData[QUICK_SLOT_COUNT];
    }


    // =========================================================
    // Add
    // =========================================================

    public bool TryAddItem(ItemData itemData)
    {
        if (_publishingTransaction || !PlayerActionGuard.CanAct(gameObject)) return false;
        if (itemData == null)
        {
            return false;
        }

        _items.Add(itemData);

        ItemAdded?.Invoke(itemData);

        int currentCount =
            GetItemCount(itemData);

        ItemCountChanged?.Invoke(
            itemData,
            currentCount
        );

        InventoryChanged?.Invoke();
        if (!itemData.CanUseQuickSlot) return true;

        // 이미 퀵슬롯에 등록되어 있다면
        // 같은 종류를 새 슬롯에 또 등록하지 않는다.
        int existingSlot =
            FindQuickSlot(itemData);

        if (existingSlot >= 0)
        {
            Debug.Log(
                $"[Inventory] {itemData.DisplayName} 획득 " +
                $"(보유 {currentCount}개)"
            );

            return true;
        }

        // 처음 획득한 종류라면 빈 퀵슬롯에 자동 등록
        int emptySlotIndex =
            FindFirstEmptyQuickSlot();

        if (emptySlotIndex >= 0)
        {
            _quickSlots[emptySlotIndex] =
                itemData;

            QuickSlotChanged?.Invoke(
                emptySlotIndex,
                itemData
            );

            Debug.Log(
                $"[Inventory] {itemData.DisplayName} 획득 → " +
                $"슬롯 {emptySlotIndex + 1} 등록"
            );
        }
        else
        {
            // 퀵슬롯이 가득 차도
            // 아이템 획득 자체는 성공한다.
            Debug.Log(
                $"[Inventory] {itemData.DisplayName} 획득 " +
                $"(퀵슬롯 빈자리 없음)"
            );
        }

        return true;
    }


    // =========================================================
    // Remove / Consume
    // =========================================================

    public bool RemoveItem(
        ItemData itemData,
        int amount = 1)
    {
        if (_publishingTransaction || itemData == null ||
            amount <= 0)
        {
            return false;
        }

        int currentCount =
            GetItemCount(itemData);

        if (currentCount < amount)
        {
            return false;
        }

        for (int i = 0; i < amount; i++)
        {
            _items.Remove(itemData);
        }

        int remainingCount =
            GetItemCount(itemData);

        ItemRemoved?.Invoke(itemData);

        ItemCountChanged?.Invoke(
            itemData,
            remainingCount
        );

        // 해당 아이템을 전부 사용했다면
        // 연결된 퀵슬롯도 비운다.
        if (remainingCount <= 0)
        {
            ClearQuickSlotsContaining(
                itemData
            );
        }

        InventoryChanged?.Invoke();
        Debug.Log(
            $"[Inventory] {itemData.DisplayName} 제거 " +
            $"(-{amount}, 남은 수량 {remainingCount})"
        );

        return true;
    }


    public bool ConsumeItem(
        ItemData itemData,
        int amount = 1)
    {
        return RemoveItem(
            itemData,
            amount
        );
    }


    // =========================================================
    // Query
    // =========================================================

    public bool Contains(ItemData itemData)
    {
        return GetItemCount(itemData) > 0;
    }


    public bool HasItem(
        ItemData itemData,
        int amount = 1)
    {
        if (itemData == null ||
            amount <= 0)
        {
            return false;
        }

        return
            GetItemCount(itemData) >= amount;
    }


    public int GetItemCount(
        ItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < _items.Count;
             i++)
        {
            if (_items[i] == itemData)
            {
                count++;
            }
        }

        return count;
    }


    // =========================================================
    // Quick Slot
    // =========================================================

    public ItemData GetQuickSlotItem(
        int slotIndex)
    {
        if (!IsValidQuickSlotIndex(
                slotIndex))
        {
            return null;
        }

        return _quickSlots[slotIndex];
    }


    public bool AssignQuickSlot(
        int slotIndex,
        ItemData itemData)
    {
        if (_publishingTransaction || !PlayerActionGuard.CanAct(gameObject)) return false;
        if (itemData != null && !itemData.CanUseQuickSlot) return false;
        if (!IsValidQuickSlotIndex(
                slotIndex))
        {
            return false;
        }

        // null 지정은 슬롯 비우기로 허용
        if (itemData != null &&
            !Contains(itemData))
        {
            Debug.LogWarning(
                $"[Inventory] 보유하지 않은 아이템은 " +
                $"슬롯에 등록할 수 없습니다: " +
                $"{itemData.DisplayName}"
            );

            return false;
        }

        // 같은 아이템이 다른 퀵슬롯에 있다면
        // 기존 위치에서는 제거한다.
        if (itemData != null)
        {
            for (int i = 0;
                 i < QUICK_SLOT_COUNT;
                 i++)
            {
                if (i == slotIndex)
                {
                    continue;
                }

                if (_quickSlots[i] ==
                    itemData)
                {
                    _quickSlots[i] = null;

                    QuickSlotChanged?.Invoke(
                        i,
                        null
                    );
                }
            }
        }

        _quickSlots[slotIndex] =
            itemData;

        QuickSlotChanged?.Invoke(
            slotIndex,
            itemData
        );

        return true;
    }


    public bool ClearQuickSlot(
        int slotIndex)
    {
        if (_publishingTransaction || !PlayerActionGuard.CanAct(gameObject)) return false;
        if (!IsValidQuickSlotIndex(
                slotIndex))
        {
            return false;
        }

        if (_quickSlots[slotIndex] == null)
        {
            return true;
        }

        _quickSlots[slotIndex] = null;

        QuickSlotChanged?.Invoke(
            slotIndex,
            null
        );

        return true;
    }


    public bool HasEmptyQuickSlot()
    {
        return
            FindFirstEmptyQuickSlot() >= 0;
    }


    // =========================================================
    // Internal
    // =========================================================

    public bool TryAddItem(ItemData itemData, int amount)
    {
        return TryExchangeItems(new Dictionary<ItemData, int>(), itemData, amount);
    }

    public bool CanExchangeItems(IReadOnlyDictionary<ItemData, int> costs, ItemData result, int resultAmount)
    {
        if (!PlayerActionGuard.CanAct(gameObject) || costs == null || result == null || resultAmount <= 0) return false;
        long finalCount = (long)_items.Count + resultAmount;
        foreach (var cost in costs)
        {
            if (cost.Key == null || cost.Value <= 0 || !HasItem(cost.Key, cost.Value)) return false;
            finalCount -= cost.Value;
        }
        return finalCount >= 0 && finalCount <= int.MaxValue;
    }

    // Commit all counts and slots before notifying subscribers. Reentrant writes are rejected.
    public bool TryExchangeItems(IReadOnlyDictionary<ItemData, int> costs, ItemData result, int resultAmount)
    {
        if (_publishingTransaction || !CanExchangeItems(costs, result, resultAmount)) return false;
        var nextItems = new List<ItemData>(_items);
        var changed = new HashSet<ItemData>();
        foreach (var cost in costs)
        {
            for (int i = 0; i < cost.Value; i++) nextItems.Remove(cost.Key);
            changed.Add(cost.Key);
        }
        for (int i = 0; i < resultAmount; i++) nextItems.Add(result);
        changed.Add(result);
        var previousSlots = (ItemData[])_quickSlots.Clone();
        _items.Clear();
        _items.AddRange(nextItems);
        for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            if (_quickSlots[i] != null && (!_quickSlots[i].CanUseQuickSlot || !Contains(_quickSlots[i]))) _quickSlots[i] = null;
        if (result.CanUseQuickSlot && FindQuickSlot(result) < 0)
        {
            int slot = FindFirstEmptyQuickSlot();
            if (slot >= 0) _quickSlots[slot] = result;
        }
        _publishingTransaction = true;
        try
        {
            foreach (var cost in costs) ItemRemoved?.Invoke(cost.Key);
            ItemAdded?.Invoke(result);
            foreach (var item in changed) ItemCountChanged?.Invoke(item, GetItemCount(item));
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
                if (previousSlots[i] != _quickSlots[i]) QuickSlotChanged?.Invoke(i, _quickSlots[i]);
            InventoryChanged?.Invoke();
        }
        finally { _publishingTransaction = false; }
        return true;
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


    private int FindQuickSlot(
        ItemData itemData)
    {
        if (itemData == null)
        {
            return -1;
        }

        for (int i = 0;
             i < QUICK_SLOT_COUNT;
             i++)
        {
            if (_quickSlots[i] ==
                itemData)
            {
                return i;
            }
        }

        return -1;
    }


    private void ClearQuickSlotsContaining(
        ItemData itemData)
    {
        for (int i = 0;
             i < QUICK_SLOT_COUNT;
             i++)
        {
            if (_quickSlots[i] !=
                itemData)
            {
                continue;
            }

            _quickSlots[i] = null;

            QuickSlotChanged?.Invoke(
                i,
                null
            );
        }
    }


    private bool IsValidQuickSlotIndex(
        int slotIndex)
    {
        return
            slotIndex >= 0 &&
            slotIndex < QUICK_SLOT_COUNT;
    }
}
