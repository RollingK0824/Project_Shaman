using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;
using ProjectShaman.AI.Work;

namespace ProjectShaman.AI.Core
{
    public class AIManager : SceneSingleton<AIManager>
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;

        private const float STATION_RETRY_INTERVAL = 0.5f;

        private readonly List<VillagerRuntime> _villagers = new List<VillagerRuntime>();
        private TimeManager _time;
        private RoutineResolver _resolver;
        private bool _isRunning;
        private bool _isDayActive;

        internal IReadOnlyList<VillagerRuntime> Villagers => _villagers;
        public VillagerRegistry Registry => _factory != null ? _factory.Registry : null;

        protected override void Awake()
        {
            base.Awake();

            if (_factory != null)
            {
                _factory.OnVillagersCreated += HandleVillagersCreated;
                _factory.OnVillagersCleared += HandleVillagersCleared;
            }
        }

        protected override void OnDestroy()
        {
            if (_factory != null)
            {
                _factory.OnVillagersCreated -= HandleVillagersCreated;
                _factory.OnVillagersCleared -= HandleVillagersCleared;
            }

            UnsubscribeTime();
            base.OnDestroy();
        }

        private void UnsubscribeTime()
        {
            if (_time != null)
            {
                _time.OnNewDay -= HandleNewDay;
                _time.OnDayStart -= HandleDayStart;
                _time.OnNightStart -= HandleNightStart;
            }
        }

        private void HandleVillagersCleared()
        {
            foreach (VillagerRuntime villager in _villagers)
            {
                ReleaseStation(villager);

                if (villager.Core != null)
                {
                    villager.Core.OnRoutineFailed -= villager.RoutineFailedHandler;
                }
            }

            _isRunning = false;
            _isDayActive = false;
            UnsubscribeTime();
            _time = null;

            AILog.Log(AILog.MANAGER, $"Stopped and cleared {_villagers.Count} villagers");
            _villagers.Clear();
            _resolver = null;
        }

        private void HandleVillagersCreated(IReadOnlyList<AI_Core> cores)
        {
            _time = TimeManager.Instance;

            if (_time == null || _behaviourConfig == null)
            {
                AILog.Error(AILog.MANAGER, "TimeManager or behaviour config missing, AIManager not started");
                return;
            }

            _resolver = new RoutineResolver(new System.Random(_factory.Seed + 1), _behaviourConfig);
            AIRuntimeContext context = new AIRuntimeContext(_behaviourConfig, GetGameClock, BuildToolUsePlaces());

            foreach (AI_Core core in cores)
            {
                if (core != null && core.Profile != null)
                {
                    VillagerRuntime villager = new VillagerRuntime(core);
                    villager.RoutineFailedHandler = reason => SkipRoutine(villager, reason);
                    core.OnRoutineFailed += villager.RoutineFailedHandler;

                    foreach (IAIConfigurable configurable in core.GetComponents<IAIConfigurable>())
                    {
                        configurable.Configure(context);
                    }

                    _villagers.Add(villager);
                }
            }

            AILog.Log(AILog.MANAGER, $"Registered {_villagers.Count} villagers, places={PlaceRegistry.Count}, day={TimeManager.DAY_DURATION}s night={TimeManager.NIGHT_DURATION}s");

            if (_time.DayCount > 0)
            {
                AILog.Error(AILog.TIME_TO_AI, $"Day {_time.DayCount} already started before villagers were created, waiting for next day");
            }

            UnsubscribeTime();
            _time.OnNewDay += HandleNewDay;
            _time.OnDayStart += HandleDayStart;
            _time.OnNightStart += HandleNightStart;
            _isRunning = true;
        }

        private float GetGameClock()
        {
            return _time != null ? (float)_time.Elapsed : Time.time;
        }

        private float GetDayElapsed()
        {
            return (float)(_time.Elapsed % TimeManager.CYCLE_DURATION);
        }

        private Dictionary<string, List<string>> BuildToolUsePlaces()
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

            if (_factory.DataProvider == null)
            {
                return result;
            }

            foreach (RoutineEntry routine in _factory.DataProvider.Routines)
            {
                if (routine == null || string.IsNullOrEmpty(routine.ToolId) || string.IsNullOrEmpty(routine.PlaceId))
                {
                    continue;
                }

                if (!result.TryGetValue(routine.ToolId, out List<string> places))
                {
                    places = new List<string>();
                    result[routine.ToolId] = places;
                }

                if (!places.Contains(routine.PlaceId))
                {
                    places.Add(routine.PlaceId);
                }
            }

            return result;
        }

        private void HandleNewDay(int dayCount)
        {
            AILog.Log(AILog.TIME_TO_AI, $"NewDay {dayCount} (cycleDay={(dayCount - 1) % _behaviourConfig.CycleDays})");

            foreach (VillagerRuntime villager in _villagers)
            {
                List<DailyBlock> blocks = DailyBlockPlanner.Build(GetBlockSeed(villager, dayCount), _behaviourConfig);
                villager.SetToday(blocks, CalculateHomeReturnTime(villager, dayCount));
                villager.Core.SetDailyStartOffset(blocks.Count > 0 ? blocks[0].StartTime : 0f);
                PlanCorruption(villager, dayCount);
                AILog.Log(AILog.MANAGER, villager.Id, $"Day {dayCount} blocks=[{string.Join(", ", blocks)}] homeReturn={DayClock.ToHour(villager.HomeReturnTime):F1}h ({villager.HomeReturnTime:F1}s)");
            }
        }

        private void HandleDayStart()
        {
            AILog.Log(AILog.TIME_TO_AI, $"DayStart Day {_time.DayCount}");

            foreach (VillagerRuntime villager in _villagers)
            {
                ReleaseStation(villager);
                villager.ResetForDayStart();
                villager.Core.ClearRoutine(villager.NextSerial(), "day start");
                villager.Core.SetMustGoHome(false);
            }

            _isDayActive = true;
        }

        private void HandleNightStart()
        {
            AILog.Log(AILog.TIME_TO_AI, $"NightStart Day {_time.DayCount}");
            _isDayActive = false;

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
            if (!_isRunning || !_isDayActive)
            {
                return;
            }

            float now = GetDayElapsed();

            foreach (VillagerRuntime villager in _villagers)
            {
                if (villager.Core == null)
                {
                    continue;
                }

                TickVillager(villager, now);
                TryAssignWaitingStation(villager);
                TickSymptoms(villager, now);
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

            if (!villager.TryGetBlockAt(now, out DailyBlock block) || block.Index == villager.LastBlockIndex)
            {
                return;
            }

            villager.LastBlockIndex = block.Index;

            float startTime = block.StartTime;
            float endTime = Mathf.Min(block.EndTime, villager.HomeReturnTime);
            float remaining = endTime - now;

            float minRemaining = _behaviourConfig.MinRemainingRatio * block.Length;
            if (remaining < minRemaining)
            {
                SkipRoutine(villager, $"block {block.Index} remaining {remaining:F1}s < {minRemaining:F1}s");
                return;
            }

            WorkStation previousStation = villager.Current.Station;

            int serial = villager.NextSerial();
            if (_resolver.TryResolve(villager.Profile, _time.DayCount, block.Index, serial, startTime, endTime, previousStation, out ResolvedRoutine resolved))
            {
                if (previousStation != null && previousStation != resolved.Station)
                {
                    previousStation.Release(villager.Id);
                }

                villager.Current = resolved;
                villager.Core.ApplyRoutine(resolved);
                RecordMemory(villager, resolved);
            }
            else
            {
                SkipRoutine(villager, $"block {block.Index} resolve failed");
            }
        }

        private void PlanCorruption(VillagerRuntime villager, int dayCount)
        {
            AI_Possession possession = villager.Profile.Possession;
            if (possession == null)
            {
                return;
            }

            possession.ApplyMorningYin(_behaviourConfig.MinimumYin);
            CorruptionPlan plan = CorruptionPlanner.Build(possession, villager.Profile.Schedule, dayCount, _behaviourConfig.RoutinesPerDay, _behaviourConfig);
            possession.SetPlan(plan);
            AILog.Log(AILog.GHOST, villager.Id, $"Corruption plan Day{dayCount}: yin={plan.MorningYin:F0}, corrupted slots={plan.CorruptedSlotCount}, symptoms={plan.Symptoms.Count}");
        }

        private void TickSymptoms(VillagerRuntime villager, float now)
        {
            AI_Possession possession = villager.Profile.Possession;
            ResolvedRoutine current = villager.Current;

            if (possession == null || possession.TodayPlan == null || villager.IsHomeOrdered || current.Category != RoutineCategory.Rest)
            {
                return;
            }

            float length = Mathf.Max(1f, current.EndTime - current.StartTime);
            float fraction = (now - current.StartTime) / length;
            List<PlannedSymptom> symptoms = possession.TodayPlan.Symptoms;

            for (int i = 0; i < symptoms.Count; i++)
            {
                PlannedSymptom symptom = symptoms[i];
                if (symptom.HasFired || symptom.Slot != current.Slot || fraction < symptom.SlotFraction)
                {
                    continue;
                }

                symptom.HasFired = true;
                symptoms[i] = symptom;
                villager.Core.TriggerSymptom(symptom.Type);
                possession.RefreshDebug();
            }
        }

        internal void RemoveVillager(AI_Core core, string reason)
        {
            VillagerRuntime villager = _villagers.Find(v => v.Core == core);
            if (villager == null)
            {
                return;
            }

            ReleaseStation(villager);
            core.OnRoutineFailed -= villager.RoutineFailedHandler;
            _villagers.Remove(villager);

            AILog.Log(AILog.MANAGER, villager.Id, $"Removed ({reason}), remaining={_villagers.Count}");
            _factory.Registry?.MarkDead(villager.Id);
            AIEvents.RaiseVillagerDied(villager.Id, reason);

            if (_factory.SpawnHandler != null)
            {
                _factory.SpawnHandler.Despawn(core.gameObject);
            }
        }

        private void TryAssignWaitingStation(VillagerRuntime villager)
        {
            ResolvedRoutine current = villager.Current;
            if (!current.IsWaitingForStation || villager.IsHomeOrdered || current.Source == null || Time.time < villager.NextStationRetryTime)
            {
                return;
            }

            villager.NextStationRetryTime = Time.time + STATION_RETRY_INTERVAL;

            if (!PlaceRegistry.TryGet(current.Source.PlaceId, out PlaceArea place) || !place.TryReserveStation(villager.Id, current.ActionId, out WorkStation station))
            {
                return;
            }

            current.Station = station;
            current.TargetPosition = station.StandPosition;
            current.IsWaitingForStation = false;
            current.Serial = villager.NextSerial();
            villager.Current = current;

            AILog.Log(AILog.WORK, villager.Id, $"Station {station.StationId} freed, moving to it");
            villager.Core.ApplyRoutine(current);
        }

        private void SkipRoutine(VillagerRuntime villager, string reason)
        {
            ReleaseStation(villager);
            villager.Current = default;
            villager.Core.ClearRoutine(villager.NextSerial(), $"skipped: {reason}");
            AILog.Log(AILog.MANAGER, villager.Id, $"Routine skipped ({reason})");
        }

        private void ReleaseStation(VillagerRuntime villager)
        {
            ResolvedRoutine current = villager.Current;
            if (current.Station != null)
            {
                current.Station.Release(villager.Id);
                current.Station = null;
                villager.Current = current;
            }
        }

        private void OrderGoHome(VillagerRuntime villager)
        {
            villager.IsHomeOrdered = true;
            ReleaseStation(villager);
            float distance = Vector3.Distance(villager.Core.transform.position, villager.Profile.HomePosition);
            ToolTidyZone zone = ToolTidyPolicy.DecideZone(distance, _behaviourConfig);

            if (villager.ToolHandler != null)
            {
                villager.ToolHandler.SetTidyZone(zone);
            }

            AILog.Log(AILog.MANAGER, villager.Id, $"Tidy zone {zone} (distance to home {distance:F1}m)");
            AILog.Log(AILog.MANAGER, villager.Id, $"GoHome ordered at {DayClock.ToHour(GetDayElapsed()):F1}h ({GetDayElapsed():F1}s)");
            villager.Core.SetMustGoHome(true);
        }

        private int GetBlockSeed(VillagerRuntime villager, int dayCount)
        {
            return unchecked(_factory.Seed * 486187739 + villager.Profile.Index * 16777619 + dayCount * 31);
        }

        private float CalculateHomeReturnTime(VillagerRuntime villager, int dayCount)
        {
            float travelDistance = 0f;
            VillagerSchedule schedule = villager.Profile.Schedule;
            RoutineEntry lastRoutine = schedule != null ? schedule.Get(dayCount, _behaviourConfig.RoutinesPerDay - 1) : null;

            if (lastRoutine != null && PlaceRegistry.TryGet(lastRoutine.PlaceId, out PlaceArea place))
            {
                travelDistance = Vector3.Distance(place.Center, villager.Profile.HomePosition);
            }

            float lead = _behaviourConfig.ToolTidySecondsEstimate + travelDistance / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate) + _behaviourConfig.HomeReturnMarginSeconds;
            return Mathf.Max(0f, TimeManager.DAY_DURATION - lead);
        }

        private void RecordMemory(VillagerRuntime villager, ResolvedRoutine resolved)
        {
            AILog.Log(AILog.STUB, villager.Id, $"RecordMemory skipped (memory branch) #{resolved.Serial}");
        }

        public class VillagerRuntime
        {
            private int _serial;
            private List<DailyBlock> _todayBlocks = new List<DailyBlock>();

            public AI_Core Core { get; }
            public AI_ToolHandler ToolHandler { get; }
            public System.Action<string> RoutineFailedHandler { get; set; }
            public VillagerProfile Profile => Core.Profile;
            public string Id { get; }
            public int LastBlockIndex { get; set; } = -1;
            public bool IsHomeOrdered { get; set; }
            public float HomeReturnTime { get; private set; }
            public IReadOnlyList<DailyBlock> TodayBlocks => _todayBlocks;
            public float NextStationRetryTime { get; set; }
            public ResolvedRoutine Current { get; set; }

            public VillagerRuntime(AI_Core core)
            {
                Core = core;
                Id = core.LogId;
                ToolHandler = core.GetComponent<AI_ToolHandler>();
            }

            public int NextSerial()
            {
                _serial++;
                return _serial;
            }

            public void SetToday(List<DailyBlock> blocks, float homeReturnTime)
            {
                _todayBlocks = blocks;
                HomeReturnTime = homeReturnTime;
            }

            public void ResetForDayStart()
            {
                LastBlockIndex = -1;
                IsHomeOrdered = false;
            }

            public bool TryGetBlockAt(float time, out DailyBlock block)
            {
                foreach (DailyBlock candidate in _todayBlocks)
                {
                    if (candidate.Contains(time))
                    {
                        block = candidate;
                        return true;
                    }
                }

                block = default;
                return false;
            }
        }
    }
}
