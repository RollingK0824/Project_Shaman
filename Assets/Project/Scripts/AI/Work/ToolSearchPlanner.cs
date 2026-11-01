using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public static class ToolSearchPlanner
    {
        // 1st the sheds: the tool's own sheds nearest first, then every other shed in the village, each next one the nearest
        // to the last. 2nd the memory: where the tool was seen or someone said it was (asking around adds to it).
        // Spots in `excluded` (already looked over this routine) are left out; `storages` receives every shed centre
        public static void BuildCandidates(
            string toolTypeId,
            IReadOnlyList<ToolSighting> sightings,
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

            List<Vector3> ownSheds = new List<Vector3>();
            ToolRegistry.CollectStoragePositions(toolTypeId, storages);
            foreach (Vector3 storage in storages)
            {
                AddGuess(ownSheds, result, excluded, storage, mergeDistance);
            }

            ownSheds.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));
            result.AddRange(ownSheds);

            List<PlaceArea> sheds = new List<PlaceArea>();
            PlaceRegistry.CollectByType(PlaceType.Storage, sheds);
            List<Vector3> otherSheds = new List<Vector3>();

            foreach (PlaceArea shed in sheds)
            {
                AddUnique(storages, shed.Center, mergeDistance);
                AddGuess(otherSheds, result, excluded, shed.Center, mergeDistance);
            }

            Vector3 from = result.Count > 0 ? result[result.Count - 1] : origin;
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

            // The caller passes only sightings not looked over since, so a shed someone named again is gone back to
            foreach (ToolSighting sighting in sightings)
            {
                float elapsed = now - sighting.SeenTime;
                bool isSeenInUseJustNow = !string.IsNullOrEmpty(sighting.HolderId) && elapsed >= 0f && elapsed <= freshHeldWindow;
                if (!isSeenInUseJustNow)
                {
                    AddUnique(result, sighting.Position, mergeDistance);
                }
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
