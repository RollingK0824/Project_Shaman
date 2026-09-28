using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Routine
{
    public class RoutineResolver
    {
        private readonly System.Random _random;

        public RoutineResolver(System.Random random)
        {
            _random = random;
        }

        public bool TryResolve(VillagerProfile profile, int dayCount, int slot, int serial, float startTime, float endTime, out ResolvedRoutine resolved)
        {
            resolved = default;

            RoutineEntry routine = profile.Schedule != null ? profile.Schedule.Get(dayCount, slot) : null;
            if (routine == null)
            {
                AILog.Warn(AILog.ROUTINE, $"{profile.VillagerId} no routine at Day{dayCount} S{slot}");
                return false;
            }

            if (!PlaceRegistry.TryGet(routine.PlaceId, out PlaceArea place))
            {
                AILog.Warn(AILog.ROUTINE, $"{profile.VillagerId} place '{routine.PlaceId}' not found for {routine.RoutineId}");
                return false;
            }

            resolved = new ResolvedRoutine
            {
                Source = routine,
                DayCount = dayCount,
                Slot = slot,
                Serial = serial,
                Category = routine.Category,
                TargetPosition = place.GetRandomPoint(_random),
                StartTime = startTime,
                EndTime = endTime,
                IsCorrupted = false
            };

            ApplyCorruption(profile, ref resolved);
            return true;
        }

        private void ApplyCorruption(VillagerProfile profile, ref ResolvedRoutine resolved)
        {
            AILog.Log(AILog.STUB, profile.VillagerId, "ApplyCorruption skipped (corruption branch)");
        }
    }
}
