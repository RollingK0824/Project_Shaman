using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Perception
{
    [RequireComponent(typeof(AI_Core))]
    public class AI_Perception : MonoBehaviour, IAIConfigurable
    {
        private readonly List<ToolItem> _visibleBuffer = new List<ToolItem>();

        private AI_Core _core;
        private AIRuntimeContext _context;
        private float _nextScanTime;

        public event System.Action<ToolItem> OnToolSeen;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
        }

        public void Configure(AIRuntimeContext context)
        {
            _context = context;
        }

        private void Update()
        {
            if (_context == null || !_core.IsSimulating || Time.time < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.time + _context.Config.ToolSightInterval;
            ScanTools(transform.position, _context.Config.ToolSightRadius);
        }

        public void ScanTools(Vector3 center, float radius)
        {
            ToolRegistry.CollectVisible(center, radius, _visibleBuffer);

            foreach (ToolItem tool in _visibleBuffer)
            {
                OnToolSeen?.Invoke(tool);
            }
        }
    }
}
