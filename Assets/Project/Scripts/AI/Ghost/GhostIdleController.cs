using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public class GhostIdleController
    {
        private const float TICK_INTERVAL = 0.5f;
        private const float NAVMESH_SAMPLE_DISTANCE = 3f;
        private const float DESTINATION_UPDATE_DISTANCE = 1f;
        private const float WANDER_ARRIVE_DISTANCE = 1.5f;
        private const float FACE_TURN_SPEED = 240f;
        private const int WANDER_ATTEMPTS = 8;

        private readonly AI_GhostEntity _owner;
        private readonly AI_Movement _movement;
        private readonly AIBehaviourConfig _config;
        private readonly System.Random _random;
        private readonly List<IGhostTarget> _players = new List<IGhostTarget>();

        private IGhostTarget _stalkTarget;
        private PlayerStressController _stalkStress;
        private Vector3 _destination;
        private bool _hasDestination;
        private float _nextTickTime;
        private float _wanderResumeTime;

        public GhostIdleController(AI_GhostEntity owner, AI_Movement movement, AIBehaviourConfig config, System.Random random)
        {
            _owner = owner;
            _movement = movement;
            _config = config;
            _random = random;
        }

        public string State { get; private set; } = "none";
        public IGhostTarget StalkTarget => _stalkTarget;

        public float DistanceToStalkTarget
        {
            get
            {
                if (!IsAlive(_stalkTarget))
                {
                    return float.MaxValue;
                }

                Vector3 offset = _stalkTarget.gameObject.transform.position - _owner.transform.position;
                offset.y = 0f;
                return offset.magnitude;
            }
        }

        public void Tick()
        {
            if (_stalkTarget != null)
            {
                FaceStalkTarget();
            }

            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + TICK_INTERVAL;

            IGhostTarget player = FindPlayerInRadius();
            if (player != null)
            {
                Stalk(player);
                return;
            }

            if (_stalkTarget != null)
            {
                StopStalking("player left radius");
                _hasDestination = false;
                _wanderResumeTime = Time.time;
            }

            Wander();
        }

        public void Stop(string reason)
        {
            StopStalking(reason);
            _hasDestination = false;
            State = "none";
            _movement.ResetSpeed();
        }

        private void Stalk(IGhostTarget player)
        {
            if (player != _stalkTarget)
            {
                StopStalking("switched target");
                StartStalking(player);
            }

            Vector3 ghostPosition = _owner.transform.position;
            Vector3 playerPosition = player.gameObject.transform.position;
            Vector3 offset = ghostPosition - playerPosition;
            offset.y = 0f;
            float distance = offset.magnitude;
            float keep = (_config.GhostStalkMinDistance + _config.GhostStalkMaxDistance) * 0.5f;

            if (distance <= keep)
            {
                State = "stalk watch";
                SetDestination(ghostPosition, false);
                return;
            }

            bool isFar = distance > _config.GhostStalkMaxDistance;
            State = isFar ? "stalk follow" : "stalk creep";
            _movement.SetSpeed(isFar ? _config.GhostStalkSpeed : _config.GhostStalkCreepSpeed);

            Vector3 direction = distance > 0.01f ? offset / distance : -_owner.transform.forward;
            Vector3 desired = playerPosition + direction * keep;
            desired.y = ghostPosition.y;

            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) && _owner.IsInsideRadius(hit.position))
            {
                SetDestination(hit.position, false);
            }
        }

        private void StartStalking(IGhostTarget player)
        {
            _stalkTarget = player;
            _stalkStress = player.gameObject.GetComponentInParent<PlayerStressController>();
            float rate = _config.GhostStalkStressPerSecond;

            if (_stalkStress != null)
            {
                _stalkStress.SetSourceRate(_owner, rate);
                AILog.Log(AILog.GHOST, _owner.GhostId, $"Stalking {player.gameObject.name} (stress +{rate}/s)");
            }
            else
            {
                AILog.Log(AILog.GHOST, _owner.GhostId, $"Stalking {player.gameObject.name} (no PlayerStressController, stress +{rate}/s skipped)");
            }
        }

        private void StopStalking(string reason)
        {
            if (_stalkTarget == null)
            {
                return;
            }

            if (_stalkStress != null)
            {
                _stalkStress.RemoveSource(_owner);
            }

            AILog.Log(AILog.GHOST, _owner.GhostId, $"Stalk ended ({reason})");
            _stalkTarget = null;
            _stalkStress = null;
        }

        private void Wander()
        {
            _movement.SetSpeed(_config.GhostWanderSpeed);

            if (_hasDestination && State == "wander")
            {
                Vector3 offset = _destination - _owner.transform.position;
                offset.y = 0f;

                if (offset.sqrMagnitude > WANDER_ARRIVE_DISTANCE * WANDER_ARRIVE_DISTANCE)
                {
                    return;
                }

                _hasDestination = false;
                _wanderResumeTime = Time.time + Mathf.Lerp(_config.GhostWanderPauseMin, _config.GhostWanderPauseMax, (float)_random.NextDouble());
            }

            State = "wander";

            if (_hasDestination || Time.time < _wanderResumeTime)
            {
                return;
            }

            if (TryPickWanderPoint(out Vector3 point))
            {
                SetDestination(point, true);
            }
        }

        private bool TryPickWanderPoint(out Vector3 point)
        {
            for (int i = 0; i < WANDER_ATTEMPTS; i++)
            {
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Sqrt((float)_random.NextDouble()) * _owner.Radius;
                Vector3 candidate = _owner.Center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) && _owner.IsInsideRadius(hit.position))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = _owner.transform.position;
            return false;
        }

        private void SetDestination(Vector3 destination, bool force)
        {
            Vector3 offset = destination - _destination;
            offset.y = 0f;

            if (!force && _hasDestination && offset.sqrMagnitude < DESTINATION_UPDATE_DISTANCE * DESTINATION_UPDATE_DISTANCE)
            {
                return;
            }

            _destination = destination;
            _hasDestination = true;
            _owner.SetIdleDestination(destination);
        }

        private IGhostTarget FindPlayerInRadius()
        {
            GhostTargetRegistry.CollectOfKind(GhostTargetKind.Player, _players);

            IGhostTarget best = null;
            float bestDistance = float.MaxValue;
            Vector3 from = _owner.transform.position;

            foreach (IGhostTarget player in _players)
            {
                Vector3 position = player.gameObject.transform.position;
                if (!_owner.IsInsideRadius(position))
                {
                    continue;
                }

                float distance = (position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = player;
                    bestDistance = distance;
                }
            }

            _players.Clear();
            return best;
        }

        private void FaceStalkTarget()
        {
            if (!IsAlive(_stalkTarget) || _movement.CurrentSpeed > 0.1f)
            {
                return;
            }

            Vector3 direction = _stalkTarget.gameObject.transform.position - _owner.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(direction);
            _owner.transform.rotation = Quaternion.RotateTowards(_owner.transform.rotation, look, FACE_TURN_SPEED * Time.deltaTime);
        }

        private static bool IsAlive(IGhostTarget target)
        {
            return target != null && !(target is Object unityObject && unityObject == null);
        }
    }
}
