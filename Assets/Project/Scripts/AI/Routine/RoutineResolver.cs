using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Routine
{
    public class RoutineResolver
    {
        private readonly System.Random _random;
        private readonly AIBehaviourConfig _config;

        public RoutineResolver(System.Random random, AIBehaviourConfig config)
        {
            _random = random;
            _config = config;
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
            if (resolved.Category != RoutineCategory.Work || place.Stations.Count == 0 || resolved.CorruptionAxis == CorruptionAxis.Place)
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
            AI_Possession possession = profile.Possession;
            if (possession == null || possession.TodayPlan == null || resolved.Category != RoutineCategory.Work)
            {
                return;
            }

            CorruptionAxis planned = possession.TodayPlan.GetSlotAxis(resolved.Slot);
            if (planned == CorruptionAxis.None)
            {
                return;
            }

            System.Random random = CorruptionPlanner.CreateRandom(possession.Seed, resolved.DayCount, resolved.Slot + 1);
            List<CorruptionAxis> order = BuildAxisOrder(planned, resolved.Source);

            foreach (CorruptionAxis axis in order)
            {
                if (TryApplyAxis(axis, possession, random, ref resolved))
                {
                    resolved.IsCorrupted = true;
                    resolved.CorruptionAxis = axis;
                    AILog.Log(AILog.GHOST, profile.VillagerId, $"Corruption {axis}{(axis != planned ? $" (fallback from {planned})" : string.Empty)}: {resolved.CorruptionDetail}");
                    return;
                }
            }
        }

        private static List<CorruptionAxis> BuildAxisOrder(CorruptionAxis planned, RoutineEntry routine)
        {
            List<CorruptionAxis> order = new List<CorruptionAxis> { planned };

            foreach (CorruptionAxis axis in CorruptionPlanner.GetAvailableAxes(routine))
            {
                if (!order.Contains(axis))
                {
                    order.Add(axis);
                }
            }

            return order;
        }

        private bool TryApplyAxis(CorruptionAxis axis, AI_Possession possession, System.Random random, ref ResolvedRoutine resolved)
        {
            RoutineEntry routine = resolved.Source;

            switch (axis)
            {
                case CorruptionAxis.Tool:
                    return TryApplyToolAxis(routine, possession, random, ref resolved);
                case CorruptionAxis.Action:
                    if (routine.WrongActionIds.Count == 0)
                    {
                        return false;
                    }

                    resolved.ActionOverride = routine.WrongActionIds[random.Next(routine.WrongActionIds.Count)];
                    resolved.CorruptionDetail = $"action {routine.ActionId} → {resolved.ActionOverride}";
                    return true;
                case CorruptionAxis.Place:
                    if (routine.WrongPlaceIds.Count == 0)
                    {
                        return false;
                    }

                    string wrongPlaceId = routine.WrongPlaceIds[random.Next(routine.WrongPlaceIds.Count)];
                    if (!PlaceRegistry.TryGet(wrongPlaceId, out PlaceArea wrongPlace))
                    {
                        return false;
                    }

                    resolved.TargetPosition = wrongPlace.GetRandomPoint(_random);
                    resolved.CorruptionDetail = $"place {routine.PlaceId} → {wrongPlaceId}";
                    return true;
                case CorruptionAxis.Time:
                    float slotLength = Mathf.Max(1f, resolved.EndTime - resolved.StartTime);
                    float ratio = Mathf.Lerp(_config.TimeDelayMinRatio, _config.TimeDelayMaxRatio, (float)random.NextDouble());
                    resolved.StartDelaySeconds = slotLength * ratio;
                    resolved.CorruptionDetail = $"start delay {resolved.StartDelaySeconds:F1}s";
                    return true;
                default:
                    return false;
            }
        }

        private bool TryApplyToolAxis(RoutineEntry routine, AI_Possession possession, System.Random random, ref ResolvedRoutine resolved)
        {
            if (routine.WrongToolIds.Count == 0 || !PlaceRegistry.TryGet(routine.PlaceId, out PlaceArea place))
            {
                return false;
            }

            List<string> feasible = new List<string>();

            foreach (string toolType in routine.WrongToolIds)
            {
                if (ToolRegistry.FindAvailableNear(toolType, place.Center, _config.ToolCorruptionSearchRadius, null) != null)
                {
                    feasible.Add(toolType);
                }
            }

            if (feasible.Count > 0)
            {
                resolved.ToolTypeOverride = feasible[random.Next(feasible.Count)];
                resolved.CorruptionDetail = $"tool {routine.ToolId} → {resolved.ToolTypeOverride} (real tool)";
                return true;
            }

            if (possession.Yin >= _config.EmptyHandToolYinThreshold && random.NextDouble() < _config.EmptyHandToolChance)
            {
                resolved.ToolTypeOverride = string.Empty;
                resolved.CorruptionDetail = $"tool {routine.ToolId} → pulled from thin air";
                AILog.Log(AILog.STUB, possession.GhostId, "[Stub] Symptom EmptyHandTool (tool appears from nowhere)");
                return true;
            }

            return false;
        }
    }
}
