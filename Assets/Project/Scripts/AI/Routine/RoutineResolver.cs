using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
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

        public bool TryResolve(VillagerProfile profile, int dayCount, int slot, int serial, float startTime, float endTime, WorkStation preferredStation, out ResolvedRoutine resolved)
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
            ReserveStation(profile, place, preferredStation, ref resolved);
            return true;
        }

        private void ReserveStation(VillagerProfile profile, PlaceArea place, WorkStation preferredStation, ref ResolvedRoutine resolved)
        {
            if (resolved.Category != RoutineCategory.Work || place.Stations.Count == 0)
            {
                return;
            }

            if (preferredStation != null && place.OwnsStation(preferredStation) && preferredStation.OccupantId == profile.VillagerId)
            {
                resolved.Station = preferredStation;
                resolved.TargetPosition = preferredStation.StandPosition;
                AILog.Log(AILog.WORK, profile.VillagerId, $"Station {preferredStation.StationId} kept for next routine");
                return;
            }

            if (place.TryReserveStation(profile.VillagerId, resolved.ActionId, out WorkStation station))
            {
                resolved.Station = station;
                resolved.TargetPosition = station.StandPosition;
            }
            else
            {
                resolved.IsWaitingForStation = true;
                resolved.TargetPosition = place.GetWaitingPoint();
                AILog.Log(AILog.WORK, profile.VillagerId, $"No free station in {place.PlaceId} yet, waiting at {resolved.TargetPosition}");
            }
        }

        private void ApplyCorruption(VillagerProfile profile, ref ResolvedRoutine resolved)
        {
            AILog.Log(AILog.STUB, profile.VillagerId, "ApplyCorruption skipped (corruption branch)");
        }
    }
}
