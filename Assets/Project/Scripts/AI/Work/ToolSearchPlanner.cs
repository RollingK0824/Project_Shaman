using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public static class ToolSearchPlanner
    {
        // Known sightings first, then the guesses by distance (work place, use places, the tool's own sheds),
        // then every other shed in the village, each next one the nearest to the last.
        // Spots in `excluded` (already looked over this routine) are left out of the guesses;
        // `storages` receives every shed centre so the caller can tell a shed from other spots
        public static void BuildCandidates(
            string toolTypeId,
            IReadOnlyList<ToolSighting> sightings,
            string currentPlaceId,
            IReadOnlyDictionary<string, List<string>> toolUsePlaces,
            Vector3 origin,
            float mergeDistance,
            float now,
            float freshHeldWindow,
            IReadOnlyList<Vector3> excluded,
            List<Vector3> result,
            List<Vector3> storages)
        {
            result.Clear();
            storages.Clear();

            foreach (ToolSighting sighting in sightings)
            {
                float elapsed = now - sighting.SeenTime;
                bool isSeenInUseJustNow = !string.IsNullOrEmpty(sighting.HolderId) && elapsed >= 0f && elapsed <= freshHeldWindow;
                if (isSeenInUseJustNow)
                {
                    continue;
                }

                AddUnique(result, sighting.Position, mergeDistance);
            }

            int knownCount = result.Count;
            List<Vector3> guesses = new List<Vector3>();

            if (PlaceRegistry.TryGet(currentPlaceId, out PlaceArea currentPlace))
            {
                AddGuess(guesses, result, excluded, currentPlace.Center, mergeDistance);
            }

            if (toolUsePlaces != null && toolUsePlaces.TryGetValue(toolTypeId, out List<string> usePlaces))
            {
                foreach (string placeId in usePlaces)
                {
                    if (PlaceRegistry.TryGet(placeId, out PlaceArea usePlace))
                    {
                        AddGuess(guesses, result, excluded, usePlace.Center, mergeDistance);
                    }
                }
            }

            ToolRegistry.CollectStoragePositions(toolTypeId, storages);

            foreach (Vector3 storage in storages)
            {
                AddGuess(guesses, result, excluded, storage, mergeDistance);
            }

            guesses.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));
            result.AddRange(guesses);

            List<PlaceArea> sheds = new List<PlaceArea>();
            PlaceRegistry.CollectByType(PlaceType.Storage, sheds);
            List<Vector3> otherSheds = new List<Vector3>();

            foreach (PlaceArea shed in sheds)
            {
                AddUnique(storages, shed.Center, mergeDistance);
                AddGuess(otherSheds, result, excluded, shed.Center, mergeDistance);
            }

            Vector3 from = result.Count > knownCount ? result[result.Count - 1] : origin;
            while (otherSheds.Count > 0)
            {
                int nearest = 0;
                for (int i = 1; i < otherSheds.Count; i++)
                {
                    if ((otherSheds[i] - from).sqrMagnitude < (otherSheds[nearest] - from).sqrMagnitude)
                    {
                        nearest = i;
                    }
                }

                from = otherSheds[nearest];
                result.Add(from);
                otherSheds.RemoveAt(nearest);
            }
        }

        public static bool IsNear(IReadOnlyList<Vector3> list, Vector3 position, float mergeDistance)
        {
            foreach (Vector3 existing in list)
            {
                if (Vector3.Distance(existing, position) < mergeDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddGuess(List<Vector3> guesses, List<Vector3> result, IReadOnlyList<Vector3> excluded, Vector3 position, float mergeDistance)
        {
            if ((excluded != null && IsNear(excluded, position, mergeDistance)) || IsNear(result, position, mergeDistance))
            {
                return;
            }

            AddUnique(guesses, position, mergeDistance);
        }

        private static void AddUnique(List<Vector3> list, Vector3 position, float mergeDistance)
        {
            if (!IsNear(list, position, mergeDistance))
            {
                list.Add(position);
            }
        }
    }
}
