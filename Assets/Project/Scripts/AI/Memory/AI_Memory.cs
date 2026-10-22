using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.Map;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
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

        public ToolSightingStore ToolSightings { get; private set; }
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

        private void RecordInteraction(InteractionResult result, InteractionEndReason reason)
        {
            if (result.Kind == InteractionKind.GhostTrace && result.Witnessed)
            {
                Record(MemoryKind.TraceWitness, result.Subject, result.PlaceId, result.Position, result.Detailed ? "detailed" : "glance");
                return;
            }

            if (result.Kind == InteractionKind.Conversation && result.Response == InteractionResponse.Converse)
            {
                Record(MemoryKind.Conversation, result.InteractableId, null, transform.position, reason.ToString());
            }
        }
    }
}
