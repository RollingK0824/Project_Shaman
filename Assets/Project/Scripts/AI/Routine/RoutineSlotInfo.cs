using System;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Routine
{
    [Serializable]
    public struct RoutineSlotInfo
    {
        public string RoutineId;
        public string DisplayName;
        public RoutineCategory Category;
        public string PlaceId;

        public bool IsEmpty => string.IsNullOrEmpty(RoutineId);

        public static RoutineSlotInfo From(RoutineEntry routine)
        {
            if (routine == null)
            {
                return default;
            }

            return new RoutineSlotInfo
            {
                RoutineId = routine.RoutineId,
                DisplayName = routine.DisplayName,
                Category = routine.Category,
                PlaceId = routine.PlaceId
            };
        }

        public override string ToString()
        {
            return IsEmpty ? "-" : $"{RoutineId}({Category})@{PlaceId}";
        }
    }
}
