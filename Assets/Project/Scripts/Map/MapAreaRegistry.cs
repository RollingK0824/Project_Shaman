using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.Map
{
    // Every IMapArea registers itself here (OnEnable/OnDisable). Lookups never depend on AIManager or scene order.
    public static class MapAreaRegistry
    {
        private const float NEAR_DISTANCE = 12f;

        private static readonly Dictionary<string, IMapArea> _byId = new Dictionary<string, IMapArea>();
        private static readonly List<IMapArea> _all = new List<IMapArea>();

        public static IReadOnlyList<IMapArea> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _byId.Clear();
            _all.Clear();
        }

        public static void Register(IMapArea area)
        {
            if (area == null || string.IsNullOrEmpty(area.AreaId))
            {
                Debug.LogWarning($"[MapArea] Area without id ignored: {Describe(area)}");
                return;
            }

            if (_byId.TryGetValue(area.AreaId, out IMapArea existing) && !ReferenceEquals(existing, area))
            {
                Debug.LogWarning($"[MapArea] Duplicate area id '{area.AreaId}' on {Describe(area)}, kept {Describe(existing)}");
                return;
            }

            _byId[area.AreaId] = area;
            if (!_all.Contains(area))
            {
                _all.Add(area);
            }
        }

        public static void Unregister(IMapArea area)
        {
            if (area == null)
            {
                return;
            }

            _all.Remove(area);
            if (!string.IsNullOrEmpty(area.AreaId) && _byId.TryGetValue(area.AreaId, out IMapArea existing) && ReferenceEquals(existing, area))
            {
                _byId.Remove(area.AreaId);
            }
        }

        public static bool TryGet(string areaId, out IMapArea area)
        {
            area = null;
            return !string.IsNullOrEmpty(areaId) && _byId.TryGetValue(areaId, out area) && IsAlive(area);
        }

        // The most specific area containing the position: deepest in the hierarchy, then the smallest footprint
        public static bool TryResolve(Vector3 position, out IMapArea area)
        {
            area = null;

            foreach (IMapArea candidate in _all)
            {
                if (!IsAlive(candidate) || !candidate.Contains(position))
                {
                    continue;
                }

                if (area == null || candidate.Depth > area.Depth || (candidate.Depth == area.Depth && candidate.Footprint < area.Footprint))
                {
                    area = candidate;
                }
            }

            return area != null;
        }

        public static string ResolveId(Vector3 position)
        {
            return TryResolve(position, out IMapArea area) ? area.AreaId : null;
        }

        // "김씨네 집" inside an area, "우물가 근처" within NEAR_DISTANCE of one, null otherwise
        public static string Describe(Vector3 position)
        {
            if (TryResolve(position, out IMapArea area))
            {
                return area.DisplayName;
            }

            IMapArea nearest = null;
            float nearestSqr = NEAR_DISTANCE * NEAR_DISTANCE;

            foreach (IMapArea candidate in _all)
            {
                if (!IsAlive(candidate))
                {
                    continue;
                }

                Vector3 offset = candidate.Center - position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;

                if (sqr < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = sqr;
                }
            }

            return nearest != null ? $"{nearest.DisplayName} 근처" : null;
        }

        public static string NameOf(string areaId)
        {
            return TryGet(areaId, out IMapArea area) ? area.DisplayName : null;
        }

        private static bool IsAlive(IMapArea area)
        {
            return area != null && !(area is Object unityObject && unityObject == null);
        }

        private static string Describe(IMapArea area)
        {
            return area is Component component && component != null ? component.name : "null";
        }
    }
}
