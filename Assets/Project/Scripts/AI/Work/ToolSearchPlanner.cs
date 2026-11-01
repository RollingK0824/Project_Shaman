using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public static class ToolSearchPlanner
    {
        // 1st the tool's own shed (the nearest one when it has several), 2nd the memory: where the tool was seen or someone
        // said it was (asking around adds to it). Other sheds only come in as a lead: going round every shed took too long.
        // Spots in `excluded` (already looked over this routine) are left out; `storages` receives the tool's shed centres
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

            ToolRegistry.CollectStoragePositions(toolTypeId, storages);
            Vector3 nearestShed = origin;
            float nearestSqr = float.MaxValue;
            foreach (Vector3 storage in storages)
            {
                float sqr = (storage - origin).sqrMagnitude;
                if (sqr < nearestSqr && (excluded == null || !IsNear(excluded, storage, mergeDistance)))
                {
                    nearestShed = storage;
                    nearestSqr = sqr;
                }
            }

            if (nearestSqr < float.MaxValue)
            {
                result.Add(nearestShed);
            }

            // The caller passes only sightings not looked over since, so a shed someone named again is gone back to
            foreach (ToolSighting sighting in sightings)
            {
                float elapsed = now - sighting.SeenTime;
                // Carried: the carrier is asked instead (ToolSearchSession); only a ghost's or a player's carry leaves a spot
                bool isCarriedByVillager = !string.IsNullOrEmpty(sighting.HolderId) && !sighting.HolderId.StartsWith("GHOST_") && !sighting.HolderId.StartsWith("Player_");
                bool isSeenInUseJustNow = !string.IsNullOrEmpty(sighting.HolderId) && elapsed >= 0f && elapsed <= freshHeldWindow;
                if (!isCarriedByVillager && !isSeenInUseJustNow)
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

        private static void AddUnique(List<Vector3> list, Vector3 position, float mergeDistance)
        {
            if (!IsNear(list, position, mergeDistance))
            {
                list.Add(position);
            }
        }
    }
}
