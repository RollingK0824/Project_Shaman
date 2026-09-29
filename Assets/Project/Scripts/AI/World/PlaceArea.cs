using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.World
{
    public class PlaceArea : MonoBehaviour
    {
        private const float NAVMESH_SAMPLE_DISTANCE = 3f;
        private const int RANDOM_POINT_ATTEMPTS = 12;
        private const float STATION_CLEARANCE = 1.5f;

        [SerializeField] private string _placeId;
        [SerializeField] private string _displayName;
        [SerializeField] private PlaceType _placeType = PlaceType.Work;
        [SerializeField] private PlaceShape _shape = PlaceShape.Sphere;
        [SerializeField] private float _radius = 2f;
        [SerializeField] private Vector3 _boxSize = new Vector3(4f, 2f, 4f);
        [SerializeField] private int _maxPeople = 1;

        public string PlaceId => _placeId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _placeId : _displayName;
        public PlaceType Type => _placeType;
        public int MaxPeople => _maxPeople;
        public Vector3 Center => transform.position;

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

        public bool TryReserveStation(string occupantId, string actionId, out WorkStation reserved)
        {
            reserved = FindAvailable(actionId, true) ?? FindAvailable(actionId, false);
            return reserved != null && reserved.TryReserve(occupantId);
        }

        private WorkStation FindAvailable(string actionId, bool requireAccept)
        {
            foreach (WorkStation station in Stations)
            {
                if (station != null && station.IsAvailable && (!requireAccept || station.Accepts(actionId)))
                {
                    return station;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            PlaceRegistry.Register(this);
        }

        private void OnDisable()
        {
            PlaceRegistry.Unregister(this);
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
                default:
                    return Color.gray;
            }
        }
    }
}
