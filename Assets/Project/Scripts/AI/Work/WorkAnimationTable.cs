using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Work
{
    // Action id → Animator state name, loaded once from the AI data. A corrupted routine swaps its action id, so its animation follows
    public static class WorkAnimationTable
    {
        private static readonly Dictionary<string, string> _animations = new Dictionary<string, string>();
        private static readonly HashSet<string> _reportedMissing = new HashSet<string>();

        public static void Load(IReadOnlyList<ActionEntry> actions)
        {
            _animations.Clear();
            _reportedMissing.Clear();
            if (actions == null)
            {
                return;
            }

            foreach (ActionEntry action in actions)
            {
                if (action == null || string.IsNullOrEmpty(action.ActionId))
                {
                    continue;
                }

                // An empty animation is kept: the action is known and plays no clip (standing work)
                _animations[action.ActionId] = action.Animation ?? string.Empty;
            }
        }

        // Actions that play a different animation from this one, for a corrupted routine that must look wrong
        public static void CollectOtherLooking(string actionId, List<string> result)
        {
            result.Clear();
            _animations.TryGetValue(actionId ?? string.Empty, out string own);

            foreach (KeyValuePair<string, string> action in _animations)
            {
                if (action.Key != actionId && action.Value != own && action.Value.Length > 0)
                {
                    result.Add(action.Key);
                }
            }

            result.Sort(string.CompareOrdinal);
        }

        // Empty when the action has no animation: the villager keeps its locomotion pose
        public static string Get(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return string.Empty;
            }

            if (_animations.TryGetValue(actionId, out string animation))
            {
                return animation;
            }

            // Reported once per action so a missing row shows up without filling the log
            if (_reportedMissing.Add(actionId))
            {
                AILog.Warn(AILog.WORK, $"No animation for action '{actionId}' (add it to the AI data's actions)");
            }

            return string.Empty;
        }
    }
}
