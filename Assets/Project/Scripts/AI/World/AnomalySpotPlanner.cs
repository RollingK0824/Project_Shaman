using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.World
{
    // [Server] Where a ghost may leave an anomaly, worked out at run time from the houses, so nothing is placed in the scene:
    // per house the door nearest its yard (a bloody handprint at hand height on its outer face) and the yard itself
    // (blood or filth on the ground). Built once, the first night (NightHuntDirector)
    public static class AnomalySpotPlanner
    {
        private const string ROOT_NAME = "AnomalySpots (runtime)";
        // A house's door is this close to its yard point at most
        private const float MAX_DOOR_FROM_YARD = 15f;
        private const float HAND_HEIGHT = 1.3f;
        private const float DOOR_SURFACE_GAP = 0.05f;
        private const float YARD_SAMPLE_DISTANCE = 3f;

        private static GameObject _root;

        public static bool IsBuilt => _root != null;

        public static void Build()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject(ROOT_NAME);
            List<PlaceArea> homes = new List<PlaceArea>();
            foreach (PlaceArea place in Object.FindObjectsByType<PlaceArea>(FindObjectsSortMode.None))
            {
                if (place.Type == PlaceType.Home)
                {
                    homes.Add(place);
                }
            }

            NetDoor[] doors = Object.FindObjectsByType<NetDoor>(FindObjectsSortMode.None);
            int doorCount = 0;
            int yardCount = 0;

            foreach (PlaceArea home in homes)
            {
                Vector3 yard = home.GetHomePoint();

                NetDoor door = Nearest(doors, yard);
                if (door != null && (door.transform.position - yard).sqrMagnitude <= MAX_DOOR_FROM_YARD * MAX_DOOR_FROM_YARD
                    && TryDoorSpot(door, home, out Vector3 position, out Quaternion rotation))
                {
                    AddSpot($"Door_{home.PlaceId}", position, rotation, AnomalySpot.HANDPRINT);
                    doorCount++;
                }

                if (NavMesh.SamplePosition(yard, out NavMeshHit hit, YARD_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    AddSpot($"Yard_{home.PlaceId}", hit.position, Quaternion.identity, AnomalySpot.BLOOD, AnomalySpot.FILTH);
                    yardCount++;
                }
            }

            AILog.Log(AILog.GHOST, $"Anomaly spots built: {doorCount} door(s), {yardCount} yard(s)");
        }

        private static NetDoor Nearest(NetDoor[] doors, Vector3 point)
        {
            NetDoor best = null;
            float bestSqr = float.MaxValue;
            foreach (NetDoor door in doors)
            {
                float sqr = (door.transform.position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = door;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        // Hand height on the door's outer face: its own flat axis pointing most away from the house
        private static bool TryDoorSpot(NetDoor door, PlaceArea house, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers)
            {
                bounds.Encapsulate(part.bounds);
            }

            Vector3 away = bounds.center - house.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : door.transform.forward;

            Vector3 outward = door.transform.forward;
            float best = float.MinValue;
            foreach (Vector3 axis in new[] { door.transform.forward, -door.transform.forward, door.transform.right, -door.transform.right })
            {
                Vector3 flat = new Vector3(axis.x, 0f, axis.z).normalized;
                float dot = Vector3.Dot(flat, away);
                if (dot > best)
                {
                    best = dot;
                    outward = flat;
                }
            }

            float halfDepth = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;
            position = new Vector3(bounds.center.x, Mathf.Min(bounds.center.y, bounds.min.y + HAND_HEIGHT), bounds.center.z) + outward * (halfDepth + DOOR_SURFACE_GAP);
            rotation = Quaternion.LookRotation(outward);
            return true;
        }

        private static void AddSpot(string name, Vector3 position, Quaternion rotation, params string[] kinds)
        {
            GameObject spot = new GameObject(name);
            spot.SetActive(false);
            spot.transform.SetParent(_root.transform, false);
            spot.transform.SetPositionAndRotation(position, rotation);
            spot.AddComponent<AnomalySpot>().SetKinds(kinds);
            // Registered with the ghost targets on enable, with its kinds already set
            spot.SetActive(true);
        }
    }
}
