using System;

public static class GameplayEvents
{
    public static event Action<uint, string, string, string> ActionDone;
    public static void RaiseActionDone(uint playerId, string actionType, string targetId, string contextId = null)
    {
        if (string.IsNullOrEmpty(actionType) || string.IsNullOrEmpty(targetId))
        {
            return;
        }
        ActionDone?.Invoke(playerId, actionType, targetId, contextId);
    }
}
