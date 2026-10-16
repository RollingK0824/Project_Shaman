using Mirror;
using UnityEngine;

public static class PlayerItemActions
{
    // Call only after the effect/transaction succeeds. Context is optional for location-specific Use quests.
    public static void ReportUse(GameObject player, ItemData item, string contextId = null)
    {
        if (!PlayerActionGuard.CanAct(player) || item == null || string.IsNullOrEmpty(item.ItemId)) return;
        if (NetworkClient.active && !NetworkServer.active) return;
        var identity = player.GetComponent<NetworkIdentity>();
        if (NetworkServer.active && (identity == null || identity.netId == 0)) return;
        GameplayEvents.RaiseActionDone(NetworkServer.active ? identity.netId : 0, "Use", item.ItemId, contextId);
    }
}
