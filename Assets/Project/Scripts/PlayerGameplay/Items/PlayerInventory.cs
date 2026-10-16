using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerNotebook))]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private SharedMaterialInventory _sharedMaterials;
    public SharedMaterialInventory SharedMaterials
    {
        get
        {
            if (_sharedMaterials == null)
            {
                var local = SharedMaterialInventory.ResolveOffline(gameObject);
                if (local != null) BindSharedMaterials(local);
            }
            return _sharedMaterials;
        }
    }
    public void BindSharedMaterials(SharedMaterialInventory store)
    {
        if (_sharedMaterials != null) _sharedMaterials.Changed -= HandleMaterialsChanged;
        _sharedMaterials = store;
        if (isActiveAndEnabled && _sharedMaterials != null) _sharedMaterials.Changed += HandleMaterialsChanged;
        InventoryChanged?.Invoke();
    }
    private void HandleMaterialsChanged() => InventoryChanged?.Invoke();
    private void OnEnable()
    {
        BindTalismans();
        ValidateTalismanBindings();
        if (_sharedMaterials == null) _sharedMaterials = SharedMaterialInventory.ResolveOffline(gameObject);
        if (_sharedMaterials != null) _sharedMaterials.Changed += HandleMaterialsChanged;
    }
    private void OnDisable()
    {
        if (_sharedMaterials != null) _sharedMaterials.Changed -= HandleMaterialsChanged;
        if (_talismans != null) _talismans.SelectionChanged -= ValidateTalismanBindings;
    }
    private static bool IsMaterial(ItemData item) => item != null && item.Category == ItemCategory.Material;
    private static bool UsesShared(IReadOnlyDictionary<ItemData,int> costs, ItemData result)
    {
        if (IsMaterial(result)) return true;
        if (costs != null) foreach (var cost in costs) if (IsMaterial(cost.Key)) return true;
        return false;
    }
    private const int QUICK_SLOT_COUNT = 6;

    // 같은 ItemData가 여러 번 들어 있으면
    // 해당 아이템을 여러 개 보유한 것으로 취급한다.
    private readonly List<ItemData> _items =
        new List<ItemData>();

    private ItemData[] _quickSlots;
    // Ordinary stacks keep the existing ItemData API. Only saved drawings carry an ID.
    private readonly string[] _quickSlotInstanceIds = new string[QUICK_SLOT_COUNT];
    private PlayerTalismanCollection _talismans;

    private void BindTalismans()
    {
        var collection = GetComponent<PlayerTalismanCollection>();
        if (_talismans != null) _talismans.SelectionChanged -= ValidateTalismanBindings;
        _talismans = collection;
        if (_talismans != null) _talismans.SelectionChanged += ValidateTalismanBindings;
    }

    public string GetQuickSlotInstanceId(int slotIndex) =>
        IsValidQuickSlotIndex(slotIndex) ? _quickSlotInstanceIds[slotIndex] : null;

    public TalismanDrawingData GetQuickSlotDrawing(int slotIndex)
    {
        if (!IsValidQuickSlotIndex(slotIndex) || string.IsNullOrEmpty(_quickSlotInstanceIds[slotIndex])) return null;
        var collection = _talismans != null ? _talismans : GetComponent<PlayerTalismanCollection>();
        if (collection == null) return null;
        var drawing = collection.Find(_quickSlotInstanceIds[slotIndex], out var template);
        return template == _quickSlots[slotIndex] ? drawing : null;
    }

    public bool AssignTalismanQuickSlot(int slotIndex, string instanceId)
    {
        if (_publishingTransaction || !PlayerActionGuard.CanAct(gameObject) || !IsValidQuickSlotIndex(slotIndex) || string.IsNullOrEmpty(instanceId)) return false;
        BindTalismans();
        if (_talismans == null) return false;
        var drawing = _talismans.Find(instanceId, out var template);
        if (drawing == null || template == null || !template.IsEffectTalisman || !template.CanUseQuickSlot || template.FindEffect(drawing.EffectId) == null) return false;
        for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            if (i != slotIndex && _quickSlotInstanceIds[i] == instanceId) return false;
        _quickSlots[slotIndex] = template;
        _quickSlotInstanceIds[slotIndex] = instanceId;
        QuickSlotChanged?.Invoke(slotIndex, template);
        return true;
    }

    // Ownership cleanup is not a player action: it must also run during death/reset/snapshot updates.
    private void ValidateTalismanBindings()
    {
        if (_quickSlots == null) return;
        for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            if (!string.IsNullOrEmpty(_quickSlotInstanceIds[i]) && GetQuickSlotDrawing(i) == null)
            {
                _quickSlots[i] = null;
                _quickSlotInstanceIds[i] = null;
                QuickSlotChanged?.Invoke(i, null);
            }
    }

    public IReadOnlyList<ItemData> Items => _items;

    public int QuickSlotCount => QUICK_SLOT_COUNT;

    public event Action<ItemData> ItemAdded;
    public event Action<ItemData> ItemRemoved;

    public event Action<ItemData, int> ItemCountChanged;

    public event Action<int, ItemData> QuickSlotChanged;
    public event Action InventoryChanged;
    // Per-unit delta lets instance data follow consumption/crafting without changing stack behavior.
    public event Action<ItemData, int> ItemUnitsChanged;
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

        if (itemData.IsUnique && Contains(itemData)) return false;
        if (IsMaterial(itemData)) return SharedMaterials != null && SharedMaterials.TryAdd(gameObject,itemData);

        _items.Add(itemData);
        ItemUnitsChanged?.Invoke(itemData, 1);

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
        if (_publishingTransaction || itemData == null || itemData.IsPermanent ||
            amount <= 0)
        {
            return false;
        }

        if (IsMaterial(itemData)) return SharedMaterials != null && SharedMaterials.TryRemove(gameObject,itemData,amount);

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
        ItemUnitsChanged?.Invoke(itemData, -amount);

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

        if (IsMaterial(itemData)) return SharedMaterials != null ? SharedMaterials.GetCount(itemData) : 0;

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

        ValidateTalismanBindings();
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

                if (_quickSlots[i] == itemData && string.IsNullOrEmpty(_quickSlotInstanceIds[i]))
                {
                    _quickSlots[i] = null;

                    QuickSlotChanged?.Invoke(
                        i,
                        null
                    );
                }
            }
        }

        _quickSlotInstanceIds[slotIndex] = null;
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

        _quickSlotInstanceIds[slotIndex] = null;
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
        if (result == null || resultAmount <= 0) return false;
        return UsesShared(costs,result) ? SharedMaterials != null && SharedMaterials.CanExchange(this,costs,result,resultAmount)
            : CanExchangePersonalItems(costs,result,resultAmount);
    }
    internal bool CanExchangePersonalItems(IReadOnlyDictionary<ItemData, int> costs, ItemData result, int resultAmount)
    {
        if (!PlayerActionGuard.CanAct(gameObject) || costs == null || (result == null ? resultAmount != 0 : resultAmount <= 0) || IsMaterial(result)) return false;
        if (result != null && result.IsUnique && (resultAmount != 1 || Contains(result))) return false;
        long finalCount = (long)_items.Count + resultAmount;
        foreach (var cost in costs)
        {
            if (cost.Key == null || cost.Key.IsPermanent || IsMaterial(cost.Key) || cost.Value <= 0 || !HasItem(cost.Key, cost.Value)) return false;
            finalCount -= cost.Value;
        }
        return finalCount >= 0 && finalCount <= int.MaxValue;
    }

    // Commit all counts and slots before notifying subscribers. Reentrant writes are rejected.
    public bool TryExchangeItems(IReadOnlyDictionary<ItemData, int> costs, ItemData result, int resultAmount)
    {
        if (_publishingTransaction || result == null || resultAmount <= 0) return false;
        return UsesShared(costs,result) ? SharedMaterials != null && SharedMaterials.TryExchange(this,costs,result,resultAmount)
            : TryExchangePersonalItems(costs,result,resultAmount,null,null);
    }
    internal bool TryExchangePersonalItems(IReadOnlyDictionary<ItemData,int> costs, ItemData result, int resultAmount, Action commitShared, Action publishShared)
    {
        if (_publishingTransaction || !CanExchangePersonalItems(costs,result,resultAmount)) return false;
        var nextItems = new List<ItemData>(_items);
        var changed = new HashSet<ItemData>();
        foreach (var cost in costs)
        {
            for (int i = 0; i < cost.Value; i++) nextItems.Remove(cost.Key);
            changed.Add(cost.Key);
        }
        for (int i = 0; i < resultAmount; i++) nextItems.Add(result);
        if (result != null) changed.Add(result);
        var previousSlots = (ItemData[])_quickSlots.Clone();
        _items.Clear();
        _items.AddRange(nextItems);
        for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            if (_quickSlots[i] != null && string.IsNullOrEmpty(_quickSlotInstanceIds[i]) && (!_quickSlots[i].CanUseQuickSlot || !Contains(_quickSlots[i]))) _quickSlots[i] = null;
        if (result != null && result.CanUseQuickSlot && FindQuickSlot(result) < 0)
        {
            int slot = FindFirstEmptyQuickSlot();
            if (slot >= 0) _quickSlots[slot] = result;
        }
        _publishingTransaction = true;
        try
        {
            commitShared?.Invoke();
            foreach (var cost in costs) ItemUnitsChanged?.Invoke(cost.Key, -cost.Value);
            if (result != null) ItemUnitsChanged?.Invoke(result, resultAmount);
            foreach (var cost in costs) ItemRemoved?.Invoke(cost.Key);
            if (result != null) ItemAdded?.Invoke(result);
            foreach (var item in changed) ItemCountChanged?.Invoke(item, GetItemCount(item));
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
                if (previousSlots[i] != _quickSlots[i]) QuickSlotChanged?.Invoke(i, _quickSlots[i]);
            InventoryChanged?.Invoke();
        }
        catch (Exception e) { Debug.LogException(e,this); } // Counts already committed; observer failure is not a failed craft.
        finally { try { publishShared?.Invoke(); } finally { _publishingTransaction = false; } }
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
            if (_quickSlots[i] == itemData && string.IsNullOrEmpty(_quickSlotInstanceIds[i]))
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
            if (_quickSlots[i] != itemData || !string.IsNullOrEmpty(_quickSlotInstanceIds[i]))
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
