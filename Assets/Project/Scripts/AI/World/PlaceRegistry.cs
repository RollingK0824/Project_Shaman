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
    }
}
