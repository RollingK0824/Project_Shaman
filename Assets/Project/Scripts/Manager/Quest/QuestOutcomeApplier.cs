using Mirror;
using UnityEngine;

// Server (or offline) side of QuestEnded: rewards go to the player who took the quest, penalties hit the team.
// Item rewards go through PlayerInventory, which already routes materials into the synced shared store
// and mirrors personal items to the owning client.
public static class QuestOutcomeApplier
{
    public static void Apply(IQuestView quest, QuestItemCatalog catalog)
    {
        if (quest == null || (NetworkClient.active && !NetworkServer.active))
        {
            return;
        }

        if (quest.State == QuestState.Completed)
        {
            ApplyReward(quest, catalog);
        }
        else if (quest.State == QuestState.Failed)
        {
            ApplyPenalty(quest);
        }
    }

    private static void ApplyReward(IQuestView quest, QuestItemCatalog catalog)
    {
        QuestEffect reward = quest.Definition.Reward;
        if (reward.Type != "Item" || reward.Amount <= 0)
        {
            return;
        }

        ItemData item = catalog != null ? catalog.Find(reward.Id) : null;
        if (item == null)
        {
            Debug.LogWarning($"[QuestOutcome] Reward item '{reward.Id}' of {quest.Definition.Id} not in QuestItemCatalog (assign it on QuestManager)");
            return;
        }

        PlayerInventory recipient = FindRecipient(quest.AcceptedBy);
        if (recipient == null || !recipient.TryAddItem(item, reward.Amount))
        {
            Debug.LogWarning($"[QuestOutcome] Reward {item.DisplayName} x{reward.Amount} for {quest.Definition.Id} could not be given");
            return;
        }

        GameLog.Log($"[QuestOutcome] #{quest.InstanceId} {quest.Definition.Id} completed → {recipient.name} +{item.DisplayName} x{reward.Amount}");
    }

    private static void ApplyPenalty(IQuestView quest)
    {
        QuestEffect penalty = quest.Definition.Penalty;
        if (penalty.Type != "Stress" || penalty.Amount <= 0)
        {
            return;
        }

        int count = 0;
        foreach (PlayerStressController player in Object.FindObjectsByType<PlayerStressController>(FindObjectsSortMode.None))
        {
            if (PlayerActionGuard.CanAct(player.gameObject))
            {
                player.ReceiveStress(penalty.Amount, StressCause.Other);
                count++;
            }
        }

        GameLog.Log($"[QuestOutcome] #{quest.InstanceId} {quest.Definition.Id} failed → stress +{penalty.Amount} to {count} players");
    }

    // The player who accepted it, else anyone alive (offline: the local player)
    private static PlayerInventory FindRecipient(uint netId)
    {
        if (netId != 0 && NetworkServer.active && NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity)
            && identity != null && identity.TryGetComponent(out PlayerInventory owner) && PlayerActionGuard.CanAct(owner.gameObject))
        {
            return owner;
        }

        foreach (PlayerInventory inventory in Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
        {
            if (PlayerActionGuard.CanAct(inventory.gameObject))
            {
                return inventory;
            }
        }

        return null;
    }
}
