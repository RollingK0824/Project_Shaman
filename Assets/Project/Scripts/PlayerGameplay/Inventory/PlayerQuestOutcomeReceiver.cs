using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// The systems team's QuestEnded adapter calls this after server settlement.
// This component does not offer, progress, complete or synchronize quests.
public class PlayerQuestOutcomeReceiver : MonoBehaviour
{
    [SerializeField] private QuestItemCatalog _catalog;
    [SerializeField] private SharedMaterialInventory _materials;
    [SerializeField] private SharedQuestRewardInventory _rewards;
    private readonly HashSet<string> _applied = new();
    private Func<bool> _authorityCheck;
    private bool _busy;
    public SharedQuestRewardInventory Rewards => _rewards;
    public void Bind(QuestItemCatalog catalog, SharedMaterialInventory materials, SharedQuestRewardInventory rewards)
    { _catalog = catalog; _materials = materials; _rewards = rewards; }
    public void SetAuthorityCheck(Func<bool> check) => _authorityCheck = check;
    public bool CanApply => isActiveAndEnabled &&
        ((!NetworkClient.active && !NetworkServer.active && (_authorityCheck == null || _authorityCheck())) ||
         (NetworkServer.active && _authorityCheck != null && _authorityCheck()));

    public bool TryApplyEnded(int dayIssued, int instanceId, string state, string type, string id, int amount)
    {
        string key = dayIssued + ":" + instanceId;
        if (_busy || !CanApply || dayIssued < 0 || instanceId < 0 || _applied.Contains(key)) return false;
        if (state != "Completed" && state != "Failed" && state != "Cancelled") return false;
        _busy = true;
        try
        {
            bool applied = state == "Cancelled";
            if (state == "Completed" && type == "Item" && amount > 0)
            {
                var item = _catalog != null ? _catalog.Find(id) : null;
                if (item == null) return false;
                applied = item.Category == ItemCategory.Material
                    ? _materials != null && _materials.TryAdd(gameObject, item, amount)
                    : _rewards != null && _rewards.TryAdd(item, amount);
            }
            else if (state == "Failed" && type == "Stress" && amount > 0)
            {
                foreach (var player in FindObjectsByType<PlayerStressController>(FindObjectsSortMode.None))
                    if (player.gameObject.scene == gameObject.scene && PlayerActionGuard.CanAct(player.gameObject))
                        player.ReceiveStress(amount, StressCause.Other, gameObject);
                applied = true;
            }
            if (!applied) return false;
            _applied.Add(key);
            Debug.Log($"[QuestOutcome] {key} {state} {type} {id} x{amount}", this);
            return true;
        }
        finally { _busy = false; }
    }

    public static PlayerQuestOutcomeReceiver Resolve(GameObject player, QuestItemCatalog catalog)
    {
        if (player == null) return null;
        foreach (var receiver in FindObjectsByType<PlayerQuestOutcomeReceiver>(FindObjectsSortMode.None))
            if (receiver.gameObject.scene == player.scene) return receiver;
        // Network session objects and authority delegates belong to the network adapter.
        if (NetworkClient.active || NetworkServer.active) return null;
        var materials = SharedMaterialInventory.ResolveOffline(player);
        if (materials == null) return null;
        var rewards = materials.gameObject.AddComponent<SharedQuestRewardInventory>();
        var result = materials.gameObject.AddComponent<PlayerQuestOutcomeReceiver>();
        result.Bind(catalog, materials, rewards);
        return result;
    }
}
