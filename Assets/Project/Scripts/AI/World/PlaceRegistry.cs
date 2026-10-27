using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.World
{
    public static class PlaceRegistry
    {
        private static readonly Dictionary<string, PlaceArea> _places = new Dictionary<string, PlaceArea>();

        public static int Count => _places.Count;

        public static void Register(PlaceArea place)
        {
            if (place == null || string.IsNullOrEmpty(place.PlaceId))
            {
                AILog.Warn(AILog.PLACE, $"PlaceArea without PlaceId ignored: {(place != null ? place.name : "null")}");
                return;
            }

            if (_places.TryGetValue(place.PlaceId, out PlaceArea existing) && existing != place)
            {
                AILog.Warn(AILog.PLACE, $"Duplicate PlaceId '{place.PlaceId}' on {place.name}, kept {existing.name}");
                return;
            }

            _places[place.PlaceId] = place;
        }

        public static void Unregister(PlaceArea place)
        {
            if (place != null && !string.IsNullOrEmpty(place.PlaceId) && _places.TryGetValue(place.PlaceId, out PlaceArea existing) && existing == place)
            {
                _places.Remove(place.PlaceId);
            }
        }

        // The most specific place around a position: deepest in the hierarchy, then the smallest footprint
        public static bool TryFindContaining(Vector3 position, out PlaceArea place)
        {
            place = null;

            foreach (PlaceArea candidate in _places.Values)
            {
                if (candidate == null || !candidate.Contains(position))
                {
                    continue;
                }

                if (place == null || candidate.Depth > place.Depth || (candidate.Depth == place.Depth && candidate.Footprint < place.Footprint))
                {
                    place = candidate;
                }
            }

            return place != null;
        }

        // Houses and the like, in a stable order (hierarchy path) so every run hands them out the same way
        public static void CollectByType(PlaceType type, List<PlaceArea> results)
        {
            results.Clear();

            foreach (PlaceArea place in _places.Values)
            {
                if (place != null && place.Type == type)
                {
                    results.Add(place);
                }
            }

            results.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
        }

        public static void ClearOwners()
        {
            foreach (PlaceArea place in _places.Values)
            {
                if (place != null)
                {
                    place.SetOwner(null);
                }
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        public static bool TryGet(string placeId, out PlaceArea place)
        {
            place = null;
            return !string.IsNullOrEmpty(placeId) && _places.TryGetValue(placeId, out place);
        }

        // A routine's place: the place itself, or of a group the member nearest to `near` (a villager, its home)
        public static bool TryPick(string placeOrGroupId, Vector3 near, out PlaceArea place)
        {
            if (TryGet(placeOrGroupId, out place))
            {
                return true;
            }

            place = null;
            if (string.IsNullOrEmpty(placeOrGroupId))
            {
                return false;
            }

            float bestSqr = float.MaxValue;
            foreach (PlaceArea candidate in _places.Values)
            {
                if (candidate == null || !candidate.IsGroupMember || candidate.Group != placeOrGroupId)
                {
                    continue;
                }

                Vector3 offset = candidate.Center - near;
                offset.y = 0f;
                if (offset.sqrMagnitude < bestSqr)
                {
                    place = candidate;
                    bestSqr = offset.sqrMagnitude;
                }
            }

            return place != null;
        }

        public static bool IsGroup(string placeOrGroupId)
        {
            return !string.IsNullOrEmpty(placeOrGroupId) && !_places.ContainsKey(placeOrGroupId) && CountGroup(placeOrGroupId) > 0;
        }

        public static int CountGroup(string group)
        {
            int count = 0;
            foreach (PlaceArea place in _places.Values)
            {
                if (place != null && place.IsGroupMember && place.Group == group)
                {
                    count++;
                }
            }

            return count;
        }

        // How many a routine there can take at once: the place's own capacity, or every member's together. 0 = nowhere
        public static int CapacityOf(string placeOrGroupId)
        {
            if (TryGet(placeOrGroupId, out PlaceArea place))
            {
                return place.Capacity;
            }

            int capacity = 0;
            foreach (PlaceArea candidate in _places.Values)
            {
                if (candidate != null && candidate.IsGroupMember && candidate.Group == placeOrGroupId)
                {
                    capacity += candidate.Capacity;
                }
            }

            return capacity;
        }

        // A name for the schedule tab: the place's, or the group's own ("나무 그늘")
        public static string NameOf(string placeOrGroupId)
        {
            if (TryGet(placeOrGroupId, out PlaceArea place))
            {
                return place.DisplayName;
            }

            foreach (PlaceArea candidate in _places.Values)
            {
                if (candidate != null && candidate.IsGroupMember && candidate.Group == placeOrGroupId)
                {
                    return candidate.GroupDisplayName;
                }
            }

            return null;
        }

        // "Shade=12, CropField=2" for the start-up log; empty when the scene has no groups
        public static string DescribeGroups()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (PlaceArea place in _places.Values)
            {
                if (place != null && place.IsGroupMember)
                {
                    counts[place.Group] = counts.TryGetValue(place.Group, out int count) ? count + 1 : 1;
                }
            }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                parts.Add($"{pair.Key}={pair.Value}");
            }

            return string.Join(", ", parts);
        }
    }
}
