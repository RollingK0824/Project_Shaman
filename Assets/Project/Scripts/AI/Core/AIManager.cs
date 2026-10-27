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
        // Free time shorter than this is not worth a trip: the villager stays as it is
        private const float FREE_TIME_MIN_SECONDS = 8f;
        // Schedule tab names for free time: an evening out before a late walk home, or a gap with no rest place to go to
        private const string OUTING_NAME = "마실";
        private const string FREE_TIME_NAME = "쉬기";

        // A planned free-time gap and where the villager spends it (null: nowhere it may rest, it stays put)
        public struct FreeSlot
        {
            public float Start;
            public float End;
            public RoutineEntry Rest;
            public bool IsOuting;
        }

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

        // Blocks already over at fromTime are dropped; the one in progress stays and TickVillager judges what is left of it
        private void PlanDay(int dayCount, float fromTime)
        {
            foreach (VillagerRuntime villager in _villagers)
            {
                PickHomeHours(villager.Profile, dayCount, out int homeHour, out int bedHour);
                float homeArriveTime = DayClock.ToDayElapsed(homeHour);
                float homeReturnTime = CalculateHomeReturnTime(villager, dayCount, homeArriveTime);
                List<DailyBlock> blocks = DailyBlockPlanner.Build(GetBlockSeed(villager.Profile, dayCount), _behaviourConfig, homeReturnTime, out string blockNote);
                List<FreeSlot> freeSlots = PlanFreeTime(villager.Profile, dayCount, blocks, homeReturnTime, homeHour > DayClock.DAY_END_HOUR);
                blocks.RemoveAll(block => block.EndTime <= fromTime);
                villager.SetToday(blocks, freeSlots, homeReturnTime, homeArriveTime);
                villager.Core.SetDailyStartOffset(blocks.Count > 0 ? blocks[0].StartTime : 0f);
                int[] blockHours = DailyBlock.ToHourPairs(blocks);
                villager.Core.SetTodayBlocks(dayCount, blockHours, homeHour, bedHour);
                PublishDayHours(villager.Id, dayCount, blockHours, homeHour, bedHour);
                PublishFreeTime(villager.Id, dayCount, blocks, freeSlots);
                PlanCorruption(villager, dayCount);

                if (villager.Memory != null)
                {
                    villager.Memory.BeginDay(dayCount);
                }

                if (villager.Stress != null)
                {
                    villager.Stress.ApplyMorningDecay();
                }
                AILog.Log(AILog.MANAGER, villager.Id, $"Day {dayCount} blocks=[{string.Join(", ", blocks)}] {blockNote} homeReturn={DayClock.ToHour(villager.HomeReturnTime):F1}h ({villager.HomeReturnTime:F1}s) home by {homeHour}h, bed {bedHour}h");
            }

            // A death the village does not know of yet keeps a day on the schedule tab, planned the same way with the
            // walk home it last had. The death comes out through villagers ("not seen all day"), not through an empty day
            foreach (UnknownDeath dead in _unknownDeaths)
            {
                PickHomeHours(dead.Profile, dayCount, out int homeHour, out int bedHour);
                float homeReturnTime = DayClock.ToDayElapsed(homeHour) - dead.HomeWalkSeconds;
                List<DailyBlock> blocks = DailyBlockPlanner.Build(GetBlockSeed(dead.Profile, dayCount), _behaviourConfig, homeReturnTime, out _);
                PublishDayHours(dead.Profile.VillagerId, dayCount, DailyBlock.ToHourPairs(blocks), homeHour, bedHour);
                PublishFreeTime(dead.Profile.VillagerId, dayCount, blocks, PlanFreeTime(dead.Profile, dayCount, blocks, homeReturnTime, homeHour > DayClock.DAY_END_HOUR));
            }
        }

        private void PublishDayHours(string villagerId, int dayCount, int[] blockHours, int homeHour, int bedHour)
        {
            Registry?.SetDayHours(villagerId, dayCount, blockHours, homeHour, bedHour);
            AIEvents.RaiseDayPlanned(villagerId, dayCount, blockHours, homeHour, bedHour);
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
            if (!_isRunning)
            {
                return;
            }

            float now = GetDayElapsed();

            // Night: the villagers still on their way home hurry on until they are in
            if (!_isDayActive)
            {
                foreach (VillagerRuntime villager in _villagers)
                {
                    if (villager.Core != null && villager.IsHomeOrdered)
                    {
                        TickHomeward(villager, now);
                    }
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

            if (now >= villager.HomeReturnTime || MustLeaveForHome(villager, now))
            {
                OrderGoHome(villager);
                return;
            }

            if (!villager.TryGetBlockAt(now, out DailyBlock block))
            {
                TickFreeTime(villager, now);
                return;
            }

            float startTime = block.StartTime;
            float endTime = Mathf.Min(block.EndTime, villager.HomeReturnTime);
            float remaining = endTime - now;
            float minRemaining = _behaviourConfig.MinRemainingRatio * block.Length;

            if (block.Index == villager.LastBlockIndex)
            {
                TryResumeWithTool(villager, block, remaining, minRemaining);
                return;
            }

            villager.LastBlockIndex = block.Index;
            villager.IsFree = false;
            villager.WaitingToolType = null;

            if (remaining < minRemaining)
            {
                SkipRoutine(villager, block.Index, endTime, $"block {block.Index} remaining {remaining:F1}s < {minRemaining:F1}s");
                return;
            }

            WorkStation previousStation = villager.Current.Station;

            int serial = villager.NextSerial();
            if (_resolver.TryResolve(villager.Profile, _time.DayCount, block.Index, serial, startTime, endTime, previousStation, villager.Core.transform.position, out ResolvedRoutine resolved))
            {
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
                    SkipRoutine(villager, block.Index, endTime, $"still waiting for '{resolved.ToolTypeId}' asked about today");
                    MarkRestedForTool(villager, block.Index, true);
                    return;
                }

                villager.Core.ApplyRoutine(resolved);
                _observation?.OnRoutineApplied(villager.Core, villager.Interaction, resolved, endTime - now);
                RecordMemory(villager, resolved);
            }
            else
            {
                SkipRoutine(villager, block.Index, endTime, $"block {block.Index} resolve failed");
            }
        }

        // The tool a failed work was missing came back (a player handed it over, found later): the block's own routine
        // starts again while enough of it is left (the same rule as a late start), else the tool goes back to its shed
        private void TryResumeWithTool(VillagerRuntime villager, DailyBlock block, float remaining, float minRemaining)
        {
            string toolType = villager.WaitingToolType;
            if (string.IsNullOrEmpty(toolType) || villager.ToolHandler == null || !villager.ToolHandler.IsHoldingType(toolType))
            {
                return;
            }

            villager.WaitingToolType = null;

            if (remaining < minRemaining)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Got '{toolType}' back, too late for block {block.Index} ({remaining:F1}s < {minRemaining:F1}s)");
                villager.ToolHandler.PutAwayUnlessNeeded($"got '{toolType}' back too late");
                return;
            }

            AILog.Log(AILog.MANAGER, villager.Id, $"Got '{toolType}' back, resuming block {block.Index} ({remaining:F1}s left)");
            MarkRestedForTool(villager, block.Index, false);
            villager.LastBlockIndex = -1;
        }

        // The planned home time assumes the walk from the last routine's place. Wherever the villager actually is
        // (a far fallback, a search that wandered), it also sets off once the walk from here would end after its home-by hour.
        private bool MustLeaveForHome(VillagerRuntime villager, float now)
        {
            if (now < villager.NextHomeCheckTime)
            {
                return false;
            }

            villager.NextHomeCheckTime = now + HOME_CHECK_INTERVAL;
            float walk = PathLength(villager.Core.transform.position, villager.Profile.HomePosition) / Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            float lead = _behaviourConfig.ToolTidySecondsEstimate + walk + _behaviourConfig.HomeReturnMarginSeconds;
            if (now + lead < villager.HomeArriveTime)
            {
                return false;
            }

            AILog.Log(AILog.MANAGER, villager.Id, $"Leaving for home early: {walk:F0}s walk from here, {villager.HomeArriveTime - now:F0}s left before home-by {DayClock.ToHour(villager.HomeArriveTime):F0}h");
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
                if (IsAsleepAtHome(villager))
                {
                    villager.Core.SetAsleep(true);
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
        public bool IsAsleepAtHome(VillagerRuntime villager)
        {
            return villager != null && villager.Core != null && villager.HasArrivedHome
                && (villager.Core.TodayBedHour <= 0 || DayClock.IsPastBedHour(villager.Core.TodayBedHour));
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
            float early = villager.HomeArriveTime - now;
            string verdict = early >= 0f ? $"{early:F0}s before" : $"LATE by {-early:F0}s for";
            AILog.Log(AILog.MANAGER, villager.Id, $"Arrived home at {DayClock.ToHour(now):F1}h ({now:F1}s), {verdict} home-by {DayClock.ToHour(villager.HomeArriveTime):F0}h");
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
            _unknownDeaths.Add(new UnknownDeath { Profile = villager.Profile, HomeWalkSeconds = villager.HomeArriveTime - villager.HomeReturnTime });

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

        // A work that cannot go on (its tool not found): the villager rests until the block ends and waits for the tool
        private void HandleRoutineFailed(VillagerRuntime villager, string reason)
        {
            ResolvedRoutine failed = villager.Current;
            villager.WaitingToolType = failed.ToolTypeId;
            SkipRoutine(villager, failed.Slot, failed.EndTime, reason);
            MarkRestedForTool(villager, failed.Slot, true);
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

        // Between blocks and after the last one: free time until the next block or the walk home. The villager goes to the
        // rest place planned for it this morning (PlanFreeTime); there it talks (rumours spread) and its work tool is put
        // away on the way. Before the first block it is still at home
        private void TickFreeTime(VillagerRuntime villager, float now)
        {
            if (villager.IsFree || villager.LastBlockIndex < 0)
            {
                return;
            }

            float until = villager.HomeReturnTime;
            foreach (DailyBlock next in villager.TodayBlocks)
            {
                if (next.StartTime > now)
                {
                    until = Mathf.Min(until, next.StartTime);
                    break;
                }
            }

            if (until - now < FREE_TIME_MIN_SECONDS)
            {
                return;
            }

            villager.IsFree = true;
            villager.WaitingToolType = null;
            ReleaseStation(villager);
            villager.Current = default;

            string reason = $"free time {until - now:F0}s";
            RoutineEntry planned = villager.PlannedRestAt(now);
            bool rested = planned != null
                ? TryApplyRest(villager, villager.LastBlockIndex, until, planned, reason)
                : TryApplyFallbackRest(villager, villager.LastBlockIndex, until, reason, anyPlace: true);

            if (!rested)
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

            return TryApplyRest(villager, slot, endTime, best, reason);
        }

        private bool TryApplyRest(VillagerRuntime villager, int slot, float endTime, RoutineEntry rest, string reason)
        {
            float now = GetDayElapsed();
            Vector3 from = villager.Core.transform.position;
            if (!_resolver.TryResolveEntry(villager.Profile, rest, _time.DayCount, slot, villager.NextSerial(), now, endTime, null, from, out ResolvedRoutine resolved))
            {
                return false;
            }

            villager.Current = resolved;
            villager.Core.ApplyRoutine(resolved);
            _observation?.OnRoutineApplied(villager.Core, villager.Interaction, resolved, endTime - now);
            RecordMemory(villager, resolved);
            string placeNote = resolved.PlaceId != rest.PlaceId ? $"{resolved.PlaceId} (nearest of group {rest.PlaceId})" : resolved.PlaceId;
            AILog.Log(AILog.MANAGER, villager.Id, $"Routine replaced by rest {rest.RoutineId} @ {placeNote} until {endTime:F1}s ({reason})");
            return true;
        }

        // Free time is planned with the day (C130): each gap after a block of at least FREE_TIME_MIN_SECONDS gets a
        // gathering rest place picked now, so the schedule tab can show where the villager will be. The spread-out
        // places of a group (tree shade) are left out: free time is for meeting people (rumours)
        private List<FreeSlot> PlanFreeTime(VillagerProfile profile, int dayCount, List<DailyBlock> blocks, float homeReturnTime, bool staysOutLate)
        {
            List<FreeSlot> slots = new List<FreeSlot>();
            List<RoutineEntry> rests = new List<RoutineEntry>();
            if (_factory.DataProvider != null)
            {
                foreach (RoutineEntry routine in _factory.DataProvider.Routines)
                {
                    if (routine != null && routine.Category == RoutineCategory.Rest && routine.IsLoadableFor(profile.PublicInfo)
                        && !PlaceRegistry.IsGroup(routine.PlaceId) && PlaceRegistry.TryGet(routine.PlaceId, out _))
                    {
                        rests.Add(routine);
                    }
                }
            }

            System.Random random = new System.Random(unchecked(GetBlockSeed(profile, dayCount) * 31 + 7));
            for (int i = 0; i < blocks.Count; i++)
            {
                bool isLast = i == blocks.Count - 1;
                float start = blocks[i].EndTime;
                float end = isLast ? homeReturnTime : blocks[i + 1].StartTime;
                if (end - start < FREE_TIME_MIN_SECONDS)
                {
                    continue;
                }

                slots.Add(new FreeSlot
                {
                    Start = start,
                    End = end,
                    Rest = rests.Count > 0 ? rests[random.Next(rests.Count)] : null,
                    IsOuting = isLast && staysOutLate
                });
            }

            return slots;
        }

        // Free time on the schedule tab: what the villager does there and where, in the hours between its blocks
        private void PublishFreeTime(string villagerId, int dayCount, List<DailyBlock> blocks, List<FreeSlot> slots)
        {
            int[] hours = new int[slots.Count * 2];
            string[] names = new string[slots.Count];
            string[] placeIds = new string[slots.Count];

            for (int i = 0; i < slots.Count; i++)
            {
                FreeSlot slot = slots[i];
                int startHour = Mathf.RoundToInt(DayClock.ToHour(slot.Start));
                int endHour = Mathf.RoundToInt(DayClock.ToHour(slot.End));

                // Kept clear of the blocks' own hour rows
                foreach (DailyBlock block in blocks)
                {
                    if (block.EndTime <= slot.Start + 0.01f)
                    {
                        startHour = Mathf.Max(startHour, block.EndHour);
                    }
                    else if (block.StartTime >= slot.End - 0.01f)
                    {
                        endHour = Mathf.Min(endHour, block.StartHour);
                    }
                }

                hours[i * 2] = startHour;
                hours[i * 2 + 1] = endHour;
                names[i] = slot.IsOuting ? OUTING_NAME : slot.Rest != null ? slot.Rest.DisplayName : FREE_TIME_NAME;
                placeIds[i] = slot.Rest != null ? slot.Rest.PlaceId : null;
            }

            Registry?.SetFreeTime(villagerId, dayCount, hours, names, placeIds);
            AIEvents.RaiseFreeTimePlanned(villagerId, dayCount, hours, names, placeIds);
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

            // Only if the detour still ends at home by the home-by hour (delays during the day eat the budget)
            float speed = Mathf.Max(0.1f, _behaviourConfig.WalkSpeedEstimate);
            Vector3 housePoint = house.GetHomePoint();
            if (!HasCompletePath(villager.Core.transform.position, housePoint))
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Skip look-in on {target.VillagerId}: no complete walkable path to the house");
                return;
            }

            float needed = (PathLength(villager.Core.transform.position, housePoint) + PathLength(housePoint, villager.Profile.HomePosition)) / speed
                + _behaviourConfig.NeighbourVisitLingerSeconds + _behaviourConfig.HomeReturnMarginSeconds;
            float left = villager.HomeArriveTime - GetDayElapsed();
            if (needed > left)
            {
                AILog.Log(AILog.MANAGER, villager.Id, $"Skip look-in on {target.VillagerId}: needs {needed:F0}s, {left:F0}s left before home-by");
                return;
            }

            if (villager.Interaction.TryBegin(house, InteractionResponse.Approach, _behaviourConfig.NeighbourVisitLingerSeconds))
            {
                _visitedDay[villager.Id] = _time.DayCount;
                AILog.Log(AILog.MANAGER, villager.Id, $"Looking in on {target.VillagerId} on the way home (not seen today)");
            }
        }

        private int GetBlockSeed(VillagerProfile profile, int dayCount)
        {
            return unchecked(_factory.Seed * 486187739 + profile.Index * 16777619 + dayCount * 31);
        }

        // Each villager's home-by and bed hours, fixed for the day and shown on the schedule tab (half-sijin rows)
        private void PickHomeHours(VillagerProfile profile, int dayCount, out int homeHour, out int bedHour)
        {
            System.Random random = new System.Random(unchecked(GetBlockSeed(profile, dayCount) * 7 + 1));
            homeHour = Mathf.Clamp(PickHour(random, _behaviourConfig.HomeArriveHours, DayClock.DAY_END_HOUR - 1), DayClock.DAY_START_HOUR + 1, DayClock.DAY_END_HOUR);
            // Now and then a villager stays out past dark and walks home at night, where it may meet a ghost (C122).
            // Home by 21~23h: it sets off at dusk at the latest (CalculateHomeReturnTime) and is not hurried on the way
            if (random.NextDouble() < _behaviourConfig.StayOutLateChance)
            {
                homeHour = random.Next(DayClock.DAY_END_HOUR + 1, DayClock.DAY_END_HOUR + 4);
            }

            // Everyone in the house goes to sleep as night falls; one still out sleeps as it gets in
            bedHour = Mathf.Max(DayClock.DAY_END_HOUR, homeHour);
        }

        private static int PickHour(System.Random random, IReadOnlyList<int> hours, int fallback)
        {
            return hours != null && hours.Count > 0 ? hours[random.Next(hours.Count)] : fallback;
        }

        // When to set off so the villager is home by homeArriveTime (walk from the last routine's place + look-in + margin)
        private float CalculateHomeReturnTime(VillagerRuntime villager, int dayCount, float homeArriveTime)
        {
            float travelDistance = 0f;
            float visitSeconds = 0f;
            VillagerSchedule schedule = villager.Profile.Schedule;
            RoutineEntry lastRoutine = schedule != null ? schedule.Get(dayCount, _behaviourConfig.RoutinesPerDay - 1) : null;

            if (lastRoutine != null && PlaceRegistry.TryPick(lastRoutine.PlaceId, villager.Profile.HomePosition, out PlaceArea place))
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
            // Nightfall sends everyone home, so even one due home late sets off before dusk (by the same margin)
            float latest = DayClock.ToDayElapsed(DayClock.DAY_END_HOUR) - _behaviourConfig.HomeReturnMarginSeconds;
            return Mathf.Clamp(homeArriveTime - lead, 0f, Mathf.Max(0f, latest));
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
            // The walk home of its last day: its next days are planned with it, as if it still came home from work
            public float HomeWalkSeconds;
        }

        public class VillagerRuntime
        {
            private int _serial;
            private List<DailyBlock> _todayBlocks = new List<DailyBlock>();
            private List<FreeSlot> _todayFree = new List<FreeSlot>();

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
            public float HomeReturnTime { get; private set; }
            public float HomeArriveTime { get; private set; } = TimeManager.DAY_DURATION;
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
                Movement = core.GetComponent<AI_Movement>();
            }

            public int NextSerial()
            {
                _serial++;
                return _serial;
            }

            public void SetToday(List<DailyBlock> blocks, List<FreeSlot> freeSlots, float homeReturnTime, float homeArriveTime)
            {
                _todayBlocks = blocks;
                _todayFree = freeSlots;
                HomeReturnTime = homeReturnTime;
                HomeArriveTime = homeArriveTime;
            }

            // The rest planned for the free-time gap now falls in, null if none
            public RoutineEntry PlannedRestAt(float time)
            {
                foreach (FreeSlot slot in _todayFree)
                {
                    if (time >= slot.Start && time < slot.End)
                    {
                        return slot.Rest;
                    }
                }

                return null;
            }

            public void ResetForDayStart()
            {
                LastBlockIndex = -1;
                IsFree = false;
                WaitingToolType = null;
                IsHomeOrdered = false;
                HasArrivedHome = false;
                // The clock starts over each day: a check time left from last evening (or night) would hold off the next ones
                NextHomeCheckTime = 0f;
                StallSince = -1f;
                IsStallLogged = false;
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
