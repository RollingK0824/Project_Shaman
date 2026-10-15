using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// Session-owned state. Unity main thread only; the server adapter serializes requests here.
// No network transport, static count dictionary or player-owned material copies.
[DisallowMultipleComponent]
public class SharedMaterialInventory : MonoBehaviour
{
    private Dictionary<ItemData, int> _counts = new();
    private bool _busy;
    private Func<bool> _authorityCheck;
    public long Version { get; private set; }
    public IReadOnlyDictionary<ItemData, int> Counts => new ReadOnlyDictionary<ItemData, int>(_counts);
    public event Action<ItemData, int> MaterialCountChanged;
    public event Action Changed;
    public bool CanMutate => isActiveAndEnabled &&
        ((!NetworkClient.active && !NetworkServer.active && (_authorityCheck == null || _authorityCheck())) ||
         (NetworkServer.active && _authorityCheck != null && _authorityCheck()));
    public void SetAuthorityCheck(Func<bool> check) => _authorityCheck = check;

    public static SharedMaterialInventory ResolveOffline(GameObject player)
    {
        if (player == null || NetworkClient.active || NetworkServer.active) return null;
        foreach (var store in FindObjectsByType<SharedMaterialInventory>(FindObjectsSortMode.None))
            if (store.gameObject.scene == player.scene) return store;
        var root = new GameObject("Shared Materials (Offline Session)");
        SceneManager.MoveGameObjectToScene(root, player.scene);
        return root.AddComponent<SharedMaterialInventory>();
    }
    public int GetCount(ItemData item) => item != null && _counts.TryGetValue(item, out int n) ? n : 0;
    private static bool Material(ItemData item) => item != null && item.Category == ItemCategory.Material;
    public bool TryAdd(GameObject requester, ItemData item, int amount = 1)
    {
        if (_busy || !CanMutate || !PlayerActionGuard.CanAct(requester) || !Material(item) || amount <= 0 || (long)GetCount(item) + amount > int.MaxValue) return false;
        _busy = true;
        try { _counts[item] = GetCount(item) + amount; Version++; Publish(new[]{item}); return true; }
        finally { _busy = false; }
    }
    public bool TryRemove(GameObject requester, ItemData item, int amount = 1)
    {
        if (_busy || !CanMutate || !PlayerActionGuard.CanAct(requester) || !Material(item) || amount <= 0 || GetCount(item) < amount) return false;
        _busy = true;
        try { int next = GetCount(item) - amount; if (next == 0) _counts.Remove(item); else _counts[item] = next; Version++; Publish(new[]{item}); return true; }
        finally { _busy = false; }
    }
    public bool CanExchange(PlayerInventory recipient, IReadOnlyDictionary<ItemData,int> costs, ItemData result, int amount)
    {
        if (recipient == null || costs == null || result == null || amount <= 0 || !PlayerActionGuard.CanAct(recipient.gameObject)) return false;
        var personal = new Dictionary<ItemData,int>();
        foreach (var cost in costs)
        {
            if (cost.Key == null || cost.Value <= 0) return false;
            if (Material(cost.Key)) { if (GetCount(cost.Key) < cost.Value) return false; }
            else personal.Add(cost.Key, cost.Value);
        }
        if (Material(result))
        {
            costs.TryGetValue(result, out int spent);
            if ((long)GetCount(result) - spent + amount > int.MaxValue) return false;
        }
        return recipient.CanExchangePersonalItems(personal, Material(result) ? null : result, Material(result) ? 0 : amount);
    }
    public bool TryExchange(PlayerInventory recipient, IReadOnlyDictionary<ItemData,int> costs, ItemData result, int amount)
    {
        if (_busy || !CanMutate || !CanExchange(recipient,costs,result,amount)) return false;
        _busy = true;
        try
        {
            var next = new Dictionary<ItemData,int>(_counts);
            var personal = new Dictionary<ItemData,int>();
            var changed = new HashSet<ItemData>();
            foreach (var cost in costs)
            {
                if (!Material(cost.Key)) { personal.Add(cost.Key,cost.Value); continue; }
                int count = next[cost.Key] - cost.Value;
                if (count == 0) next.Remove(cost.Key); else next[cost.Key] = count;
                changed.Add(cost.Key);
            }
            if (Material(result)) { next.TryGetValue(result,out int n); next[result] = n + amount; changed.Add(result); }
            // The recipient stages and validates its result before commitShared runs.
            // Both stores commit before any observers run. Reentrant writes are rejected.
            return recipient.TryExchangePersonalItems(personal, Material(result) ? null : result, Material(result) ? 0 : amount,
                () => { _counts = next; Version++; }, () => Publish(changed));
        }
        finally { _busy = false; }
    }
    // Trusted replication entry point: read-only client state, ordered snapshots, no predicted spending.
    public bool ApplyReplicatedSnapshot(long version, IReadOnlyDictionary<ItemData,int> snapshot)
    {
        if (_busy || !NetworkClient.active || NetworkServer.active || version <= Version || snapshot == null) return false;
        var next = new Dictionary<ItemData,int>();
        foreach (var pair in snapshot)
        { if (!Material(pair.Key) || pair.Value < 0) return false; if (pair.Value > 0) next.Add(pair.Key,pair.Value); }
        var changed = new HashSet<ItemData>(_counts.Keys);changed.UnionWith(next.Keys);
        _busy = true;
        try { _counts = next; Version = version; Publish(changed); return true; }
        finally { _busy = false; }
    }
    public bool ResetSession()
    {
        if (_busy || !CanMutate) return false;
        var changed = new List<ItemData>(_counts.Keys);_busy = true;
        try { _counts.Clear(); Version++; Publish(changed); return true; }
        finally { _busy = false; }
    }
    private void Publish(IEnumerable<ItemData> changed)
    {
        foreach (var item in changed)
            if (MaterialCountChanged != null)
                foreach (Action<ItemData,int> listener in MaterialCountChanged.GetInvocationList())
                    try { listener(item,GetCount(item)); } catch (Exception e) { Debug.LogException(e,this); }
        if (Changed != null)
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception e) { Debug.LogException(e,this); }
    }
}
