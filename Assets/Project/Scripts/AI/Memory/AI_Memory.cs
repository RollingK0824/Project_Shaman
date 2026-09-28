using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Perception;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Memory
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Perception))]
    public class AI_Memory : MonoBehaviour, IAIConfigurable
    {
        [Header("Runtime")]
        [SerializeField] private List<string> _toolSightingsDebug = new List<string>();

        private AI_Core _core;
        private AI_Perception _perception;
        private AIRuntimeContext _context;

        public ToolSightingStore ToolSightings { get; private set; }

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _perception = GetComponent<AI_Perception>();

            _perception.OnToolSeen += RecordToolSighting;
        }

        private void OnDestroy()
        {
            _perception.OnToolSeen -= RecordToolSighting;
        }

        public void Configure(AIRuntimeContext context)
        {
            _context = context;
            ToolSightings = new ToolSightingStore(context.Config.ToolSightingsPerType);
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
                HolderId = tool.HolderId,
                DayCount = _core.CurrentRoutine.DayCount,
                SeenTime = _context.GameNow
            });

            ToolSightings.WriteDebug(_toolSightingsDebug);
        }
    }
}
