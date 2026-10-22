using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Perception
{
    [RequireComponent(typeof(AI_Core))]
    public class AI_Perception : MonoBehaviour, IAIConfigurable
    {
        private const float TOOL_LOOK_HEIGHT = 0.3f;

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
                if (CanSee(tool.Position + Vector3.up * TOOL_LOOK_HEIGHT))
                {
                    OnToolSeen?.Invoke(tool);
                }
            }
        }

        // Range is the caller's scan; here: in front (or close enough to sense) and not behind an obstacle.
        // An empty obstacle mask turns the line-of-sight test off.
        public bool CanSee(Vector3 point)
        {
            if (_context == null)
            {
                return true;
            }

            AIBehaviourConfig config = _context.Config;
            Vector3 eye = transform.position + Vector3.up * config.VisionEyeHeight;
            Vector3 toPoint = point - eye;
            float distance = toPoint.magnitude;

            if (distance < 0.01f)
            {
                return true;
            }

            Vector3 flat = new Vector3(toPoint.x, 0f, toPoint.z);
            if (distance > config.VisionNearRadius && flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > config.VisionHalfAngle)
            {
                return false;
            }

            return config.VisionObstacleMask.value == 0
                || !Physics.Raycast(eye, toPoint / distance, distance, config.VisionObstacleMask, QueryTriggerInteraction.Ignore);
        }
    }
}
