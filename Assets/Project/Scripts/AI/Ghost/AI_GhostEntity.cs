using UnityEngine;
using Unity.Behavior;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Ghost
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_GhostEntity : MonoBehaviour
    {
        [Header("Runtime (server only)")]
        [SerializeField] private string _ghostId;
        [SerializeField] private string _hostVillagerId;
        [SerializeField] private Vector3 _center;
        [SerializeField] private float _radius;
        [SerializeField] private int _killsRemaining;
        [SerializeField] private float _killDistance;
        [SerializeField] private string _currentTargetId;

        private AI_Movement _movement;
        private BehaviorGraphAgent _behaviorAgent;
        private NightHuntDirector _director;

        public string GhostId => _ghostId;
        public Vector3 Center => _center;
        public float Radius => _radius;
        public bool CanHunt => _killsRemaining > 0;
        public float KillDistance => _killDistance;

        private void Awake()
        {
            _movement = GetComponent<AI_Movement>();
            _behaviorAgent = GetComponent<BehaviorGraphAgent>();
        }

        public void Initialize(NightHuntDirector director, string ghostId, string hostVillagerId, Vector3 center, float radius, int kills, float killDistance)
        {
            _director = director;
            _ghostId = ghostId;
            _hostVillagerId = hostVillagerId;
            _center = center;
            _radius = radius;
            _killsRemaining = kills;
            _killDistance = killDistance;
            AILog.Log(AILog.GHOST, ghostId, $"Entity initialized at {center} radius={radius:F1}m kills={kills}");
        }

        public void SetSimulationActive(bool active)
        {
            _movement.SetAgentActive(active);
            _behaviorAgent.enabled = active;
        }

        public bool IsInsideRadius(Vector3 position)
        {
            Vector3 offset = position - _center;
            offset.y = 0f;
            return offset.sqrMagnitude <= _radius * _radius;
        }

        public bool TrySelectTarget(out GameObject target)
        {
            target = null;

            if (!CanHunt || _director == null)
            {
                return false;
            }

            AI_Core selected = _director.FindHuntTarget(this);
            if (selected == null)
            {
                return false;
            }

            target = selected.gameObject;
            _currentTargetId = selected.LogId;
            AILog.Log(AILog.GHOST, _ghostId, $"Target selected {selected.LogId} at {selected.transform.position}");
            return true;
        }

        public bool IsTargetValid(GameObject target)
        {
            return target != null && CanHunt && _director != null && _director.IsHuntable(target.GetComponent<AI_Core>()) && IsInsideRadius(target.transform.position);
        }

        public bool TryKill(GameObject target)
        {
            if (!IsTargetValid(target))
            {
                return false;
            }

            _killsRemaining--;
            _currentTargetId = string.Empty;
            _director.Kill(this, target.GetComponent<AI_Core>());
            return true;
        }

        private void OnDrawGizmos()
        {
            if (_radius <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(_center, _radius);
        }
    }
}
