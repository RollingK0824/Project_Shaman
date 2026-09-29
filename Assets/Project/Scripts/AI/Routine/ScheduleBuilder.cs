using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Routine
{
    public class ScheduleBuilder
    {
        private readonly IReadOnlyList<RoutineEntry> _routines;
        private readonly int _cycleDays;
        private readonly int _slotsPerDay;
        private readonly int _minRestPerDay;
        private readonly System.Random _random;
        private readonly System.Func<string, int> _placeCapacityProvider;
        private readonly Dictionary<string, int> _occupancy = new Dictionary<string, int>();

        public ScheduleBuilder(IReadOnlyList<RoutineEntry> routines, int cycleDays, int slotsPerDay, int minRestPerDay, System.Random random, System.Func<string, int> placeCapacityProvider)
        {
            _placeCapacityProvider = placeCapacityProvider;
            _routines = routines;
            _cycleDays = cycleDays;
            _slotsPerDay = slotsPerDay;
            _minRestPerDay = minRestPerDay;
            _random = random;
        }

        public bool TryBuild(VillagerPublicInfo info, out VillagerSchedule schedule)
        {
            schedule = null;

            List<RoutineEntry> works = new List<RoutineEntry>();
            List<RoutineEntry> rests = new List<RoutineEntry>();
            CollectLoadable(info, works, rests);

            if (rests.Count == 0 && _minRestPerDay > 0)
            {
                AILog.Log(AILog.ROUTINE, info.VillagerId, $"No loadable rest routine for {info.JobId}");
                return false;
            }

            if (works.Count == 0)
            {
                AILog.Log(AILog.ROUTINE, info.VillagerId, $"No loadable work routine for {info.JobId}");
                return false;
            }

            VillagerSchedule result = new VillagerSchedule(_cycleDays, _slotsPerDay);
            List<string> reservedKeys = new List<string>();

            for (int day = 0; day < _cycleDays; day++)
            {
                HashSet<int> restSlots = PickRestSlots();

                for (int slot = 0; slot < _slotsPerDay; slot++)
                {
                    bool isRestSlot = restSlots.Contains(slot);
                    RoutineEntry picked = PickAvailable(isRestSlot ? rests : works, day, slot);

                    if (picked == null && !isRestSlot)
                    {
                        picked = PickAvailable(rests, day, slot);
                        if (picked != null)
                        {
                            AILog.Log(AILog.ROUTINE, info.VillagerId, $"D{day} S{slot} work full, fallback to rest {picked.RoutineId}");
                        }
                    }

                    if (picked == null)
                    {
                        AILog.Warn(AILog.ROUTINE, $"{info.VillagerId} D{day} S{slot} no work or rest routine with free capacity");
                        ReleaseAll(reservedKeys);
                        return false;
                    }

                    string routineKey = MakeKey(day, slot, picked.RoutineId);
                    string placeKey = MakePlaceKey(day, slot, picked.PlaceId);
                    Reserve(routineKey);
                    Reserve(placeKey);
                    reservedKeys.Add(routineKey);
                    reservedKeys.Add(placeKey);
                    result.Set(day, slot, picked);
                }
            }

            schedule = result;
            return true;
        }

        private void CollectLoadable(VillagerPublicInfo info, List<RoutineEntry> works, List<RoutineEntry> rests)
        {
            foreach (RoutineEntry routine in _routines)
            {
                if (routine == null || string.IsNullOrEmpty(routine.RoutineId) || !routine.IsLoadableFor(info))
                {
                    continue;
                }

                if (routine.Category == RoutineCategory.Rest)
                {
                    rests.Add(routine);
                }
                else if (routine.Category == RoutineCategory.Work)
                {
                    works.Add(routine);
                }
            }
        }

        private HashSet<int> PickRestSlots()
        {
            HashSet<int> restSlots = new HashSet<int>();
            int count = System.Math.Min(_minRestPerDay, _slotsPerDay);

            while (restSlots.Count < count)
            {
                restSlots.Add(_random.Next(_slotsPerDay));
            }

            return restSlots;
        }

        private RoutineEntry PickAvailable(List<RoutineEntry> pool, int day, int slot)
        {
            List<RoutineEntry> available = new List<RoutineEntry>();

            foreach (RoutineEntry routine in pool)
            {
                int routineLimit = System.Math.Max(1, routine.MaxPeople);
                int placeLimit = GetPlaceCapacity(routine.PlaceId);
                bool hasRoutineRoom = GetOccupancy(MakeKey(day, slot, routine.RoutineId)) < routineLimit;
                bool hasPlaceRoom = GetOccupancy(MakePlaceKey(day, slot, routine.PlaceId)) < placeLimit;

                if (hasRoutineRoom && hasPlaceRoom)
                {
                    available.Add(routine);
                }
            }

            return available.Count > 0 ? available[_random.Next(available.Count)] : null;
        }

        private int GetPlaceCapacity(string placeId)
        {
            return _placeCapacityProvider != null ? _placeCapacityProvider(placeId) : int.MaxValue;
        }

        private static string MakePlaceKey(int day, int slot, string placeId)
        {
            return $"P:{day}:{slot}:{placeId}";
        }

        private static string MakeKey(int day, int slot, string routineId)
        {
            return $"{day}:{slot}:{routineId}";
        }

        private int GetOccupancy(string key)
        {
            return _occupancy.TryGetValue(key, out int count) ? count : 0;
        }

        private void Reserve(string key)
        {
            _occupancy[key] = GetOccupancy(key) + 1;
        }

        private void ReleaseAll(List<string> keys)
        {
            foreach (string key in keys)
            {
                _occupancy[key] = GetOccupancy(key) - 1;
            }
        }
    }
}
