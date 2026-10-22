using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Request;

namespace ProjectShaman.AI.Observation
{
    // Server only. "Nobody saw OO all day": judged over the whole day, so start offsets, travel and delays never matter,
    // and nobody needs to know anyone else's schedule. Killings happen at night inside houses, so a body is never
    // seen directly; being missed is how the village finds out.
    // Night start: anyone no living villager saw today is missed; the villager who saw them last asks next morning.
    public sealed class AbsenceTracker
    {
        private readonly VillagerRegistry _registry;
        private readonly HashSet<string> _reported = new HashSet<string>();
        private readonly List<(string GiverId, string MissingId, int Day)> _pending = new List<(string, string, int)>();

        public AbsenceTracker(VillagerRegistry registry)
        {
            _registry = registry;
        }

        public void OnNightStart(IReadOnlyList<AIManager.VillagerRuntime> villagers, int day)
        {
            if (_registry == null || !AnySightingOn(villagers, day))
            {
                // No simulated day (e.g. villagers spawned after it began): nothing to judge
                return;
            }

            foreach (IVillagerView view in _registry.Villagers)
            {
                // The request is a hint about a death nobody has found yet; the living are never "missing"
                if (view.IsAlive || view.IsDeathKnown)
                {
                    continue;
                }

                string missingId = view.VillagerId;
                bool seenToday = false;
                AIManager.VillagerRuntime lastWitness = null;
                float lastTime = float.MinValue;

                foreach (AIManager.VillagerRuntime witness in villagers)
                {
                    if (witness.Id == missingId || witness.Memory == null || !witness.Memory.TryGetLastSeen(missingId, out int seenDay, out float seenTime))
                    {
                        continue;
                    }

                    seenToday |= seenDay == day;
                    if (seenTime > lastTime)
                    {
                        lastTime = seenTime;
                        lastWitness = witness;
                    }
                }

                if (!seenToday && BodyAlreadySeen(villagers, missingId))
                {
                    continue;
                }

                if (seenToday)
                {
                    // Seen again: may be missed again on a later day
                    _reported.Remove(missingId);
                    continue;
                }

                if (lastWitness == null || !_reported.Add(missingId))
                {
                    continue;
                }

                _pending.Add((lastWitness.Id, missingId, day));
                AILog.Log(AILog.MANAGER, lastWitness.Id, $"Nobody saw {missingId} on day {day}, will ask tomorrow");
            }
        }

        public void OnDayStart(IReadOnlyList<AIManager.VillagerRuntime> villagers, int day)
        {
            foreach ((string giverId, string missingId, int missedDay) in _pending)
            {
                AIManager.VillagerRuntime giver = Find(villagers, giverId);
                if (giver == null || giver.Memory == null)
                {
                    continue;
                }

                // Known by name: being missed is no evidence of possession
                giver.Memory.RecordFact(new MemoryEntry
                {
                    Kind = MemoryKind.Absent,
                    Source = MemorySource.Direct,
                    SubjectId = missingId,
                    ActorId = missingId,
                    VerbId = ObservationVerbs.ABSENT,
                    TraitMask = 7,
                    IsAnomalous = true,
                    DayCount = missedDay,
                    RecordedTime = 0f
                });

                AIEvents.RaiseRequest(giverId, VillagerRequestRouter.CHECK_ON, missingId);
            }

            _pending.Clear();
        }

        public void Clear()
        {
            _reported.Clear();
            _pending.Clear();
        }

        private static bool BodyAlreadySeen(IReadOnlyList<AIManager.VillagerRuntime> villagers, string missingId)
        {
            foreach (AIManager.VillagerRuntime witness in villagers)
            {
                if (witness.Memory == null || witness.Memory.Events == null)
                {
                    continue;
                }

                foreach (MemoryEntry fact in witness.Memory.Events.Get(MemoryKind.TraceWitness))
                {
                    if (fact.ActorId == missingId)
                    {
                        return true;
                    }
                }

                foreach (MemoryEntry fact in witness.Memory.Events.Get(MemoryKind.Death))
                {
                    if (fact.ActorId == missingId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool AnySightingOn(IReadOnlyList<AIManager.VillagerRuntime> villagers, int day)
        {
            foreach (AIManager.VillagerRuntime witness in villagers)
            {
                if (witness.Memory == null)
                {
                    continue;
                }

                foreach (IVillagerView view in _registry.Villagers)
                {
                    if (view.VillagerId != witness.Id && witness.Memory.TryGetLastSeen(view.VillagerId, out int seenDay, out _) && seenDay == day)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static AIManager.VillagerRuntime Find(IReadOnlyList<AIManager.VillagerRuntime> villagers, string id)
        {
            foreach (AIManager.VillagerRuntime villager in villagers)
            {
                if (villager.Id == id)
                {
                    return villager;
                }
            }

            return null;
        }
    }
}
