using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.Map;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Observation;
using ProjectShaman.AI.Trace;
using ProjectShaman.AI.Perception;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Memory
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Perception))]
    public class AI_Memory : MonoBehaviour, IAIConfigurable
    {
        [Header("Runtime")]
        [SerializeField] private List<string> _toolSightingsDebug = new List<string>();
        [SerializeField] private List<string> _eventsDebug = new List<string>();

        private AI_Core _core;
        private AI_Perception _perception;
        private AI_Interaction _interaction;
        private AIRuntimeContext _context;
        private int _currentDay;
        private readonly Dictionary<string, (int Day, float Time)> _lastSeen = new Dictionary<string, (int, float)>();

        public ToolSightingStore ToolSightings { get; private set; }
        public int CurrentDay => _currentDay;
        public EventMemoryStore Events { get; private set; }

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _perception = GetComponent<AI_Perception>();
            _interaction = GetComponent<AI_Interaction>();

            _perception.OnToolSeen += RecordToolSighting;

            if (_interaction != null)
            {
                _interaction.OnInteractionEnded += RecordInteraction;
            }
        }

        private void OnDestroy()
        {
            _perception.OnToolSeen -= RecordToolSighting;

            if (_interaction != null)
            {
                _interaction.OnInteractionEnded -= RecordInteraction;
            }
        }

        public void Configure(AIRuntimeContext context)
        {
            _context = context;
            ToolSightings = new ToolSightingStore(context.Config.ToolSightingsPerType);
            Events = new EventMemoryStore(context.Config.MemoryEntriesPerKind, context.Config.MemoryDays);
        }

        public void BeginDay(int dayCount)
        {
            _currentDay = dayCount;

            if (Events == null)
            {
                return;
            }

            int removed = Events.Prune(dayCount);
            if (removed > 0)
            {
                AILog.Log(AILog.MEMORY, _core.LogId, $"Day {dayCount}: forgot {removed} old entries");
                Events.WriteDebug(_eventsDebug);
            }
        }

        public void RecordToolSighting(ToolItem tool)
        {
            if (ToolSightings == null || tool == null)
            {
                return;
            }

            ToolSightings.Record(new ToolSighting
            {
                InstanceId = tool.InstanceId,
                TypeId = tool.TypeId,
                Position = tool.Position,
                AreaId = MapAreaRegistry.ResolveId(tool.Position),
                HolderId = tool.HolderId,
                DayCount = _core.CurrentRoutine.DayCount,
                SeenTime = _context.GameNow
            });

            ToolSightings.WriteDebug(_toolSightingsDebug);
        }

        public void RecordRoutine(in ResolvedRoutine routine)
        {
            if (routine.Source == null)
            {
                return;
            }

            string detail = routine.IsCorrupted ? $"{routine.ActionId} corrupted={routine.CorruptionAxis}" : routine.ActionId;
            Record(MemoryKind.Routine, routine.Source.RoutineId, routine.Source.PlaceId, routine.TargetPosition, detail);
        }

        public void RecordToolLost(string toolTypeId)
        {
            Record(MemoryKind.ToolLost, toolTypeId, _core.CurrentRoutine.Source?.PlaceId, transform.position, null);
        }

        public void RecordSleepParalysis(string actionId)
        {
            Record(MemoryKind.SleepParalysis, actionId, null, transform.position, null);
        }

        public void Record(MemoryKind kind, string subjectId, string placeId, Vector3 position, string detail)
        {
            if (Events == null)
            {
                return;
            }

            MemoryEntry entry = new MemoryEntry
            {
                Kind = kind,
                Source = MemorySource.Direct,
                SubjectId = subjectId,
                PlaceId = placeId,
                Position = position,
                Detail = detail,
                DayCount = _currentDay,
                RecordedTime = _context.GameNow
            };

            Events.Record(entry);
            Events.WriteDebug(_eventsDebug);
            AILog.Log(AILog.MEMORY, _core.LogId, $"Remember {entry}");
        }

        // Seen with our own eyes; the same act by the same actor counts once a day
        public bool RecordWitness(Observable observable, int traitMask)
        {
            if (Events == null || observable == null)
            {
                return false;
            }

            MemoryEntry entry = new MemoryEntry
            {
                Kind = MemoryKind.Witness,
                Source = MemorySource.Direct,
                SubjectId = observable.ActorId,
                ActorId = observable.ActorId,
                VerbId = observable.VerbId,
                ObjectId = observable.ObjectId,
                Position = observable.Position,
                AreaId = MapAreaRegistry.ResolveId(observable.Position),
                TraitMask = traitMask,
                IsAnomalous = observable.IsAnomalous,
                DayCount = _currentDay,
                RecordedTime = _context.GameNow
            };
            entry.PlaceId = entry.AreaId;

            return RecordFact(entry);
        }

        // Seen out at night (C122): only where, never whose ghost. Keyed by the host so one ghost counts once a night,
        // and an exorcism lets it go with the host's other odd things
        public bool RecordGhostSighting(string hostId, Vector3 position)
        {
            string areaId = MapAreaRegistry.ResolveId(position);
            return RecordFact(new MemoryEntry
            {
                Kind = MemoryKind.GhostSighting,
                Source = MemorySource.Direct,
                SubjectId = hostId,
                ActorId = hostId,
                VerbId = "ghost",
                Position = position,
                AreaId = areaId,
                PlaceId = areaId,
                IsAnomalous = true,
                DayCount = _currentDay,
                RecordedTime = _context.GameNow
            });
        }

        // Witness/heard facts share one dedupe rule: a fact we already know (from anyone) is not stored twice
        public bool RecordFact(MemoryEntry entry)
        {
            if (Events == null || Events.ContainsFact(entry.FactKey))
            {
                return false;
            }

            Events.Record(entry);
            Events.WriteDebug(_eventsDebug);
            AILog.Log(AILog.MEMORY, _core.LogId, $"Remember {entry}");
            return true;
        }

        public void RecordSeenVillager(string villagerId)
        {
            if (_context != null && !string.IsNullOrEmpty(villagerId))
            {
                _lastSeen[villagerId] = (_currentDay, _context.GameNow);
            }
        }

        public bool HasSeenToday(string villagerId)
        {
            return _lastSeen.TryGetValue(villagerId, out (int Day, float Time) seen) && seen.Day == _currentDay;
        }

        public bool TryGetLastSeen(string villagerId, out int day, out float time)
        {
            bool found = _lastSeen.TryGetValue(villagerId, out (int Day, float Time) seen);
            day = seen.Day;
            time = seen.Time;
            return found;
        }

        // The odd things a host did were the ghost's doing: once it is driven out, what was seen or heard of them is let go
        public int ForgetAnomaliesOf(string actorId)
        {
            if (Events == null || string.IsNullOrEmpty(actorId))
            {
                return 0;
            }

            int removed = Events.RemoveAll(e => e.IsAnomalous && e.ActorId == actorId);
            if (removed > 0)
            {
                Events.WriteDebug(_eventsDebug);
                AILog.Log(AILog.MEMORY, _core.LogId, $"Forgot {removed} odd thing(s) about {actorId} (exorcised)");
            }

            return removed;
        }

        public bool Knows(in MemoryEntry fact)
        {
            return Events != null && Events.ContainsFact(fact.FactKey);
        }

        private void RecordInteraction(InteractionResult result, InteractionEndReason reason)
        {
            if (result.Kind == InteractionKind.GhostTrace && result.Witnessed)
            {
                GhostTrace trace = QuestTargets.TryGet(result.Subject, out IQuestTarget target) ? target as GhostTrace : null;
                string kind = trace != null ? trace.Kind.ToString() : null;
                string areaId = MapAreaRegistry.ResolveId(result.Position) ?? result.PlaceId;
                RecordFact(new MemoryEntry
                {
                    Kind = MemoryKind.TraceWitness,
                    Source = MemorySource.Direct,
                    SubjectId = result.Subject,
                    PlaceId = areaId,
                    AreaId = areaId,
                    Position = result.Position,
                    Detail = result.Detailed ? "detailed" : "glance",
                    VerbId = "trace",
                    ObjectId = kind,
                    ActorId = trace != null ? trace.VictimId : null,
                    IsAnomalous = true,
                    DayCount = _currentDay,
                    RecordedTime = _context.GameNow
                });
                return;
            }

            if (result.Kind == InteractionKind.Conversation && result.Response == InteractionResponse.Converse)
            {
                Record(MemoryKind.Conversation, result.InteractableId, null, transform.position, reason.ToString());
            }
        }
    }
}
