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
                if (action == null || string.IsNullOrEmpty(action.ActionId) || string.IsNullOrEmpty(action.Animation))
                {
                    continue;
                }

                _animations[action.ActionId] = action.Animation;
            }
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
