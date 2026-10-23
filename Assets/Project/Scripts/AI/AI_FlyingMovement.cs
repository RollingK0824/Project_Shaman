using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    public class AI_FlyingMovement : MonoBehaviour, INetworkMovable
    {
        private static readonly List<AI_FlyingMovement> _active = new List<AI_FlyingMovement>();

        [SerializeField, Min(0.1f)] private float _defaultSpeed = 3f;
        [SerializeField, Min(0f)] private float _arriveDistance = 0.5f;
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Header("Height")]
        [SerializeField] private float _hoverHeight = 0.8f;
        [SerializeField, Min(0f)] private float _heightFollowRate = 4f;
        [SerializeField, Min(0f)] private float _matchTargetHeightDistance = 4f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.15f;
        [SerializeField, Min(0f)] private float _bobFrequency = 1.5f;

        [Header("Separation")]
        [SerializeField, Min(0f)] private float _separationRadius = 1.2f;
        [SerializeField, Min(0f)] private float _separationStrength = 1.5f;

        [Header("Runtime")]
        [SerializeField] private bool _isActive;
        [SerializeField] private bool _isStopped = true;
        [SerializeField] private Vector3 _destination;
        [SerializeField] private float _speed;

        private Vector3 _velocity;
        private float _baseHeight;
        private float _bobPhase;
        private bool _hasDestination;

        public bool IsStopped => _isStopped;
        public bool HasReachedDestination => _hasDestination && FlatDistance(transform.position, _destination) <= _arriveDistance;
        public float CurrentSpeed => _velocity.magnitude;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _active.Clear();
        }

        private void Awake()
        {
            _speed = _defaultSpeed;
            _baseHeight = transform.position.y - _hoverHeight;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        public void SetAgentActive(bool active)
        {
            _isActive = active;

            if (active)
            {
                if (!_active.Contains(this))
                {
                    _active.Add(this);
                }

                _baseHeight = GroundHeight(transform.position, transform.position.y - _hoverHeight);
            }
            else
            {
                _active.Remove(this);
                Stop();
            }
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.1f, speed);
        }

        public void MoveTo(Vector3 destination)
        {
            _destination = destination;
            _hasDestination = true;
            _isStopped = false;
        }

        public void Stop()
        {
            _isStopped = true;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 desired = Vector3.zero;

            if (!_isStopped && _hasDestination)
            {
                Vector3 toTarget = _destination - position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                if (distance > _arriveDistance)
                {
                    desired = toTarget / distance * Mathf.Min(_speed, distance / Mathf.Max(deltaTime, 0.0001f));
                }
            }

            desired += Separation(position) * _separationStrength;
            _velocity = desired;
            position += _velocity * deltaTime;
            position.y = NextHeight(position, deltaTime);
            transform.position = position;

            Vector3 facing = new Vector3(_velocity.x, 0f, _velocity.z);
            if (facing.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), _turnSpeed * deltaTime);
            }
        }

        private float NextHeight(Vector3 position, float deltaTime)
        {
            float ground = GroundHeight(position, _baseHeight);

            if (_hasDestination && _matchTargetHeightDistance > 0f)
            {
                float closeness = 1f - Mathf.Clamp01(FlatDistance(position, _destination) / _matchTargetHeightDistance);
                ground = Mathf.Lerp(ground, _destination.y, closeness);
            }

            _baseHeight = ground;
            float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f + _bobPhase) * _bobAmplitude;
            float target = ground + _hoverHeight + bob;
            return Mathf.Lerp(position.y, target, 1f - Mathf.Exp(-_heightFollowRate * deltaTime));
        }

        private Vector3 Separation(Vector3 position)
        {
            if (_separationRadius <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 push = Vector3.zero;
            float sqrRadius = _separationRadius * _separationRadius;

            foreach (AI_FlyingMovement other in _active)
            {
                if (other == this || other == null)
                {
                    continue;
                }

                Vector3 away = position - other.transform.position;
                away.y = 0f;
                float sqr = away.sqrMagnitude;

                if (sqr < sqrRadius && sqr > 0.0001f)
                {
                    float distance = Mathf.Sqrt(sqr);
                    push += away / distance * (1f - distance / _separationRadius);
                }
            }

            return push;
        }

        private static float GroundHeight(Vector3 position, float fallback)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;

                if (position.x >= origin.x && position.x <= origin.x + size.x && position.z >= origin.z && position.z <= origin.z + size.z)
                {
                    return origin.y + terrain.SampleHeight(position);
                }
            }

            return fallback;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            if (_hasDestination && !_isStopped)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _destination);
            }
        }
    }
}
