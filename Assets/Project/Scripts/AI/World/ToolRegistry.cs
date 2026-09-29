using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.World
{
    public static class ToolRegistry
    {
        private static readonly List<ToolItem> _tools = new List<ToolItem>();

        public static IReadOnlyList<ToolItem> All => _tools;

        public static void Register(ToolItem tool)
        {
            if (tool == null || _tools.Contains(tool))
            {
                return;
            }

            if (string.IsNullOrEmpty(tool.TypeId))
            {
                AILog.Warn(AILog.TOOL, $"ToolItem without TypeId ignored: {tool.name}");
                return;
            }

            _tools.Add(tool);
        }

        public static void Unregister(ToolItem tool)
        {
            _tools.Remove(tool);
        }

        public static ToolItem FindAvailableNear(string typeId, Vector3 center, float radius, string villagerId)
        {
            ToolItem best = null;
            float bestDistance = radius * radius;

            foreach (ToolItem tool in _tools)
            {
                if (tool == null || tool.TypeId != typeId || !tool.IsAvailableFor(villagerId))
                {
                    continue;
                }

                float distance = (tool.Position - center).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = tool;
                }
            }

            return best;
        }

        public static void CollectVisible(Vector3 center, float radius, List<ToolItem> result)
        {
            result.Clear();
            float radiusSqr = radius * radius;

            foreach (ToolItem tool in _tools)
            {
                if (tool != null && (tool.Position - center).sqrMagnitude <= radiusSqr)
                {
                    result.Add(tool);
                }
            }
        }

        public static void CollectStoragePositions(string typeId, List<Vector3> result)
        {
            HashSet<string> visited = new HashSet<string>();

            foreach (ToolItem tool in _tools)
            {
                if (tool == null || tool.TypeId != typeId || string.IsNullOrEmpty(tool.HomeStoragePlaceId) || !visited.Add(tool.HomeStoragePlaceId))
                {
                    continue;
                }

                if (PlaceRegistry.TryGet(tool.HomeStoragePlaceId, out PlaceArea place))
                {
                    result.Add(place.Center);
                }
            }
        }
    }
}
