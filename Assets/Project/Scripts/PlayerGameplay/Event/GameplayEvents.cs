using System;
using Mirror;
using UnityEngine;

// Shared action contract supplied by the systems team. It carries confirmed facts, never requests.
public static class GameplayEvents
{
    public static event Action<uint, string, string, string> ActionDone;

    public static void RaiseActionDone(uint playerId, string actionType, string targetId, string receiverId = null)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        if (string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(targetId)) return;
        if (!NetworkServer.active) playerId = 0;
        if (ActionDone == null) return;
        foreach (Action<uint, string, string, string> listener in ActionDone.GetInvocationList())
            try { listener(playerId, actionType, targetId, receiverId); }
            catch (Exception exception) { Debug.LogException(exception); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetListeners() => ActionDone = null;
}
