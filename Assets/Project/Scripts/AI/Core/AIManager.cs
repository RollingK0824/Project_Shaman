using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Core
{
    public class AIManager : MonoBehaviour
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private MonoBehaviour _timeSourceBehaviour;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;

        private readonly List<VillagerRuntime> _villagers = new List<VillagerRuntime>();
        private ITimeSource _timeSource;
        private RoutineResolver _resolver;
        private bool _isRunning;

        public IReadOnlyList<VillagerRuntime> Villagers => _villagers;

        private void Awake()
        {
            _timeSource = _timeSourceBehaviour as ITimeSource;

            if (_factory != null)
            {
                _factory.OnVillagersCreated += HandleVillagersCreated;
            }
        }

        private void OnDestroy()
        {
            if (_factory != null)
            {
                _factory.OnVillagersCreated -= HandleVillagersCreated;
            }

            if (_timeSource != null)
            {
                _timeSource.OnNewDay -= HandleNewDay;
                _timeSource.OnNightStart -= HandleNightStart;
            }
        }

        private void HandleVillagersCreated(IReadOnlyList<AI_Core> cores)
        {
            if (_timeSource == null || _behaviourConfig == null)
            {
                AILog.Error(AILog.MANAGER, "Time source or behaviour config missing, AIManager not started");
                return;
            }

            _resolver = new RoutineResolver(new System.Random(_factory.Seed + 1));

            foreach (AI_Core core in cores)
            {
                if (core != null && core.Profile != null)
                {
                    _villagers.Add(new VillagerRuntime(core));
                }
            }

            AILog.Log(AILog.MANAGER, $"Registered {_villagers.Count} villagers, places={PlaceRegistry.Count}, time={_timeSource.SourceName}");

            _timeSource.OnNewDay += HandleNewDay;
            _timeSource.OnNightStart += HandleNightStart;
            _isRunning = true;

            if (!_timeSource.IsRunning)
            {
                _timeSource.StartClock();
            }
        }

        private void HandleNewDay(int dayCount)
        {
            AILog.Log(AILog.TIME_TO_AI, $"NewDay {dayCount} (cycleDay={(dayCount - 1) % _behaviourConfig.CycleDays})");

            foreach (VillagerRuntime villager in _villagers)
            {
                villager.ResetForNewDay(CalculateHomeReturnTime(villager, dayCount));
                villager.Core.ClearRoutine();
                villager.Core.SetMustGoHome(false);
                AILog.Log(AILog.MANAGER, villager.Id, $"Day {dayCount} offset={villager.Profile.StartOffsetSeconds:F1}s homeReturn={villager.HomeReturnTime:F1}s");
            }
        }

        private void HandleNightStart()
        {
            AILog.Log(AILog.TIME_TO_AI, $"NightStart Day {_timeSource.DayCount}");

            foreach (VillagerRuntime villager in _villagers)
            {
                if (!villager.IsHomeOrdered)
                {
                    AILog.Warn(AILog.MANAGER, $"{villager.Id} not ordered home before night, forcing");
                    OrderGoHome(villager);
                }
            }
        }

        private void Update()
        {
            if (!_isRunning || _timeSource.IsNight)
            {
                return;
            }

            float now = _timeSource.PhaseElapsed;

            foreach (VillagerRuntime villager in _villagers)
            {
                TickVillager(villager, now);
            }
        }

        private void TickVillager(VillagerRuntime villager, float now)
        {
            if (villager.IsHomeOrdered)
            {
                return;
            }

            if (now >= villager.HomeReturnTime)
            {
                OrderGoHome(villager);
                return;
            }

            float slotDuration = _timeSource.SlotDuration;
            float localTime = now - villager.Profile.StartOffsetSeconds;

            if (localTime < 0f)
            {
                return;
            }

            int slot = Mathf.FloorToInt(localTime / slotDuration);
            if (slot >= _timeSource.SlotsPerDay || slot == villager.LastSlot)
            {
                return;
            }

            villager.LastSlot = slot;

            float startTime = villager.Profile.StartOffsetSeconds + slot * slotDuration;
            float endTime = Mathf.Min(startTime + slotDuration, villager.HomeReturnTime);
            float remaining = endTime - now;

            if (remaining < _behaviourConfig.MinRemainingSecondsToRun)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Slot {slot} skipped (remaining {remaining:F1}s < {_behaviourConfig.MinRemainingSecondsToRun}s)");
                return;
            }

            int serial = villager.NextSerial();
            if (_resolver.TryResolve(villager.Profile, _timeSource.DayCount, slot, serial, startTime, endTime, out ResolvedRoutine resolved))
            {
                villager.Current = resolved;
                villager.Core.ApplyRoutine(resolved);
                RecordMemory(villager, resolved);
            }
        }

        private void OrderGoHome(VillagerRuntime villager)
        {
            villager.IsHomeOrdered = true;
            float distance = Vector3.Distance(villager.Core.transform.position, villager.Profile.HomePosition);
            AILog.Log(AILog.STUB, villager.Id, $"ToolTidy zone skipped (distance={distance:F1}m)");
            AILog.Log(AILog.MANAGER, villager.Id, $"GoHome ordered at {_timeSource.PhaseElapsed:F1}s");
            villager.Core.SetMustGoHome(true);
        }

        private float CalculateHomeReturnTime(VillagerRuntime villager, int dayCount)
        {
            float travelDistance = 0f;
            VillagerSchedule schedule = villager.Profile.Schedule;
            RoutineEntry lastRoutine = schedule != null ? schedule.Get(dayCount, _timeSource.SlotsPerDay - 1) : null;

            if (lastRoutine != null && PlaceRegistry.TryGet(lastRoutine.PlaceId, out PlaceArea place))
            {
                travelDistance = Vector3.Distance(place.Center, villager.Profile.HomePosition);
            }

            float lead = _behaviourConfig.ToolTidySecondsEstimate + travelDistance / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate) + _behaviourConfig.HomeReturnMarginSeconds;
            return Mathf.Max(0f, _timeSource.DayDuration - lead);
        }

        private void RecordMemory(VillagerRuntime villager, ResolvedRoutine resolved)
        {
            AILog.Log(AILog.STUB, villager.Id, $"RecordMemory skipped (memory branch) #{resolved.Serial}");
        }

        public class VillagerRuntime
        {
            private int _serial;

            public AI_Core Core { get; }
            public VillagerProfile Profile => Core.Profile;
            public string Id => Core.LogId;
            public int LastSlot { get; set; } = -1;
            public bool IsHomeOrdered { get; set; }
            public float HomeReturnTime { get; private set; }
            public ResolvedRoutine Current { get; set; }

            public VillagerRuntime(AI_Core core)
            {
                Core = core;
            }

            public int NextSerial()
            {
                _serial++;
                return _serial;
            }

            public void ResetForNewDay(float homeReturnTime)
            {
                LastSlot = -1;
                IsHomeOrdered = false;
                HomeReturnTime = homeReturnTime;
            }
        }
    }
}
