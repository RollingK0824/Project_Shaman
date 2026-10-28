using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Work
{
    // Action id → animation, loaded once from the AI data. A corrupted routine swaps its action id, so its animation follows
    public static class WorkAnimationTable
    {
        private static readonly Dictionary<string, WorkAnimation> _animations = new Dictionary<string, WorkAnimation>();
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

                _animations[action.ActionId] = action.Animation;
            }
        }

        public static WorkAnimation Get(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return WorkAnimation.None;
            }

            if (_animations.TryGetValue(actionId, out WorkAnimation animation))
            {
                return animation;
            }

            // Reported once per action so a missing row shows up without filling the log
            if (_reportedMissing.Add(actionId))
            {
                AILog.Warn(AILog.WORK, $"No animation for action '{actionId}' (add it to the AI data's actions)");
            }

            return WorkAnimation.None;
        }
    }
}
