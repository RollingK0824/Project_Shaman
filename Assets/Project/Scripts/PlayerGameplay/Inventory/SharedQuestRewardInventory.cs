using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mirror;
using UnityEngine;

// Session-owned equipment rewards. Materials stay in SharedMaterialInventory.
public class SharedQuestRewardInventory : MonoBehaviour
{
    private readonly Dictionary<ItemData, int> _counts = new();
    private Func<bool> _authorityCheck;
    private bool _busy;
    public event Action Changed;
    public IReadOnlyDictionary<ItemData, int> Counts => new ReadOnlyDictionary<ItemData, int>(_counts);
    public bool CanMutate => isActiveAndEnabled &&
        ((!NetworkClient.active && !NetworkServer.active && (_authorityCheck == null || _authorityCheck())) ||
         (NetworkServer.active && _authorityCheck != null && _authorityCheck()));
    public void SetAuthorityCheck(Func<bool> check) => _authorityCheck = check;
    public int GetCount(ItemData item) => item != null && _counts.TryGetValue(item, out int count) ? count : 0;
    public bool TryAdd(ItemData item, int amount)
    {
        if (_busy || !CanMutate || item == null || item.IsPermanent || item.Category == ItemCategory.Material ||
            amount <= 0 || (long)GetCount(item) + amount > int.MaxValue) return false;
        _busy = true;
        try { _counts[item] = GetCount(item) + amount; Publish(); return true; }
        finally { _busy = false; }
    }
    public bool TryClaim(PlayerInventory recipient, ItemData item, int amount = 1)
    {
        if (_busy || !CanMutate || recipient == null || recipient.gameObject.scene != gameObject.scene ||
            item == null || amount <= 0 || GetCount(item) < amount || !PlayerActionGuard.CanAct(recipient.gameObject)) return false;
        _busy = true;
        try
        {
            // Both stores commit before either store publishes changes. Reentrant/double claims cannot spend twice.
            return recipient.TryExchangePersonalItems(new Dictionary<ItemData, int>(), item, amount,
                () => { int next = GetCount(item) - amount; if (next == 0) _counts.Remove(item); else _counts[item] = next; }, Publish);
        }
        finally { _busy = false; }
    }
    private void Publish()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
            try { listener(); } catch (Exception exception) { Debug.LogException(exception, this); }
    }
}
