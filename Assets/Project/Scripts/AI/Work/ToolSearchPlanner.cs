using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public static class ToolSearchPlanner
    {
        public static void BuildCandidates(
            string toolTypeId,
            IReadOnlyList<ToolSighting> sightings,
            string currentPlaceId,
            IReadOnlyDictionary<string, List<string>> toolUsePlaces,
            Vector3 origin,
            float mergeDistance,
            float now,
            float freshHeldWindow,
            List<Vector3> result)
        {
            result.Clear();

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

            if (PlaceRegistry.TryGet(currentPlaceId, out PlaceArea currentPlace))
            {
                AddUnique(result, currentPlace.Center, mergeDistance);
            }

            if (toolUsePlaces != null && toolUsePlaces.TryGetValue(toolTypeId, out List<string> usePlaces))
            {
                foreach (string placeId in usePlaces)
                {
                    if (PlaceRegistry.TryGet(placeId, out PlaceArea usePlace))
                    {
                        AddUnique(result, usePlace.Center, mergeDistance);
                    }
                }
            }

            List<Vector3> storages = new List<Vector3>();
            ToolRegistry.CollectStoragePositions(toolTypeId, storages);

            foreach (Vector3 storage in storages)
            {
                AddUnique(result, storage, mergeDistance);
            }

            List<Vector3> guesses = result.GetRange(knownCount, result.Count - knownCount);
            guesses.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));
            result.RemoveRange(knownCount, result.Count - knownCount);
            result.AddRange(guesses);
        }

        private static void AddUnique(List<Vector3> list, Vector3 position, float mergeDistance)
        {
            foreach (Vector3 existing in list)
            {
                if (Vector3.Distance(existing, position) < mergeDistance)
                {
                    return;
                }
            }

            list.Add(position);
        }
    }
}
