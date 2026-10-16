using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.Mock
{
    public class TestPlayerDummy : MonoBehaviour, IGhostTarget, IDamageable
    {
        public enum MoveMode
        {
            Manual,
            Waypoints,
            Random
        }

        private const float CORNER_REACHED_DISTANCE = 0.3f;
        private const float NAVMESH_SAMPLE_DISTANCE = 3f;
        private const float GROUND_SNAP_DISTANCE = 20f;
        private const int RANDOM_ATTEMPTS = 8;

        [SerializeField] private MoveMode _mode = MoveMode.Manual;
        [SerializeField] private bool _snapToNavMesh = true;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _moveSpeed = 2.5f;
        [SerializeField] private float _randomRadius = 30f;
        [SerializeField] private float _pauseSeconds = 2f;

        [Header("Runtime")]
        [SerializeField] private int _hitCount;
        [SerializeField] private float _totalDamage;

        private NavMeshPath _currentPath;
        private int _cornerIndex;
        private int _waypointIndex;
        private float _resumeTime;
        private Vector3 _origin;
        private System.Random _random;

        public GhostTargetKind Kind => GhostTargetKind.Player;
        public bool IsDead => false;

        private void Awake()
        {
            _currentPath = new NavMeshPath();
            _origin = transform.position;
            _random = new System.Random(GetInstanceID());
        }

        private void OnEnable()
        {
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            GhostTargetRegistry.Unregister(this);
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            return context.Verb == GhostActionVerb.Attack;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        public void ReceiveDamage(float amount, GameObject source = null)
        {
            _hitCount++;
            _totalDamage += amount;
            AILog.Log(AILog.GHOST, name, $"TestPlayerDummy hit {amount:F1} ({DamageTypeTag.Resolve(source)}) by {(source != null ? source.name : "unknown")}, total {_totalDamage:F1}");
        }

        private void Update()
        {
            if (_snapToNavMesh)
            {
                SnapToNavMesh();
            }

            if (_mode == MoveMode.Manual || Time.time < _resumeTime)
            {
                return;
            }

            if (_currentPath.status == NavMeshPathStatus.PathInvalid || _cornerIndex >= _currentPath.corners.Length)
            {
                PlanNextPath();
                return;
            }

            Vector3 corner = _currentPath.corners[_cornerIndex];
            transform.position = Vector3.MoveTowards(transform.position, corner, _moveSpeed * Time.deltaTime);

            Vector3 direction = corner - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            if ((transform.position - corner).sqrMagnitude <= CORNER_REACHED_DISTANCE * CORNER_REACHED_DISTANCE)
            {
                _cornerIndex++;

                if (_cornerIndex >= _currentPath.corners.Length)
                {
                    _resumeTime = Time.time + _pauseSeconds;
                }
            }
        }

        private void SnapToNavMesh()
        {
            Vector3 position = transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, GROUND_SNAP_DISTANCE, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - position.y) > 0.01f)
            {
                transform.position = new Vector3(position.x, hit.position.y, position.z);
            }
        }

        private void PlanNextPath()
        {
            if (!TryPickDestination(out Vector3 destination) || !NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, _currentPath))
            {
                _resumeTime = Time.time + _pauseSeconds;
                return;
            }

            _cornerIndex = 0;
        }

        private bool TryPickDestination(out Vector3 destination)
        {
            if (_mode == MoveMode.Waypoints)
            {
                if (_waypoints == null || _waypoints.Length == 0)
                {
                    destination = transform.position;
                    return false;
                }

                Transform waypoint = _waypoints[_waypointIndex % _waypoints.Length];
                _waypointIndex++;
                destination = waypoint != null ? waypoint.position : transform.position;
                return waypoint != null;
            }

            for (int i = 0; i < RANDOM_ATTEMPTS; i++)
            {
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Sqrt((float)_random.NextDouble()) * _randomRadius;
                Vector3 candidate = _origin + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                candidate.y = transform.position.y;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    destination = hit.position;
                    return true;
                }
            }

            destination = transform.position;
            return false;
        }
    }
}
