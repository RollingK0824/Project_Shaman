using System.Collections.Generic;
using UnityEngine;
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

        public static bool TryFindContaining(Vector3 position, out PlaceArea place)
        {
            place = null;

            foreach (PlaceArea candidate in _places.Values)
            {
                if (candidate != null && candidate.Contains(position))
                {
                    place = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGet(string placeId, out PlaceArea place)
        {
            place = null;
            return !string.IsNullOrEmpty(placeId) && _places.TryGetValue(placeId, out place);
        }
    }
}
