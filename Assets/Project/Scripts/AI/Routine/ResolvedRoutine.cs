using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.World;

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
        public WorkStation Station;
        public string ActionId => Source != null ? Source.ActionId : string.Empty;

        public override string ToString()
        {
            string id = Source != null ? Source.RoutineId : "-";
            return $"Day{DayCount} S{Slot} #{Serial} {id}({Category}) → {TargetPosition} [{StartTime:F0}s~{EndTime:F0}s]{(Station != null ? $" station={Station.StationId}" : string.Empty)}{(IsCorrupted ? " CORRUPTED" : string.Empty)}";
        }
    }
}
