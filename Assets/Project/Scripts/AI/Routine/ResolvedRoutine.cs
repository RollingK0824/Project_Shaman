using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Routine
{
    public struct ResolvedRoutine
    {
        public RoutineEntry Source;
        public int DayCount;
        public int Slot;
        public int Serial;
        public RoutineCategory Category;
        public Vector3 TargetPosition;
        public float StartTime;
        public float EndTime;
        public bool IsCorrupted;

        public override string ToString()
        {
            string id = Source != null ? Source.RoutineId : "-";
            return $"Day{DayCount} S{Slot} #{Serial} {id}({Category}) → {TargetPosition} [{StartTime:F0}s~{EndTime:F0}s]{(IsCorrupted ? " CORRUPTED" : string.Empty)}";
        }
    }
}
