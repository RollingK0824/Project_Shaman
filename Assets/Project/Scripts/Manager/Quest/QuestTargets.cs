using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Registry of quest targets and the server-side entry points that turn player actions into ActionDone reports.
// Channels: hand interaction (ReportAction), item use (NotifyItemUsed), performed action (NotifyPerformed).
public static class QuestTargets
{
    private const float ITEM_REACH = 4f;
    private const float ITEM_MIN_FACING = 0.3f;

    private static readonly Dictionary<string, IQuestTarget> _byId = new Dictionary<string, IQuestTarget>();
    private static readonly List<IQuestTarget> _all = new List<IQuestTarget>();

    public static IReadOnlyList<IQuestTarget> All => _all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        _byId.Clear();
        _all.Clear();
    }

    public static void Register(IQuestTarget target)
    {
        if (target == null || string.IsNullOrEmpty(target.QuestContextId))
        {
            return;
        }

        if (_byId.TryGetValue(target.QuestContextId, out IQuestTarget existing) && !ReferenceEquals(existing, target))
        {
            Debug.LogWarning($"[QuestTargets] Duplicate context id '{target.QuestContextId}', replaced");
            _all.Remove(existing);
        }

        _byId[target.QuestContextId] = target;
        if (!_all.Contains(target))
        {
            _all.Add(target);
        }
    }

    public static void Unregister(IQuestTarget target)
    {
        if (target == null)
        {
            return;
        }

        _all.Remove(target);
        if (!string.IsNullOrEmpty(target.QuestContextId) && _byId.TryGetValue(target.QuestContextId, out IQuestTarget existing) && ReferenceEquals(existing, target))
        {
            _byId.Remove(target.QuestContextId);
        }
    }

    public static bool TryGet(string contextId, out IQuestTarget target)
    {
        target = null;
        return !string.IsNullOrEmpty(contextId) && _byId.TryGetValue(contextId, out target) && IsAlive(target);
    }

    // Server only, after the action is confirmed. Reports only what the target accepts.
    public static bool ReportAction(GameObject player, string actionType, string actionId, IQuestTarget target)
    {
        if (!IsAlive(target) || (NetworkClient.active && !NetworkServer.active) || !target.Accepts(actionType, actionId))
        {
            return false;
        }

        GameplayEvents.RaiseActionDone(PlayerId(player), actionType, actionId, target.QuestContextId);
        return true;
    }

    // Server only, right after an item use succeeded: the accepting target the player aims at (or stands next to) gets it
    public static bool NotifyItemUsed(GameObject player, ItemData item, Vector3 aimOrigin, Vector3 aimDirection)
    {
        if (player == null || item == null || string.IsNullOrEmpty(item.ItemId))
        {
            return false;
        }

        IQuestTarget target = FindFacing(player.transform.position, aimDirection, QuestActions.USE_ON, item.ItemId);
        return target != null && ReportAction(player, QuestActions.USE_ON, item.ItemId, target);
    }

    // Server only, for ritual actions (gut ...) once their own judge confirms a repetition
    public static bool NotifyPerformed(GameObject player, string actionId, Vector3 position)
    {
        IQuestTarget target = FindFacing(position, Vector3.zero, QuestActions.PERFORM, actionId);
        return target != null && ReportAction(player, QuestActions.PERFORM, actionId, target);
    }

    private static IQuestTarget FindFacing(Vector3 from, Vector3 direction, string actionType, string actionId)
    {
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        bool useFacing = flatDirection.sqrMagnitude > 0.0001f;
        flatDirection.Normalize();

        IQuestTarget best = null;
        float bestSqr = ITEM_REACH * ITEM_REACH;

        foreach (IQuestTarget target in _all)
        {
            if (!IsAlive(target) || !target.Accepts(actionType, actionId))
            {
                continue;
            }

            Vector3 offset = target.Position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;

            if (sqr > bestSqr)
            {
                continue;
            }

            // Within arm's length facing does not matter; farther away it must be roughly in front
            if (useFacing && sqr > 1f && Vector3.Dot(flatDirection, offset.normalized) < ITEM_MIN_FACING)
            {
                continue;
            }

            best = target;
            bestSqr = sqr;
        }

        return best;
    }

    private static uint PlayerId(GameObject player)
    {
        return player != null && NetworkServer.active && player.TryGetComponent(out NetworkIdentity identity) ? identity.netId : 0;
    }

    private static bool IsAlive(IQuestTarget target)
    {
        return target != null && !(target is Object unityObject && unityObject == null);
    }
}
