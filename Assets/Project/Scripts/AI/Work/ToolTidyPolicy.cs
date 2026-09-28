using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Work
{
    public enum ToolTidyAction
    {
        None,
        GoToStorage,
        DropHere,
        CarryHome
    }

    public static class ToolTidyPolicy
    {
        public static bool ShouldKeepForRoutine(string heldTypeId, string requiredTypeId)
        {
            return !string.IsNullOrEmpty(heldTypeId) && heldTypeId == requiredTypeId;
        }

        public static ToolTidyZone DecideZone(float distanceToHome, AIBehaviourConfig config)
        {
            if (distanceToHome <= config.TidyRelaxedMaxDistance)
            {
                return ToolTidyZone.Relaxed;
            }

            return distanceToHome <= config.TidyRushedMaxDistance ? ToolTidyZone.Rushed : ToolTidyZone.VeryRushed;
        }

        public static ToolTidyAction DecideTidyAction(bool isHolding, ToolTidyZone zone, bool hasStorage)
        {
            if (!isHolding)
            {
                return ToolTidyAction.None;
            }

            switch (zone)
            {
                case ToolTidyZone.Relaxed:
                    return hasStorage ? ToolTidyAction.GoToStorage : ToolTidyAction.DropHere;
                case ToolTidyZone.Rushed:
                    return ToolTidyAction.DropHere;
                default:
                    return ToolTidyAction.CarryHome;
            }
        }
    }
}
