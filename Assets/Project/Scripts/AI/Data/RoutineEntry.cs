using System;
using System.Collections.Generic;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class RoutineEntry
    {
        public string RoutineId;
        public string DisplayName;
        public RoutineCategory Category = RoutineCategory.Work;
        public List<string> JobIds = new List<string>();
        public AttributeCondition Condition = new AttributeCondition();
        public string PlaceId;
        public string ToolId;
        public string ActionId;
        public bool IsCoop;
        public int MaxPeople = 1;

        public bool IsLoadableFor(VillagerPublicInfo info)
        {
            bool isJobAllowed = Category == RoutineCategory.Rest || JobIds == null || JobIds.Count == 0 || JobIds.Contains(info.JobId);
            return isJobAllowed && Condition.Matches(info.Gender, info.SocialClass, info.AgeGroup);
        }

        public override string ToString()
        {
            return $"{RoutineId}({Category})@{PlaceId}";
        }
    }
}
