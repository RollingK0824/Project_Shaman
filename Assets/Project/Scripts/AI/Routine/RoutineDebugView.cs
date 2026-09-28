using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Routine
{
    [Serializable]
    public class RoutineDebugView
    {
        public List<string> Schedule = new List<string>();
        public float StartOffsetSeconds;

        [Header("Current")]
        public int DayCount;
        public int Slot = -1;
        public int Serial;
        public string RoutineId;
        public string DisplayName;
        public RoutineCategory Category;
        public string PlaceId;
        public Vector3 TargetPosition;
        public float StartTime;
        public float EndTime;
        public bool IsCorrupted;
        public CorruptionAxis CorruptionAxis;
        public string CorruptionDetail;
        public float StartDelaySeconds;
        public bool MustGoHome;
        public string StationId;

        public void SetSchedule(VillagerSchedule schedule)
        {
            Schedule.Clear();

            if (schedule == null)
            {
                return;
            }

            for (int d = 0; d < schedule.CycleDays; d++)
            {
                List<string> names = new List<string>();

                for (int s = 0; s < schedule.SlotsPerDay; s++)
                {
                    RoutineEntry routine = schedule.Get(d + 1, s);
                    names.Add(routine != null ? $"{routine.DisplayName}({routine.RoutineId})" : "-");
                }

                Schedule.Add($"D{d}: {string.Join(" / ", names)}");
            }
        }

        public void SetCurrent(ResolvedRoutine resolved)
        {
            DayCount = resolved.DayCount;
            Slot = resolved.Slot;
            Serial = resolved.Serial;
            RoutineId = resolved.Source != null ? resolved.Source.RoutineId : string.Empty;
            DisplayName = resolved.Source != null ? resolved.Source.DisplayName : string.Empty;
            PlaceId = resolved.Source != null ? resolved.Source.PlaceId : string.Empty;
            Category = resolved.Category;
            TargetPosition = resolved.TargetPosition;
            StartTime = resolved.StartTime;
            EndTime = resolved.EndTime;
            IsCorrupted = resolved.IsCorrupted;
            CorruptionAxis = resolved.CorruptionAxis;
            CorruptionDetail = resolved.CorruptionDetail;
            StartDelaySeconds = resolved.StartDelaySeconds;
            StationId = resolved.Station != null ? resolved.Station.StationId : string.Empty;
        }

        public void ClearCurrent()
        {
            Slot = -1;
            RoutineId = string.Empty;
            DisplayName = string.Empty;
            PlaceId = string.Empty;
            Category = RoutineCategory.None;
            IsCorrupted = false;
            CorruptionAxis = CorruptionAxis.None;
            CorruptionDetail = string.Empty;
            StartDelaySeconds = 0f;
            StationId = string.Empty;
        }
    }
}
