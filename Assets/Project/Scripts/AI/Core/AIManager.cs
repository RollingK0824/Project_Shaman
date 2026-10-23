using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Observation;
using ProjectShaman.AI.Perception;
using ProjectShaman.AI.Request;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.Stress;
using ProjectShaman.AI.World;
using ProjectShaman.AI.Work;

namespace ProjectShaman.AI.Core
{
    public class AIManager : SceneSingleton<AIManager>
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;

        private const float STATION_RETRY_INTERVAL = 0.5f;

        private static NavMeshPath _pathBuffer;

        private readonly List<VillagerRuntime> _villagers = new List<VillagerRuntime>();
        private TimeManager _time;
        private RoutineResolver _resolver;
        private ConversationService _conversations;
        private VillagerRequestRouter _requests;
        private ObservationService _observation;
        private AbsenceTracker _absence;
        private readonly List<VillagerProfile> _profiles = new List<VillagerProfile>();
        private readonly Dictionary<string, int> _visitedDay = new Dictionary<string, int>();

        private bool _isRunning;
        private bool _isDayActive;

        internal IReadOnlyList<VillagerRuntime> Villagers => _villagers;
        public VillagerRegistry Registry => _factory != null ? _factory.Registry : null;
        public AIBehaviourConfig Config => _behaviourConfig;

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

            _requests?.Stop();
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

                UnsubscribeDeath(villager);
            }

            _isRunning = false;
            _isDayActive = false;
            UnsubscribeTime();
            _time = null;

            AILog.Log(AILog.MANAGER, $"Stopped and cleared {_villagers.Count} villagers");
            _villagers.Clear();
            _resolver = null;
            _conversations?.Clear();
            _conversations = null;
            _requests?.Stop();
            _requests = null;
            _observation?.Clear();
            _observation = null;
            _absence?.Clear();
            _absence = null;
            _profiles.Clear();
            _visitedDay.Clear();
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
            _conversations = new ConversationService(context, _factory.Seed + 2, new Dialogue.FactConversationContent(_behaviourConfig, _factory.Seed + 5));

            foreach (AI_Core core in cores)
            {
                if (core != null && core.Profile != null)
                {
                    VillagerRuntime villager = new VillagerRuntime(core);
                    _profiles.Add(core.Profile);
                    villager.RoutineFailedHandler = reason => SkipRoutine(villager, reason);
                    core.OnRoutineFailed += villager.RoutineFailedHandler;
                    SubscribeDeath(villager);
                    AttachStress(villager);

                    foreach (IAIConfigurable configurable in core.GetComponents<IAIConfigurable>())
                    {
                        configurable.Configure(context);
                    }

                    _villagers.Add(villager);
                }
            }

            StartRequests();
            _observation = IsAuthority() ? new ObservationService(_behaviourConfig, _factory.Seed + 4) : null;
            _absence = IsAuthority() ? new AbsenceTracker(_factory.Registry) : null;

            AILog.Log(AILog.MANAGER, $"Registered {_villagers.Count} villagers, places={PlaceRegistry.Count}, day={TimeManager.DAY_DURATION}s night={TimeManager.NIGHT_DURATION}s");

            UnsubscribeTime();
            _time.OnNewDay += HandleNewDay;
            _time.OnDayStart += HandleDayStart;
            _time.OnNightStart += HandleNightStart;
            _isRunning = true;

            CatchUpStartedDay();
        }

        // Villagers created after the day began missed its NewDay/DayStart: plan only the hours still ahead
        private void CatchUpStartedDay()
        {
            if (_time.DayCount <= 0)
            {
                return;
            }

            if (_time.CurrentTimePhase != TimeOfDay.Day)
            {
                AILog.Warn(AILog.TIME_TO_AI, $"Villagers created during night {_time.DayCount}, waiting for next day");
                return;
            }

            float now = GetDayElapsed();
            AILog.Log(AILog.TIME_TO_AI, $"Day {_time.DayCount} already started before villagers were created, planning from {DayClock.ToHour(now):F1}h ({now:F1}s)");
            PlanDay(_time.DayCount, now);
            HandleDayStart();
        }

        // Requests come from villager triggers and are judged on the server only
        private void StartRequests()
        {
            _requests?.Stop();
            _requests = null;

            if (!IsAuthority())
            {
                return;
            }

            List<AI_Stress> stresses = new List<AI_Stress>();
            foreach (VillagerRuntime villager in _villagers)
            {
                if (villager.Stress != null)
                {
                    stresses.Add(villager.Stress);
                }
            }

            _requests = new VillagerRequestRouter(_behaviourConfig, stresses);
            _requests.Start();
        }

        // Server: the village learns of a death. The victim's neighbours (or, without one, the villager nearest to where
        // it came to light) remember it; it leaves vote lists and is never "missing" again.
        public void RevealDeath(string victimId, Vector3 where, string reason)
        {
            VillagerRegistry registry = Registry;
            if (registry == null || !registry.TryGet(victimId, out IVillagerView view) || view.IsAlive || view.IsDeathKnown)
            {
                return;
            }

            registry.MarkDeathKnown(victimId);

            string neighborhood = NeighborhoodOf(victimId);
            List<VillagerRuntime> learners = new List<VillagerRuntime>();
            VillagerRuntime nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (VillagerRuntime villager in _villagers)
            {
                if (villager.Core == null || villager.Memory == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(neighborhood) && villager.Profile.NeighborhoodId == neighborhood)
                {
                    learners.Add(villager);
                }

                float sqr = (villager.Core.transform.position - where).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = villager;
                }
            }

            if (learners.Count == 0 && nearest != null)
            {
                learners.Add(nearest);
            }

            string areaId = ProjectShaman.Map.MapAreaRegistry.ResolveId(where);
            foreach (VillagerRuntime learner in learners)
            {
                learner.Memory.RecordFact(new MemoryEntry
                {
                    Kind = MemoryKind.Death,
                    Source = MemorySource.Direct,
                    SubjectId = victimId,
                    ActorId = victimId,
                    VerbId = "died",
                    AreaId = areaId,
                    PlaceId = areaId,
                    Position = where,
                    TraitMask = 7,
                    IsAnomalous = true,
                    DayCount = learner.Memory.CurrentDay
                });
            }

            AILog.Log(AILog.MANAGER, victimId, $"Death revealed ({reason}), {learners.Count} villager(s) remember it");
            AIEvents.RaiseDeathKnown(victimId);
        }

        // Server: a won exorcism frees the host. With no possession the ghost has no entity, hunt, corruption or symptoms from now on.
        public void Exorcise(string villagerId)
        {
            VillagerRuntime villager = _villagers.Find(v => v.Id == villagerId);
            AI_Possession possession = villager != null ? villager.Profile.Possession : null;
            if (possession == null)
            {
                AILog.Warn(AILog.DEFENSE, $"{villagerId} exorcise requested but not possessed, ignored");
                return;
            }

            string ghostId = possession.GhostId;
            villager.Profile.Possession = null;
            Destroy(possession);
            AILog.Log(AILog.DEFENSE, villagerId, $"Exorcised {ghostId}, now an ordinary villager");
        }

        public bool TryGetHomePosition(string villagerId, out Vector3 home)
        {
            foreach (VillagerProfile profile in _profiles)
            {
                if (profile.VillagerId == villagerId)
                {
                    home = profile.HomePosition;
                    return true;
                }
            }

            home = Vector3.zero;
            return false;
        }

        public string NeighborhoodOf(string villagerId)
        {
            foreach (VillagerProfile profile in _profiles)
            {
                if (profile.VillagerId == villagerId)
                {
                    return profile.NeighborhoodId;
                }
            }

            return null;
        }

        private static bool IsAuthority()
        {
            return !NetworkClient.active || NetworkServer.active;
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
            PlanDay(dayCount, 0f);
        }

        // Blocks already over at fromTime are dropped; the one in progress stays and TickVillager judges what is left of it
        private void PlanDay(int dayCount, float fromTime)
        {
            foreach (VillagerRuntime villager in _villagers)
            {
                float homeReturnTime = CalculateHomeReturnTime(villager, dayCount);
                List<DailyBlock> blocks = DailyBlockPlanner.Build(GetBlockSeed(villager, dayCount), _behaviourConfig,
                    CalculateTravelSeconds(villager, dayCount), DayClock.ToHour(homeReturnTime), out string blockNote);
                blocks.RemoveAll(block => block.EndTime <= fromTime);
                villager.SetToday(blocks, homeReturnTime);
                villager.Core.SetDailyStartOffset(blocks.Count > 0 ? blocks[0].StartTime : 0f);
                villager.Core.SetTodayBlocks(dayCount, DailyBlock.ToHourPairs(blocks));
                PlanCorruption(villager, dayCount);

                if (villager.Memory != null)
                {
                    villager.Memory.BeginDay(dayCount);
                }

                if (villager.Stress != null)
                {
                    villager.Stress.ApplyMorningDecay();
                }
                AILog.Log(AILog.MANAGER, villager.Id, $"Day {dayCount} blocks=[{string.Join(", ", blocks)}] {blockNote} homeReturn={DayClock.ToHour(villager.HomeReturnTime):F1}h ({villager.HomeReturnTime:F1}s)");
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
            _absence?.OnDayStart(_villagers, _time.DayCount);
            _requests?.OnDayStart();
        }

        private void HandleNightStart()
        {
            AILog.Log(AILog.TIME_TO_AI, $"NightStart Day {_time.DayCount}");
            _isDayActive = false;
            LogVillagersOutside();
            _conversations?.EndAll("night");
            _absence?.OnNightStart(_villagers, _time.DayCount);

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

            _conversations?.Tick(_villagers, now);
            _observation?.Tick(_villagers);
        }

        private void TickVillager(VillagerRuntime villager, float now)
        {
            if (villager.IsHomeOrdered)
            {
                LogHomeArrival(villager, now);
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
                _observation?.OnRoutineApplied(villager.Core, villager.Interaction, resolved, endTime - now);
                RecordMemory(villager, resolved);
            }
            else
            {
                SkipRoutine(villager, $"block {block.Index} resolve failed");
            }
        }

        // Validation only: "home" is the same radius sleep paralysis uses to count a villager as in the house
        private void LogHomeArrival(VillagerRuntime villager, float now)
        {
            if (villager.HasArrivedHome || !IsAtHome(villager))
            {
                return;
            }

            villager.HasArrivedHome = true;
            AILog.Log(AILog.MANAGER, villager.Id, $"Arrived home at {DayClock.ToHour(now):F1}h ({now:F1}s), {TimeManager.DAY_DURATION - now:F0}s before night");
        }

        private void LogVillagersOutside()
        {
            List<string> outside = new List<string>();

            foreach (VillagerRuntime villager in _villagers)
            {
                if (villager.Core != null && !IsAtHome(villager))
                {
                    float distance = Vector3.Distance(villager.Core.transform.position, villager.Profile.HomePosition);
                    outside.Add($"{villager.Id}({distance:F1}m{(villager.IsHomeOrdered ? "" : ", not ordered")})");
                }
            }

            if (outside.Count > 0)
            {
                AILog.Warn(AILog.MANAGER, $"Night start: {outside.Count} villager(s) still outside: {string.Join(", ", outside)}");
            }
            else
            {
                AILog.Log(AILog.MANAGER, $"Night start: all {_villagers.Count} villagers home");
            }
        }

        private bool IsAtHome(VillagerRuntime villager)
        {
            Vector3 offset = villager.Core.transform.position - villager.Profile.HomePosition;
            offset.y = 0f;
            float radius = _behaviourConfig.SleepParalysisHomeDistance;
            return offset.sqrMagnitude <= radius * radius;
        }

        private void PlanCorruption(VillagerRuntime villager, int dayCount)
        {
            AI_Possession possession = villager.Profile.Possession;
            if (possession == null)
            {
                return;
            }

            possession.ApplyMorningYin(_behaviourConfig.MinimumYin, _behaviourConfig.MaxYin);
            CorruptionPlan plan = CorruptionPlanner.Build(possession, villager.Profile.Schedule, dayCount, _behaviourConfig.RoutinesPerDay, _behaviourConfig);
            possession.SetPlan(plan);
            AILog.Log(AILog.GHOST, villager.Id, $"Corruption plan Day{dayCount}: yin={plan.MorningYin:F0}, corrupted slots={plan.CorruptedSlotCount}, symptoms={plan.Symptoms.Count}");
        }

        private void TickSymptoms(VillagerRuntime villager, float now)
        {
            AI_Possession possession = villager.Profile.Possession;
            ResolvedRoutine current = villager.Current;

            if (possession == null || possession.TodayPlan == null || villager.IsHomeOrdered || (current.Category != RoutineCategory.Rest && current.Category != RoutineCategory.Work))
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
                _observation?.OnSymptom(villager.Core, symptom.Type);
                possession.RefreshDebug();
            }
        }

        // A symptom forced out of a host outside today's plan (struck by salt ...): shown and witnessed like a planned one
        public void ShowSymptom(AI_Core core, SymptomType symptom)
        {
            if (core == null)
            {
                return;
            }

            core.TriggerSymptom(symptom);
            _observation?.OnSymptom(core, symptom);
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
            UnsubscribeDeath(villager);
            _villagers.Remove(villager);

            AILog.Log(AILog.MANAGER, villager.Id, $"Removed ({reason}), remaining={_villagers.Count}");
            _observation?.OnVillagerRemoved(villager.Id);
            _factory.Registry?.MarkDead(villager.Id);
            AIEvents.RaiseVillagerDied(villager.Id, reason);

            if (_factory.SpawnHandler != null)
            {
                _factory.SpawnHandler.Despawn(core.gameObject);
            }
        }

        private void SubscribeDeath(VillagerRuntime villager)
        {
            if (!villager.Core.TryGetComponent(out AI_Status status))
            {
                return;
            }

            villager.Status = status;
            villager.DiedHandler = (_, source) => RemoveVillager(villager.Core, $"died by {(source != null ? source.name : "unknown")}");
            status.OnDied += villager.DiedHandler;
        }

        private void AttachStress(VillagerRuntime villager)
        {
            if (!villager.Core.TryGetComponent(out AI_Stress stress))
            {
                stress = villager.Core.gameObject.AddComponent<AI_Stress>();
            }

            villager.Stress = stress;
        }

        private void UnsubscribeDeath(VillagerRuntime villager)
        {
            if (villager.Status != null)
            {
                villager.Status.OnDied -= villager.DiedHandler;
            }
        }

        private void TryAssignWaitingStation(VillagerRuntime villager)
        {
            ResolvedRoutine current = villager.Current;
            if (!current.IsWaitingForStation || villager.IsHomeOrdered || current.Source == null || Time.time < villager.NextStationRetryTime
                || (villager.Conversation != null && villager.Conversation.IsInSession))
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
            TryLookInOnNeighbour(villager);
        }

        // On the way home: a neighbour (same home-point group) this villager has not seen all day
        // gets a look-in. Inside the house the normal trace scan sees what is there (a body included).
        private void TryLookInOnNeighbour(VillagerRuntime villager)
        {
            if (_observation == null || !_isDayActive || villager.Memory == null || villager.Interaction == null
                || string.IsNullOrEmpty(villager.Profile.NeighborhoodId)
                || (_visitedDay.TryGetValue(villager.Id, out int day) && day == _time.DayCount))
            {
                return;
            }

            VillagerProfile target = null;
            float best = float.MaxValue;

            foreach (VillagerProfile neighbour in _profiles)
            {
                if (neighbour == villager.Profile || neighbour.NeighborhoodId != villager.Profile.NeighborhoodId || villager.Memory.HasSeenToday(neighbour.VillagerId))
                {
                    continue;
                }

                float distance = (neighbour.HomePosition - villager.Profile.HomePosition).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = neighbour;
                }
            }

            if (target == null)
            {
                return;
            }

            if (!PlaceRegistry.TryGet(target.PublicInfo.HomePlaceId, out PlaceArea house))
            {
                return;
            }

            // Only if the detour still ends at home before dark (delays during the day eat the budget)
            float speed = Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            Vector3 housePoint = house.GetHomePoint();
            float needed = (PathLength(villager.Core.transform.position, housePoint) + PathLength(housePoint, villager.Profile.HomePosition)) / speed
                + _behaviourConfig.NeighbourVisitLingerSeconds + _behaviourConfig.HomeReturnMarginSeconds;
            float daylight = TimeManager.DAY_DURATION - GetDayElapsed();
            if (needed > daylight)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Skip look-in on {target.VillagerId}: needs {needed:F0}s, {daylight:F0}s of daylight left");
                return;
            }

            if (villager.Interaction.TryBegin(house, InteractionResponse.Approach, _behaviourConfig.NeighbourVisitLingerSeconds))
            {
                _visitedDay[villager.Id] = _time.DayCount;
                AILog.Log(AILog.MANAGER, villager.Id, $"Looking in on {target.VillagerId} on the way home (not seen today)");
            }
        }

        private int GetBlockSeed(VillagerRuntime villager, int dayCount)
        {
            return unchecked(_factory.Seed * 486187739 + villager.Profile.Index * 16777619 + dayCount * 31);
        }

        private List<float> CalculateTravelSeconds(VillagerRuntime villager, int dayCount)
        {
            List<float> result = new List<float>(_behaviourConfig.RoutinesPerDay);
            VillagerSchedule schedule = villager.Profile.Schedule;
            float speed = Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            Vector3 from = villager.Profile.HomePosition;

            for (int slot = 0; slot < _behaviourConfig.RoutinesPerDay; slot++)
            {
                RoutineEntry routine = schedule != null ? schedule.Get(dayCount, slot) : null;
                if (routine == null || !PlaceRegistry.TryGet(routine.PlaceId, out PlaceArea place))
                {
                    result.Add(0f);
                    continue;
                }

                result.Add(PathLength(from, place.Center) / speed);
                from = place.Center;
            }

            return result;
        }

        private float CalculateHomeReturnTime(VillagerRuntime villager, int dayCount)
        {
            float travelDistance = 0f;
            float visitSeconds = 0f;
            VillagerSchedule schedule = villager.Profile.Schedule;
            RoutineEntry lastRoutine = schedule != null ? schedule.Get(dayCount, _behaviourConfig.RoutinesPerDay - 1) : null;

            if (lastRoutine != null && PlaceRegistry.TryGet(lastRoutine.PlaceId, out PlaceArea place))
            {
                Vector3 home = villager.Profile.HomePosition;
                travelDistance = PathLength(place.Center, home);

                // Room for the look-in on the way: TryLookInOnNeighbour picks the unseen neighbour nearest home, so plan the
                // detour through the nearest one. A farther pick that no longer fits is skipped there against the daylight left
                VillagerProfile nearest = null;
                float nearestSqr = float.MaxValue;
                foreach (VillagerProfile neighbour in _profiles)
                {
                    float sqr = (neighbour.HomePosition - home).sqrMagnitude;
                    if (neighbour != villager.Profile && neighbour.NeighborhoodId == villager.Profile.NeighborhoodId && sqr < nearestSqr)
                    {
                        nearest = neighbour;
                        nearestSqr = sqr;
                    }
                }

                float detour = nearest != null ? PathLength(place.Center, nearest.HomePosition) + PathLength(nearest.HomePosition, home) - travelDistance : 0f;
                if (detour > 0f)
                {
                    visitSeconds = detour / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate) + _behaviourConfig.NeighbourVisitLingerSeconds;
                }
            }

            float lead = _behaviourConfig.ToolTidySecondsEstimate + travelDistance / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate) + visitSeconds + _behaviourConfig.HomeReturnMarginSeconds;
            return Mathf.Max(0f, TimeManager.DAY_DURATION - lead);
        }

        // Walking distance along the NavMesh (roads bend), straight line when no path is found
        private static float PathLength(Vector3 from, Vector3 to)
        {
            if (_pathBuffer == null)
            {
                _pathBuffer = new NavMeshPath();
            }

            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _pathBuffer) || _pathBuffer.status == NavMeshPathStatus.PathInvalid)
            {
                return Vector3.Distance(from, to);
            }

            Vector3[] corners = _pathBuffer.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private void RecordMemory(VillagerRuntime villager, ResolvedRoutine resolved)
        {
            if (villager.Memory != null)
            {
                villager.Memory.RecordRoutine(resolved);
            }
        }

        public class VillagerRuntime
        {
            private int _serial;
            private List<DailyBlock> _todayBlocks = new List<DailyBlock>();

            public AI_Core Core { get; }
            public AI_ToolHandler ToolHandler { get; }
            public AI_Memory Memory { get; }
            public AI_Perception Perception { get; }
            public AI_Interaction Interaction { get; }
            public AI_Conversation Conversation { get; }
            public System.Action<string> RoutineFailedHandler { get; set; }
            public AI_Status Status { get; set; }
            public AI_Stress Stress { get; set; }
            public System.Action<AI_Status, GameObject> DiedHandler { get; set; }
            public VillagerProfile Profile => Core.Profile;
            public string Id { get; }
            public int LastBlockIndex { get; set; } = -1;
            public bool IsHomeOrdered { get; set; }
            public bool HasArrivedHome { get; set; }
            public float HomeReturnTime { get; private set; }
            public IReadOnlyList<DailyBlock> TodayBlocks => _todayBlocks;
            public float NextStationRetryTime { get; set; }
            public ResolvedRoutine Current { get; set; }

            public VillagerRuntime(AI_Core core)
            {
                Core = core;
                Id = core.LogId;
                ToolHandler = core.GetComponent<AI_ToolHandler>();
                Memory = core.GetComponent<AI_Memory>();
                Perception = core.GetComponent<AI_Perception>();
                Interaction = core.GetComponent<AI_Interaction>();
                Conversation = core.GetComponent<AI_Conversation>();
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
                HasArrivedHome = false;
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
