using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.Map;

namespace ProjectShaman.AI.World
{
    // Work/rest places are map areas too, so positions inside them resolve to the place name
    public class PlaceArea : MonoBehaviour, IMapArea, IVillagerInteractable
    {
        private const float NAVMESH_SAMPLE_DISTANCE = 3f;
        private const int RANDOM_POINT_ATTEMPTS = 12;
        private const float STATION_CLEARANCE = 1.5f;
        private const float LOOK_HEIGHT = 1.2f;
        private const float NEAR_AREA_DISTANCE = 12f;

        private static InteractionProfile _visitProfile;

        [SerializeField] private string _placeId;
        [SerializeField] private string _displayName;
        // Places of one kind spread over the scene (tree shade in a tree prefab): a routine naming the group goes to
        // the nearest of them. A member may leave PlaceId empty, it gets one from its position (same on every PC),
        // and its name is prefixed with the area it sits in ("밭 나무 그늘")
        [SerializeField] private string _group;
        [SerializeField] private PlaceType _placeType = PlaceType.Work;
        [SerializeField] private PlaceShape _shape = PlaceShape.Sphere;
        [SerializeField] private float _radius = 2f;
        [SerializeField] private Vector3 _boxSize = new Vector3(4f, 2f, 4f);
        [SerializeField] private int _maxPeople = 1;

        public string PlaceId => _placeId;
        // Runtime only: who lives here. Never serialized, assigned when houses are handed out (server) and replayed on clients.
        [NonSerialized] private string _ownerId;

        public string OwnerId => _ownerId;

        public string DisplayName
        {
            get
            {
                string baseName = string.IsNullOrEmpty(_displayName) ? (IsGroupMember ? _group : _placeId) : _displayName;
                if (IsGroupMember)
                {
                    string areaName = SurroundingAreaName();
                    return string.IsNullOrEmpty(areaName) ? baseName : $"{areaName} {baseName}";
                }

                string ownerName = OwnerName();
                return string.IsNullOrEmpty(ownerName) ? baseName : $"{ownerName}네 {baseName}";
            }
        }

        public bool IsGroupMember => !string.IsNullOrEmpty(_group);
        // A place outside any group is a group of its own
        public string Group => IsGroupMember ? _group : _placeId;
        // The group's own name for a routine that names it ("나무 그늘"), without the area prefix
        public string GroupDisplayName => string.IsNullOrEmpty(_displayName) ? Group : _displayName;
        public PlaceType Type => _placeType;
        public int MaxPeople => _maxPeople;
        public Vector3 Center => transform.position;
        public string AreaId => _placeId;
        public float Footprint => _shape == PlaceShape.Sphere ? Mathf.PI * _radius * _radius : Mathf.Abs(_boxSize.x * _boxSize.z);
        public int Depth => _depth >= 0 ? _depth : (_depth = MapAreaRegistry.CountParents(transform));

        private int _depth = -1;

        private WorkStation[] _stations;

        public IReadOnlyList<WorkStation> Stations
        {
            get
            {
                if (_stations == null)
                {
                    _stations = GetComponentsInChildren<WorkStation>();
                    WarnDuplicateStationIds();
                }

                return _stations;
            }
        }

        public int Capacity => Stations.Count > 0 ? Stations.Count : Mathf.Max(1, _maxPeople);

        public bool OwnsStation(WorkStation station)
        {
            foreach (WorkStation owned in Stations)
            {
                if (owned == station)
                {
                    return true;
                }
            }

            return false;
        }

        // Only a station that takes this action: carrying firewood at a chopping block would only be rejected hit after hit (C113)
        public bool TryReserveStation(string occupantId, string actionId, out WorkStation reserved)
        {
            reserved = FindAvailable(actionId);
            return reserved != null && reserved.TryReserve(occupantId);
        }

        // False: no station here takes this action at all, so the work is done anywhere in the place instead of waiting
        public bool HasStationFor(string actionId)
        {
            foreach (WorkStation station in Stations)
            {
                if (station != null && station.Accepts(actionId))
                {
                    return true;
                }
            }

            return false;
        }

        private WorkStation FindAvailable(string actionId)
        {
            foreach (WorkStation station in Stations)
            {
                if (station != null && station.IsAvailable && station.Accepts(actionId))
                {
                    return station;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            _depth = -1;
            if (IsGroupMember && string.IsNullOrEmpty(_placeId))
            {
                Vector3 position = transform.position;
                _placeId = $"{_group}_{Mathf.RoundToInt(position.x)}_{Mathf.RoundToInt(position.z)}";
            }

            PlaceRegistry.Register(this);
            MapAreaRegistry.Register(this);
        }

        private void OnDisable()
        {
            PlaceRegistry.Unregister(this);
            MapAreaRegistry.Unregister(this);
        }

        public Vector3 GetRandomPoint(System.Random random)
        {
            for (int i = 0; i < RANDOM_POINT_ATTEMPTS; i++)
            {
                Vector3 candidate = Center + GetRandomOffset(random);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) && IsClearOfStations(hit.position))
                {
                    return hit.position;
                }
            }

            return Center;
        }

        // A neighbour looks in on a house on the way home (a Home place is only ever approached on purpose, never scanned)
        public string InteractableId => $"House_{_placeId}";
        public InteractionProfile Profile => _visitProfile != null ? _visitProfile : (_visitProfile = CreateVisitProfile());
        public Vector3 LookPoint => Center + Vector3.up * LOOK_HEIGHT;

        public bool CanInteract(in InteractionContext context)
        {
            return true;
        }

        public void BeginInteraction(in InteractionContext context, InteractionResponse response)
        {
        }

        public InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason)
        {
            return InteractionResult.None(InteractableId, InteractionKind.Conversation, response);
        }

        private static InteractionProfile CreateVisitProfile()
        {
            InteractionProfile profile = ScriptableObject.CreateInstance<InteractionProfile>();
            profile.name = "HouseVisit";
            profile.hideFlags = HideFlags.DontSave;
            return profile;
        }

        public void SetOwner(string villagerId)
        {
            _ownerId = villagerId;
        }

        // Where a resident stands/spawns: the centre, pulled onto the NavMesh
        public Vector3 GetHomePoint()
        {
            return NavMesh.SamplePosition(Center, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) ? hit.position : Center;
        }

        // The area around a group member (not itself or another of its group): the one it is inside, or one within
        // NEAR_AREA_DISTANCE as "○○ 근처"; null when it stands alone
        private string SurroundingAreaName()
        {
            IMapArea inside = null;
            IMapArea nearest = null;
            float nearestSqr = NEAR_AREA_DISTANCE * NEAR_AREA_DISTANCE;

            foreach (IMapArea area in MapAreaRegistry.All)
            {
                if (area == null || ReferenceEquals(area, this) || (area is UnityEngine.Object unityObject && unityObject == null)
                    || (area is PlaceArea place && place.IsGroupMember && place._group == _group))
                {
                    continue;
                }

                if (area.Contains(Center))
                {
                    if (inside == null || area.Depth > inside.Depth || (area.Depth == inside.Depth && area.Footprint < inside.Footprint))
                    {
                        inside = area;
                    }

                    continue;
                }

                Vector3 offset = area.Center - Center;
                offset.y = 0f;
                if (offset.sqrMagnitude < nearestSqr)
                {
                    nearest = area;
                    nearestSqr = offset.sqrMagnitude;
                }
            }

            if (inside != null)
            {
                return inside.DisplayName;
            }

            return nearest != null ? $"{nearest.DisplayName} 근처" : null;
        }

        private string OwnerName()
        {
            if (string.IsNullOrEmpty(_ownerId))
            {
                return null;
            }

            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            return registry != null && registry.TryGet(_ownerId, out IVillagerView view) && !string.IsNullOrEmpty(view.DisplayName) ? view.DisplayName : null;
        }

        public Vector3 GetWaitingPoint()
        {
            Vector3 best = Center;
            float bestClearance = -1f;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 edge = Center + GetEdgeOffset(direction);

                if (!NavMesh.SamplePosition(edge, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    continue;
                }

                float clearance = GetStationClearance(hit.position);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    best = hit.position;
                }
            }

            return best;
        }

        private Vector3 GetEdgeOffset(Vector3 direction)
        {
            if (_shape == PlaceShape.Sphere)
            {
                return direction * _radius;
            }

            Vector3 half = _boxSize * 0.5f;
            float scale = Mathf.Min(
                Mathf.Abs(direction.x) > 0.001f ? half.x / Mathf.Abs(direction.x) : float.MaxValue,
                Mathf.Abs(direction.z) > 0.001f ? half.z / Mathf.Abs(direction.z) : float.MaxValue);
            return transform.rotation * (direction * scale);
        }

        private float GetStationClearance(Vector3 position)
        {
            float min = float.MaxValue;

            foreach (WorkStation station in Stations)
            {
                if (station != null)
                {
                    min = Mathf.Min(min, Vector3.Distance(position, station.StandPosition));
                }
            }

            return min;
        }

        private bool IsClearOfStations(Vector3 position)
        {
            foreach (WorkStation station in Stations)
            {
                if (station != null && Vector3.Distance(position, station.StandPosition) < STATION_CLEARANCE)
                {
                    return false;
                }
            }

            return true;
        }

        private void WarnDuplicateStationIds()
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (WorkStation station in _stations)
            {
                if (station != null && !ids.Add(station.StationId))
                {
                    ProjectShaman.AI.Core.AILog.Warn(ProjectShaman.AI.Core.AILog.PLACE, $"{_placeId} has duplicate StationId '{station.StationId}' ({station.name})");
                }
            }
        }

        public bool Contains(Vector3 position)
        {
            Vector3 local = position - Center;

            if (_shape == PlaceShape.Sphere)
            {
                local.y = 0f;
                return local.sqrMagnitude <= _radius * _radius;
            }

            Vector3 half = _boxSize * 0.5f;
            Vector3 rotated = Quaternion.Inverse(transform.rotation) * local;
            return Mathf.Abs(rotated.x) <= half.x && Mathf.Abs(rotated.z) <= half.z;
        }

        private Vector3 GetRandomOffset(System.Random random)
        {
            float u = (float)random.NextDouble();
            float v = (float)random.NextDouble();

            if (_shape == PlaceShape.Sphere)
            {
                float angle = u * Mathf.PI * 2f;
                float distance = Mathf.Sqrt(v) * _radius;
                return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            }

            Vector3 half = _boxSize * 0.5f;
            Vector3 local = new Vector3(Mathf.Lerp(-half.x, half.x, u), 0f, Mathf.Lerp(-half.z, half.z, v));
            return transform.rotation * local;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_placeId))
            {
                // A group member gets its id from its position at runtime
                if (!IsGroupMember)
                {
                    Debug.LogWarning($"[PlaceArea] '{name}' has no PlaceId", this);
                }

                return;
            }

            foreach (PlaceArea other in FindObjectsByType<PlaceArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other != this && other.gameObject.scene == gameObject.scene && other._placeId == _placeId)
                {
                    Debug.LogWarning($"[PlaceArea] Duplicate PlaceId '{_placeId}' on '{name}' and '{other.name}'", this);
                }
            }
        }
#endif

        private void OnDrawGizmos()
        {
            Gizmos.color = GetGizmoColor();

            if (_shape == PlaceShape.Sphere)
            {
                Gizmos.DrawWireSphere(Center, _radius);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(Center, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, _boxSize);
                Gizmos.matrix = Matrix4x4.identity;
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(Center + Vector3.up * 1.5f, $"{DisplayName}\n<{_placeId}>");
#endif
        }

        private Color GetGizmoColor()
        {
            switch (_placeType)
            {
                case PlaceType.Work:
                    return new Color(1f, 0.6f, 0.1f);
                case PlaceType.Rest:
                    return new Color(0.2f, 0.8f, 0.3f);
                case PlaceType.Home:
                    return new Color(0.3f, 0.5f, 1f);
                case PlaceType.Area:
                    return new Color(0.3f, 0.8f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
