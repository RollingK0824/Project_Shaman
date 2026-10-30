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
        private const float HOME_CHECK_INTERVAL = 1f;
        // Stall log on the way home (diagnostics only, nothing acts on it)
        private const float STALL_MOVE_DISTANCE = 0.5f;
        private const float STALL_LOG_SECONDS = 3f;
        // Within this of its routine's spot the villager is there and the block starts (C131)
        private const float ROUTINE_ARRIVE_DISTANCE = 3f;

        private static NavMeshPath _pathBuffer;

        private readonly List<VillagerRuntime> _villagers = new List<VillagerRuntime>();
        // Dead, not yet known to the village: still given a day on the schedule tab (see PlanDay)
        private readonly List<UnknownDeath> _unknownDeaths = new List<UnknownDeath>();
        private TimeManager _time;
        private RoutineResolver _resolver;
        private ConversationService _conversations;
        private VillagerRequestRouter _requests;
        private ObservationService _observation;
        private AbsenceTracker _absence;
        private readonly List<VillagerProfile> _profiles = new List<VillagerProfile>();
        private readonly Dictionary<string, int> _visitedDay = new Dictionary<string, int>();
        private readonly List<VillagerRuntime> _nightWalkers = new List<VillagerRuntime>();

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
            _unknownDeaths.Clear();
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
                    villager.RoutineFailedHandler = reason => HandleRoutineFailed(villager, reason);
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
            AILog.Log(AILog.PLACE, $"Place groups: {DescribeGroupsOrNone()}");

            // Villagers sent home together ask for long paths at once; at the default budget they stand waiting for them
            NavMesh.pathfindingIterationsPerFrame = _behaviourConfig.PathfindingIterationsPerFrame;
            AILog.Log(AILog.MANAGER, $"NavMesh pathfinding iterations per frame: {NavMesh.pathfindingIterationsPerFrame}");

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
            _unknownDeaths.RemoveAll(dead => dead.Profile.VillagerId == victimId);

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
            // The exorcism belongs to the night it was won in. A defense that ends as morning breaks is already counted
            // in the new day, so that morning is the "day after": the freed villager can thank the players right away
            int night = _time != null ? (_time.CurrentTimePhase == TimeOfDay.Day ? _time.DayCount - 1 : _time.DayCount) : 0;
            villager.Profile.ExorcisedDay = Mathf.Max(0, night);
            Destroy(possession);

            // Rumours of what the ghost did through this body die with it
            int forgotten = 0;
            foreach (VillagerRuntime other in _villagers)
            {
                if (other.Memory != null)
                {
                    forgotten += other.Memory.ForgetAnomaliesOf(villagerId);
                }
            }

            AILog.Log(AILog.DEFENSE, villagerId, $"Exorcised {ghostId}, now an ordinary villager ({forgotten} rumour(s) about it dropped)");
            Registry?.MarkExorcised(villagerId);
            AIEvents.RaiseExorcised(villagerId);
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

            // A tool pointing at a shed that is not in the scene can never be put away (scene setup, fix in the Editor)
            foreach (ToolItem tool in ToolRegistry.All)
            {
                if (tool != null && !string.IsNullOrEmpty(tool.HomeStoragePlaceId) && !PlaceRegistry.TryGet(tool.HomeStoragePlaceId, out _))
                {
                    AILog.Warn(AILog.MANAGER, $"Tool {tool.InstanceId} home storage '{tool.HomeStoragePlaceId}' not in scene, it cannot be put away");
                }
            }

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

        // The day in order (C131): wake-up, then each block laid after the one before (estimates for the schedule tab),
        // free time until its leave time, home to rest until nightfall, then bed. Joining mid-day (fromTime) the day is laid from now
        private void PlanDay(int dayCount, float fromTime)
        {
            foreach (VillagerRuntime villager in _villagers)
            {
                float wakeTime = Mathf.Max(fromTime, DailyBlockPlanner.PickWakeTime(GetBlockSeed(villager.Profile, dayCount), _behaviourConfig));
                List<DailyBlock> blocks = NewBlocks();
                EstimateBlocks(villager.Profile, dayCount, blocks, 0, wakeTime);
                villager.SetToday(blocks, wakeTime, PickHomeLeaveTime(villager.Profile, dayCount));
                EstimateHomeTimeline(villager.Profile, dayCount, blocks, villager.HomeLeaveTime, out int leaveHour, out int homeHour);
                villager.SetHomeTimeline(leaveHour, homeHour, Mathf.Max(DayClock.DAY_END_HOUR, homeHour));
                villager.Core.SetDailyStartOffset(wakeTime);
                PublishBlocks(villager, dayCount);
                PlanCorruption(villager, dayCount);

                if (villager.Memory != null)
                {
                    villager.Memory.BeginDay(dayCount);
                }

                if (villager.Stress != null)
                {
                    villager.Stress.ApplyMorningDecay();
                }
                AILog.Log(AILog.MANAGER, villager.Id, $"Day {dayCount} wake {DayClock.ToHour(wakeTime):F1}h ({wakeTime:F0}s), blocks (estimate)=[{string.Join(", ", blocks)}] " +
                    $"{_behaviourConfig.RoutineSeconds:F0}s each, leaves for home {DayClock.ToHour(villager.HomeLeaveTime):F1}h (home ~{villager.HomeHour}h), bed {villager.BedHour}h");
            }

            // A death the village does not know of yet keeps a day on the schedule tab, laid the same way. The death
            // comes out through villagers ("not seen all day"), not through an empty day
            foreach (UnknownDeath dead in _unknownDeaths)
            {
                List<DailyBlock> blocks = NewBlocks();
                EstimateBlocks(dead.Profile, dayCount, blocks, 0, DailyBlockPlanner.PickWakeTime(GetBlockSeed(dead.Profile, dayCount), _behaviourConfig));
                EstimateHomeTimeline(dead.Profile, dayCount, blocks, PickHomeLeaveTime(dead.Profile, dayCount), out int leaveHour, out int homeHour);
                PublishDayHours(dead.Profile.VillagerId, dayCount, DailyBlock.ToHourPairs(blocks), leaveHour, homeHour, Mathf.Max(DayClock.DAY_END_HOUR, homeHour));
            }
        }

        // Each villager sets off home at its own time from HomeLeaveEarliestHour over HomeLeaveSpread, so the village does
        // not all move at once
        private float PickHomeLeaveTime(VillagerProfile profile, int dayCount)
        {
            System.Random random = new System.Random(unchecked(GetBlockSeed(profile, dayCount) * 7 + 1));
            float leave = DayClock.ToDayElapsed(_behaviourConfig.HomeLeaveEarliestHour) + (float)random.NextDouble() * _behaviourConfig.HomeLeaveSpreadSeconds;
            return Mathf.Min(leave, TimeManager.DAY_DURATION);
        }

        // Schedule tab: it sets off at its leave time, or once its last routine is expected to be done if that runs later,
        // and gets in after the walk from that routine's place
        private void EstimateHomeTimeline(VillagerProfile profile, int dayCount, IReadOnlyList<DailyBlock> blocks, float leaveTime, out int leaveHour, out int homeHour)
        {
            float lastEnd = 0f;
            int lastIndex = -1;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].EndTime > blocks[i].StartTime && blocks[i].EndTime >= lastEnd)
                {
                    lastEnd = blocks[i].EndTime;
                    lastIndex = i;
                }
            }

            float leave = Mathf.Max(leaveTime, lastEnd);
            Vector3 from = lastIndex >= 0 ? RoutinePosition(profile, dayCount, lastIndex, profile.HomePosition) : profile.HomePosition;
            float arrive = leave + PathLength(from, profile.HomePosition) / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            leaveHour = Mathf.RoundToInt(DayClock.ToHour(leave));
            homeHour = Mathf.RoundToInt(DayClock.ToHour(arrive));
        }

        // The walk home really started: from now and from here, with the look-in on the way if there is one
        private void UpdateHomeTimelineOnLeave(VillagerRuntime villager, float now, float lookInSeconds)
        {
            float arrive = now + lookInSeconds + PathLength(villager.Core.transform.position, villager.Profile.HomePosition) / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            int homeHour = Mathf.RoundToInt(DayClock.ToHour(arrive));
            villager.SetHomeTimeline(Mathf.RoundToInt(DayClock.ToHour(now)), homeHour, Mathf.Max(DayClock.DAY_END_HOUR, homeHour));
            PublishBlocks(villager, _time.DayCount);
        }

        private List<DailyBlock> NewBlocks()
        {
            List<DailyBlock> blocks = new List<DailyBlock>(_behaviourConfig.RoutinesPerDay);
            for (int i = 0; i < _behaviourConfig.RoutinesPerDay; i++)
            {
                blocks.Add(default);
            }

            return blocks;
        }

        // Schedule tab estimates from block `index` on, after `time`: the tidy-up and the walk from the previous block's
        // place (home for the first) to this one's, then its length
        private void EstimateBlocks(VillagerProfile profile, int dayCount, List<DailyBlock> blocks, int index, float time)
        {
            float speed = Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            Vector3 from = index > 0 ? RoutinePosition(profile, dayCount, index - 1, profile.HomePosition) : profile.HomePosition;

            DailyBlockPlanner.Lay(blocks, index, time, _behaviourConfig.RoutineSeconds, i =>
            {
                Vector3 to = RoutinePosition(profile, dayCount, i, from);
                float lead = _behaviourConfig.ToolTidySecondsEstimate + PathLength(from, to) / speed;
                from = to;
                return lead;
            });
        }

        private static Vector3 RoutinePosition(VillagerProfile profile, int dayCount, int slot, Vector3 near)
        {
            RoutineEntry routine = profile.Schedule != null ? profile.Schedule.Get(dayCount, slot) : null;
            return routine != null && PlaceRegistry.TryPick(routine.PlaceId, near, out PlaceArea place) ? place.Center : near;
        }

        // Today's hours to the schedule tab on every peer: each morning, and again when a block really starts
        private void PublishBlocks(VillagerRuntime villager, int dayCount)
        {
            int[] blockHours = DailyBlock.ToHourPairs(villager.TodayBlocks);
            villager.Core.SetTodayBlocks(dayCount, blockHours, villager.LeaveHour, villager.HomeHour, villager.BedHour);
            PublishDayHours(villager.Id, dayCount, blockHours, villager.LeaveHour, villager.HomeHour, villager.BedHour);
        }

        private void PublishDayHours(string villagerId, int dayCount, int[] blockHours, int leaveHour, int homeHour, int bedHour)
        {
            Registry?.SetDayHours(villagerId, dayCount, blockHours, leaveHour, homeHour, bedHour);
            AIEvents.RaiseDayPlanned(villagerId, dayCount, blockHours, leaveHour, homeHour, bedHour);
        }

        private void HandleDayStart()
        {
            AILog.Log(AILog.TIME_TO_AI, $"DayStart Day {_time.DayCount}");

            foreach (VillagerRuntime villager in _villagers)
            {
                ReleaseStation(villager);
                StopRunning(villager, "day start");
                villager.Core.SetAsleep(false);
                villager.Core.SetHomeForNight(false);
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

            // Nightfall sends everyone still out home, whatever it was doing, and a look-in on a neighbour is left for
            // tomorrow: straight home (C131)
            foreach (VillagerRuntime villager in _villagers)
            {
                // Only the look-in itself: whatever else it is busy with (a player talking) is left alone
                if (villager.LookInHouseId != null && villager.Interaction != null && villager.Interaction.IsInteracting
                    && villager.Interaction.Active.InteractableId == villager.LookInHouseId)
                {
                    villager.Interaction.Cancel(InteractionEndReason.MustGoHome);
                    AILog.Log(AILog.MANAGER, villager.Id, "Night fell during a look-in, straight home");
                }

                villager.LookInHouseId = null;

                if (!villager.IsHomeOrdered)
                {
                    OrderGoHome(villager);
                }
            }
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            float now = GetDayElapsed();

            // Night: the villagers still on their way home hurry on until they are in. They still see what they pass
            // (a look-in on a neighbour finds a body this way); those already in the house see nothing
            if (!_isDayActive)
            {
                _nightWalkers.Clear();
                foreach (VillagerRuntime villager in _villagers)
                {
                    if (villager.Core != null && villager.IsHomeOrdered)
                    {
                        TickHomeward(villager, now);
                        if (!villager.HasArrivedHome)
                        {
                            _nightWalkers.Add(villager);
                        }
                    }
                }

                if (_nightWalkers.Count > 0)
                {
                    _observation?.Tick(_nightWalkers);
                }

                return;
            }

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
                TickHomeward(villager, now);
                return;
            }

            // Still in bed this morning
            if (now < villager.WakeTime)
            {
                return;
            }

            if (villager.LastBlockIndex < 0)
            {
                StartBlock(villager, 0, now);
                return;
            }

            // Routines done: free time until the leave time, then home
            if (villager.LastBlockIndex >= villager.TodayBlocks.Count)
            {
                if (now >= villager.HomeLeaveTime)
                {
                    OrderGoHome(villager);
                    return;
                }

                TickFreeTime(villager, now);
                return;
            }

            // A rest (a rest routine, or rest in place of a work it had no tool for) ends at the leave time and the
            // villager heads home; a work is finished first, however late it runs (C131)
            if (now >= villager.HomeLeaveTime && IsRestBlock(villager))
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Leave time {DayClock.ToHour(villager.HomeLeaveTime):F0}h, rest of block {villager.LastBlockIndex} given up, heading home");
                OrderGoHome(villager);
                return;
            }

            // On the way (fetching its tool, walking there): the block starts once the villager is really at it
            if (!villager.IsBlockStarted)
            {
                if (HasReachedRoutine(villager))
                {
                    MarkBlockStarted(villager, now, "arrived");
                }

                return;
            }

            if (TryResumeWithTool(villager, now))
            {
                return;
            }

            if (now >= villager.BlockEndTime)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Block {villager.LastBlockIndex} done at {DayClock.ToHour(now):F1}h");
                StartBlock(villager, villager.LastBlockIndex + 1, now);
            }
        }

        // Sends the villager on to block `index` (its tool and the walk there come first), or into free time after the last
        private void StartBlock(VillagerRuntime villager, int index, float now)
        {
            villager.LastBlockIndex = index;
            villager.IsBlockStarted = false;
            villager.IsFree = false;
            villager.WaitingToolType = null;

            if (index >= villager.TodayBlocks.Count)
            {
                if (now >= villager.HomeLeaveTime)
                {
                    AILog.Log(AILog.MANAGER, villager.Id, $"Day's routines done at {DayClock.ToHour(now):F1}h, past its leave time, straight home");
                    OrderGoHome(villager);
                    return;
                }

                AILog.Log(AILog.MANAGER, villager.Id, $"Day's routines done at {DayClock.ToHour(now):F1}h, free time until {DayClock.ToHour(villager.HomeLeaveTime):F0}h");
                TickFreeTime(villager, now);
                return;
            }

            // Too little of the day left for it: skipped, not shortened (the same rule as before nightfall)
            float length = _behaviourConfig.RoutineSeconds;
            float left = TimeManager.DAY_DURATION - now;
            if (left < _behaviourConfig.MinRemainingRatio * length)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Block {index} skipped, {left:F0}s left before nightfall");
                StartBlock(villager, index + 1, now);
                return;
            }

            ApplyBlockRoutine(villager, index, now, now + length);
        }

        // The block's routine, until `endTime` (an estimate until the block really starts)
        private void ApplyBlockRoutine(VillagerRuntime villager, int index, float now, float endTime)
        {
            WorkStation previousStation = villager.Current.Station;

            int serial = villager.NextSerial();
            if (!_resolver.TryResolve(villager.Profile, _time.DayCount, index, serial, now, endTime, previousStation, villager.Core.transform.position, out ResolvedRoutine resolved))
            {
                SkipRoutine(villager, index, endTime, $"block {index} resolve failed");
                MarkBlockStarted(villager, now, "resolve failed, rests instead");
                return;
            }

            if (previousStation != null && previousStation != resolved.Station)
            {
                previousStation.Release(villager.Id);
            }

            villager.Current = resolved;

            // The tool was searched for and asked about earlier today: the villager rests and waits for it instead of
            // searching again (a player handing it over resumes the block, TryResumeWithTool)
            if (_requests != null && _requests.IsWaitingForTool(villager.Id, resolved.ToolTypeId, _time.DayCount)
                && (villager.ToolHandler == null || !villager.ToolHandler.IsHoldingType(resolved.ToolTypeId)))
            {
                villager.WaitingToolType = resolved.ToolTypeId;
                SkipRoutine(villager, index, endTime, $"still waiting for '{resolved.ToolTypeId}' asked about today");
                MarkRestedForTool(villager, index, true);
                MarkBlockStarted(villager, now, "waits for its tool, rests instead");
                return;
            }

            villager.Core.ApplyRoutine(resolved);
            _observation?.OnRoutineApplied(villager.Core, villager.Interaction, resolved, endTime - now);
            RecordMemory(villager, resolved);
        }

        // The current block is a rest: the routine itself is one, or the villager rests in place of a work (no tool)
        private static bool IsRestBlock(VillagerRuntime villager)
        {
            ResolvedRoutine current = villager.Current;
            return current.Source == null || current.Category == RoutineCategory.Rest || !string.IsNullOrEmpty(villager.WaitingToolType);
        }

        // At the routine's spot (a work also needs its tool in hand): fetching the tool and the walk are not the block
        private bool HasReachedRoutine(VillagerRuntime villager)
        {
            ResolvedRoutine current = villager.Current;
            if (current.Source == null)
            {
                return true;
            }

            string tool = current.ToolTypeId;
            if (!string.IsNullOrEmpty(tool) && (villager.ToolHandler == null || !villager.ToolHandler.IsHoldingType(tool)))
            {
                return false;
            }

            // At its spot, or anywhere inside the place: a rest is taken wherever in the place the villager stops
            Vector3 position = villager.Core.transform.position;
            Vector3 offset = position - current.TargetPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= ROUTINE_ARRIVE_DISTANCE * ROUTINE_ARRIVE_DISTANCE
                || (current.CorruptionAxis != CorruptionAxis.Place && PlaceRegistry.TryGet(current.PlaceId, out PlaceArea place) && place.Contains(position));
        }

        // The block's length runs from here. The schedule tab is laid again from this start, held up by the ordinary
        // things only: a possessed villager's stand-still before setting off is left out, so the tab still shows when
        // it should have been there
        private void MarkBlockStarted(VillagerRuntime villager, float now, string reason)
        {
            int index = villager.LastBlockIndex;
            float length = _behaviourConfig.RoutineSeconds;
            villager.IsBlockStarted = true;
            villager.BlockEndTime = Mathf.Min(now + length, TimeManager.DAY_DURATION);

            ResolvedRoutine current = villager.Current;
            if (current.Source != null)
            {
                current.StartTime = now;
                current.EndTime = villager.BlockEndTime;
                villager.Current = current;
            }

            float shownStart = Mathf.Max(villager.WakeTime, now - villager.Core.ConsumedStartDelaySeconds);
            DailyBlock planned = villager.TodayBlocks[index];
            villager.SetBlock(index, DailyBlockPlanner.Make(index, shownStart, Mathf.Min(shownStart + length, TimeManager.DAY_DURATION)));
            EstimateBlocks(villager.Profile, _time.DayCount, villager.EditableBlocks, index + 1, villager.TodayBlocks[index].EndTime);
            EstimateHomeTimeline(villager.Profile, _time.DayCount, villager.TodayBlocks, villager.HomeLeaveTime, out int leaveHour, out int homeHour);
            villager.SetHomeTimeline(leaveHour, homeHour, Mathf.Max(DayClock.DAY_END_HOUR, homeHour));
            PublishBlocks(villager, _time.DayCount);
            AILog.Log(AILog.MANAGER, villager.Id, $"Block {index} started at {DayClock.ToHour(now):F1}h ({reason}), planned {DayClock.ToHour(planned.StartTime):F1}h, ends {DayClock.ToHour(villager.BlockEndTime):F1}h");
        }

        // The tool a failed work was missing came back (a player handed it over, found later): the block's own routine
        // goes on for what is left of it while enough is left (the same rule as a late start), else the tool goes back to its shed
        private bool TryResumeWithTool(VillagerRuntime villager, float now)
        {
            string toolType = villager.WaitingToolType;
            if (string.IsNullOrEmpty(toolType) || villager.ToolHandler == null || !villager.ToolHandler.IsHoldingType(toolType))
            {
                return false;
            }

            villager.WaitingToolType = null;
            int index = villager.LastBlockIndex;
            float remaining = villager.BlockEndTime - now;
            float minRemaining = _behaviourConfig.MinRemainingRatio * _behaviourConfig.RoutineSeconds;

            if (remaining < minRemaining)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Got '{toolType}' back, too late for block {index} ({remaining:F1}s < {minRemaining:F1}s)");
                villager.ToolHandler.PutAwayUnlessNeeded($"got '{toolType}' back too late");
                return true;
            }

            AILog.Log(AILog.MANAGER, villager.Id, $"Got '{toolType}' back, resuming block {index} ({remaining:F1}s left)");
            MarkRestedForTool(villager, index, false);
            ApplyBlockRoutine(villager, index, now, villager.BlockEndTime);
            return true;
        }

        // On the way home: a villager who will not make the home-by hour at a walk (held up by a player, a late start)
        // runs the rest of the way, also after night has fallen (C121)
        private void TickHomeward(VillagerRuntime villager, float now)
        {
            LogHomeArrival(villager, now);

            if (villager.HasArrivedHome)
            {
                StopRunning(villager, "home");
                if (!villager.Core.IsAsleep && IsAsleepAtHome(villager))
                {
                    villager.Core.SetAsleep(true);
                    // The bed row runs from when it really fell asleep (floored: never later than the moment)
                    villager.SetHomeTimeline(villager.LeaveHour, villager.HomeHour, Mathf.FloorToInt(DayClock.ToHour(now)));
                    PublishBlocks(villager, _time.DayCount);
                }

                return;
            }

            LogHomewardStall(villager, now);

            if (villager.IsRunningLate || villager.Movement == null || now < villager.NextHomeCheckTime)
            {
                return;
            }

            villager.NextHomeCheckTime = now + HOME_CHECK_INTERVAL;
            float walk = PathLength(villager.Core.transform.position, villager.Profile.HomePosition) / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            if (now + walk <= villager.HomeArriveTime)
            {
                return;
            }

            villager.IsRunningLate = true;
            villager.Movement.SetSpeed(villager.Movement.DefaultSpeed * _behaviourConfig.LateRunSpeedMultiplier);
            AILog.Log(AILog.MANAGER, villager.Id, $"Running home: {walk:F0}s walk left, {villager.HomeArriveTime - now:F0}s before home-by {DayClock.ToHour(villager.HomeArriveTime):F0}h " +
                $"[{DescribePath(villager.Core.transform.position, villager.Profile.HomePosition)}, agent speed {villager.Movement.DefaultSpeed:F1} → {villager.Movement.DefaultSpeed * _behaviourConfig.LateRunSpeedMultiplier:F1}]");
        }

        // Diagnostics only: a villager sent home that has not got anywhere for a while is logged once per stall, with what
        // its agent, interaction and tool handler are doing, so a standing-still villager can be told apart from a slow one
        private void LogHomewardStall(VillagerRuntime villager, float now)
        {
            Vector3 position = villager.Core.transform.position;
            if (villager.StallSince < 0f || (position - villager.StallAnchor).sqrMagnitude > STALL_MOVE_DISTANCE * STALL_MOVE_DISTANCE)
            {
                villager.StallAnchor = position;
                villager.StallSince = now;
                villager.IsStallLogged = false;
                return;
            }

            if (villager.IsStallLogged || now - villager.StallSince < STALL_LOG_SECONDS)
            {
                return;
            }

            villager.IsStallLogged = true;
            string interaction = villager.Interaction != null && villager.Interaction.IsInteracting ? villager.Interaction.Active.InteractableId : "none";
            string activity = villager.Conversation != null ? villager.Conversation.CurrentActivity().ToString() : "-";
            string tool = villager.ToolHandler != null && villager.ToolHandler.IsHolding ? "holding a tool" : "empty-handed";
            AILog.Warn(AILog.MANAGER, $"{villager.Id} not moving on the way home for {now - villager.StallSince:F0}s, {Vector3.Distance(position, villager.Profile.HomePosition):F1}m from home " +
                $"[agent {(villager.Movement != null ? villager.Movement.DescribeAgent() : "none")}; interaction {interaction}; activity {activity}; {tool}; speed x{Time.timeScale:0.#}]");
        }

        // Diagnostics for the run-home estimate: what PathLength measured against the straight line
        private static string DescribePath(Vector3 from, Vector3 to)
        {
            float straight = Vector3.Distance(from, to);
            NavMeshPath path = new NavMeshPath();

            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            {
                return $"straight {straight:F1}m, no path (straight line used)";
            }

            Vector3[] corners = path.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            float endGap = corners.Length > 0 ? Vector3.Distance(corners[corners.Length - 1], to) : straight;
            return $"straight {straight:F1}m, path {length:F1}m {path.status} ({corners.Length} corners, ends {endGap:F1}m from home)";
        }

        private static void StopRunning(VillagerRuntime villager, string reason)
        {
            if (!villager.IsRunningLate)
            {
                return;
            }

            villager.IsRunningLate = false;
            villager.Movement?.ResetSpeed();
            AILog.Log(AILog.MANAGER, villager.Id, $"Stopped running ({reason})");
        }

        // Asleep = in the house and past the bed hour. A ghost leaves its host only then (C121)
        // In the house at night (the schedule tab's bed hour only shows it, C131)
        public bool IsAsleepAtHome(VillagerRuntime villager)
        {
            return villager != null && villager.Core != null && villager.HasArrivedHome && !_isDayActive;
        }

        // "Home" is the same radius sleep paralysis uses to count a villager as in the house
        private void LogHomeArrival(VillagerRuntime villager, float now)
        {
            if (villager.HasArrivedHome || !IsAtHome(villager))
            {
                return;
            }

            villager.HasArrivedHome = true;
            villager.Core.SetHomeForNight(true);
            villager.LookInHouseId = null;
            // The home row from when it really got in; in after nightfall it sleeps as it gets in
            int homeHour = Mathf.FloorToInt(DayClock.ToHour(now));
            villager.SetHomeTimeline(villager.LeaveHour, homeHour, Mathf.Max(DayClock.DAY_END_HOUR, homeHour));
            PublishBlocks(villager, _time.DayCount);
            float beforeNight = villager.HomeArriveTime - now;
            AILog.Log(AILog.MANAGER, villager.Id, $"Arrived home at {DayClock.ToHour(now):F1}h ({(beforeNight >= 0f ? $"{beforeNight:F0}s before" : $"{-beforeNight:F0}s after")} nightfall)");
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

            // Everyone is still out at nightfall and heads home now (C131): not a warning
            if (outside.Count > 0)
            {
                AILog.Log(AILog.MANAGER, $"Night start: {outside.Count} villager(s) out, heading home: {string.Join(", ", outside)}");
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
            _unknownDeaths.Add(new UnknownDeath { Profile = villager.Profile });

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

            if (!PlaceRegistry.TryGet(current.PlaceId, out PlaceArea place) || !place.TryReserveStation(villager.Id, current.ActionId, out WorkStation station))
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

        // A work that cannot go on (its tool not found): the villager rests for the block's length and waits for the tool.
        // A search that failed on the way never got it there, so the block starts now as rest
        private void HandleRoutineFailed(VillagerRuntime villager, string reason)
        {
            ResolvedRoutine failed = villager.Current;
            float now = GetDayElapsed();
            float endTime = villager.IsBlockStarted ? villager.BlockEndTime : Mathf.Min(now + _behaviourConfig.RoutineSeconds, TimeManager.DAY_DURATION);
            villager.WaitingToolType = failed.ToolTypeId;
            if (villager.Memory != null)
            {
                villager.Memory.ForgetRoutine(failed.Serial);
            }

            SkipRoutine(villager, failed.Slot, endTime, reason);
            MarkRestedForTool(villager, failed.Slot, true);

            if (!villager.IsBlockStarted)
            {
                MarkBlockStarted(villager, now, "tool not found, rests instead");
            }
        }

        // The schedule tab shows the block as rest while the villager waits: everyone can see it is not at that work,
        // and where it went to rest (SkipRoutine has picked it by now; none if no rest place was found)
        private void MarkRestedForTool(VillagerRuntime villager, int slot, bool rested)
        {
            string restPlaceId = rested && villager.Current.Source != null ? villager.Current.PlaceId : null;
            Registry?.SetSlotRested(villager.Id, _time.DayCount, slot, rested, restPlaceId);
            AIEvents.RaiseSlotRested(villager.Id, _time.DayCount, slot, rested, restPlaceId);
        }

        // Rather than standing where it stopped, the villager rests nearby for what is left of the block (C110)
        private void SkipRoutine(VillagerRuntime villager, int slot, float endTime, string reason)
        {
            ReleaseStation(villager);
            villager.Current = default;

            if (TryApplyFallbackRest(villager, slot, endTime, reason))
            {
                return;
            }

            villager.Core.ClearRoutine(villager.NextSerial(), $"skipped: {reason}");
            AILog.Log(AILog.MANAGER, villager.Id, $"Routine skipped ({reason})");
        }

        // After the day's routines: free time until its leave time. The villager goes off to a rest place of its own
        // picking; there it talks (rumours spread) and its work tool is put away on the way (C131)
        private void TickFreeTime(VillagerRuntime villager, float now)
        {
            if (villager.IsFree)
            {
                return;
            }

            float until = villager.HomeReturnTime;
            villager.IsFree = true;
            villager.WaitingToolType = null;
            ReleaseStation(villager);
            villager.Current = default;

            if (!TryApplyFallbackRest(villager, villager.LastBlockIndex, until, $"free time {until - now:F0}s", anyPlace: true))
            {
                villager.Core.ClearRoutine(villager.NextSerial(), "free time, no rest place");
            }
        }

        private static string DescribeGroupsOrNone()
        {
            string groups = PlaceRegistry.DescribeGroups();
            return string.IsNullOrEmpty(groups) ? "none placed" : groups;
        }

        private bool TryApplyFallbackRest(VillagerRuntime villager, int slot, float endTime, string reason, bool anyPlace = false)
        {
            float now = GetDayElapsed();
            if (_factory.DataProvider == null || villager.Profile == null)
            {
                return false;
            }

            if (endTime <= now)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"No rest ({reason}): the block is already over");
                return false;
            }

            Vector3 from = villager.Core.transform.position;
            RoutineEntry best = null;
            float bestSqr = float.MaxValue;
            int candidates = 0;

            foreach (RoutineEntry routine in _factory.DataProvider.Routines)
            {
                if (routine == null || routine.Category != RoutineCategory.Rest || !routine.IsLoadableFor(villager.Profile.PublicInfo))
                {
                    continue;
                }

                // Free time is for gathering (rumours), so the spread-out places of a group (tree shade) are left out
                bool isGroup = PlaceRegistry.IsGroup(routine.PlaceId);
                if ((anyPlace && isGroup) || !PlaceRegistry.TryPick(routine.PlaceId, from, out PlaceArea place))
                {
                    continue;
                }

                // Free time: any rest place it may use, each equally likely (reservoir pick). Otherwise the nearest
                if (anyPlace)
                {
                    candidates++;
                    if (Random.Range(0, candidates) == 0)
                    {
                        best = routine;
                    }

                    continue;
                }

                float sqr = (place.Center - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = routine;
                    bestSqr = sqr;
                }
            }

            if (best == null)
            {
                AILog.Warn(AILog.MANAGER, $"{villager.Id} no rest ({reason}): no rest place in the scene for its rest routines (groups: {DescribeGroupsOrNone()})");
                return false;
            }

            if (!_resolver.TryResolveEntry(villager.Profile, best, _time.DayCount, slot, villager.NextSerial(), now, endTime, null, from, out ResolvedRoutine resolved))
            {
                return false;
            }

            villager.Current = resolved;
            villager.Core.ApplyRoutine(resolved);
            _observation?.OnRoutineApplied(villager.Core, villager.Interaction, resolved, endTime - now);
            RecordMemory(villager, resolved);
            string placeNote = resolved.PlaceId != best.PlaceId ? $"{resolved.PlaceId} (nearest of group {best.PlaceId})" : resolved.PlaceId;
            AILog.Log(AILog.MANAGER, villager.Id, $"Routine replaced by rest {best.RoutineId} @ {placeNote} until {endTime:F1}s ({reason})");
            return true;
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
            float lookInSeconds = TryLookInOnNeighbour(villager);
            UpdateHomeTimelineOnLeave(villager, GetDayElapsed(), lookInSeconds);
        }

        // On the way home: a neighbour (same home-point group) this villager has not seen all day gets a look-in, when
        // there is time for it before nightfall. Inside the house the normal trace scan sees what is there (a body
        // included). Setting off at night it goes straight home (C131). Returns the extra seconds the look-in adds
        private float TryLookInOnNeighbour(VillagerRuntime villager)
        {
            if (!_isDayActive || _observation == null || villager.Memory == null || villager.Interaction == null
                || string.IsNullOrEmpty(villager.Profile.NeighborhoodId)
                || (_visitedDay.TryGetValue(villager.Id, out int day) && day == _time.DayCount))
            {
                return 0f;
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
                return 0f;
            }

            if (!PlaceRegistry.TryGet(target.PublicInfo.HomePlaceId, out PlaceArea house))
            {
                return 0f;
            }

            Vector3 housePoint = house.GetHomePoint();
            if (!HasCompletePath(villager.Core.transform.position, housePoint))
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Skip look-in on {target.VillagerId}: no complete walkable path to the house");
                return 0f;
            }

            // The detour and the rest of the walk home before nightfall
            float speed = Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            Vector3 from = villager.Core.transform.position;
            float needed = (PathLength(from, housePoint) + PathLength(housePoint, villager.Profile.HomePosition)) / speed + _behaviourConfig.NeighbourVisitLingerSeconds;
            float left = TimeManager.DAY_DURATION - GetDayElapsed();
            if (needed > left)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Skip look-in on {target.VillagerId}: needs {needed:F0}s, {left:F0}s left before nightfall");
                return 0f;
            }

            if (!villager.Interaction.TryBegin(house, InteractionResponse.Approach, _behaviourConfig.NeighbourVisitLingerSeconds))
            {
                return 0f;
            }

            villager.LookInHouseId = house.InteractableId;
            _visitedDay[villager.Id] = _time.DayCount;
            AILog.Log(AILog.MANAGER, villager.Id, $"Looking in on {target.VillagerId} on the way home (not seen today)");
            return Mathf.Max(0f, needed - PathLength(from, villager.Profile.HomePosition) / speed);
        }

        private int GetBlockSeed(VillagerProfile profile, int dayCount)
        {
            return unchecked(_factory.Seed * 486187739 + profile.Index * 16777619 + dayCount * 31);
        }

        // A walk that really reaches the point: a partial path only gets as far as a wall and the villager would stand there
        private static bool HasCompletePath(Vector3 from, Vector3 to)
        {
            if (_pathBuffer == null)
            {
                _pathBuffer = new NavMeshPath();
            }

            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _pathBuffer) && _pathBuffer.status == NavMeshPathStatus.PathComplete;
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

        private sealed class UnknownDeath
        {
            public VillagerProfile Profile;
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
            // In free time between blocks (or after the last one, before the walk home)
            public bool IsFree { get; set; }
            // The tool a failed work of this block was missing; holding it again resumes the block
            public string WaitingToolType { get; set; }
            public AI_Movement Movement { get; }
            public bool IsHomeOrdered { get; set; }
            public bool HasArrivedHome { get; set; }
            public bool IsRunningLate { get; set; }
            public float NextHomeCheckTime { get; set; }
            public Vector3 StallAnchor { get; set; }
            public float StallSince { get; set; } = -1f;
            public bool IsStallLogged { get; set; }
            // Heads home at its leave time after its routines (a work running past it is finished first), due in by
            // nightfall (C131)
            public float HomeReturnTime => HomeLeaveTime;
            public float HomeArriveTime => TimeManager.DAY_DURATION;
            public float HomeLeaveTime { get; private set; } = TimeManager.DAY_DURATION;
            // Schedule tab (C131): sets off home, gets in, falls asleep. Estimates, then what really happened
            public int LeaveHour { get; private set; } = DayClock.DAY_END_HOUR;
            public int HomeHour { get; private set; } = DayClock.DAY_END_HOUR;
            public int BedHour { get; private set; } = DayClock.DAY_END_HOUR;
            // The house it looks in on, on the way home (the look-in is given up at nightfall)
            public string LookInHouseId { get; set; }
            public float WakeTime { get; private set; }
            // The current block (LastBlockIndex) has begun: the villager got there, or rests in its place
            public bool IsBlockStarted { get; set; }
            public float BlockEndTime { get; set; }
            public IReadOnlyList<DailyBlock> TodayBlocks => _todayBlocks;
            internal List<DailyBlock> EditableBlocks => _todayBlocks;
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
                Movement = core.GetComponent<AI_Movement>();
            }

            public int NextSerial()
            {
                _serial++;
                return _serial;
            }

            public void SetToday(List<DailyBlock> blocks, float wakeTime, float homeLeaveTime)
            {
                _todayBlocks = blocks;
                WakeTime = wakeTime;
                HomeLeaveTime = homeLeaveTime;
            }

            public void SetBlock(int index, DailyBlock block)
            {
                _todayBlocks[index] = block;
            }

            public void SetHomeTimeline(int leaveHour, int homeHour, int bedHour)
            {
                LeaveHour = leaveHour;
                HomeHour = homeHour;
                BedHour = bedHour;
            }

            public void ResetForDayStart()
            {
                LastBlockIndex = -1;
                IsBlockStarted = false;
                LookInHouseId = null;
                IsFree = false;
                WaitingToolType = null;
                IsHomeOrdered = false;
                HasArrivedHome = false;
                // The clock starts over each day: a check time left from last evening (or night) would hold off the next ones
                NextHomeCheckTime = 0f;
                StallSince = -1f;
                IsStallLogged = false;
            }

        }
    }
}
