using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_GhostEntity : MonoBehaviour
    {
        public const string BB_COMMAND_SERIAL = "CommandSerial";
        public const string BB_ACTION_SHAPE = "ActionShape";
        public const string BB_MOVE_POSITION = "MovePosition";
        public const string BB_ARRIVE_DISTANCE = "ArriveDistance";

        private const int HIDE_POSITION_ATTEMPTS = 12;
        private const float NAVMESH_SAMPLE_DISTANCE = 2f;
        private const float TARGET_CHECK_INTERVAL = 0.25f;
        private const float RANGE_TOLERANCE = 0.5f;
        private const float FOLLOW_UPDATE_DISTANCE = 0.5f;

        [SerializeField] private Transform _carrySocket;

        [Header("Runtime (server only)")]
        [SerializeField] private string _ghostId;
        [SerializeField] private string _hostVillagerId;
        [SerializeField] private Vector3 _center;
        [SerializeField] private float _radius;
        [SerializeField] private bool _isHunter;
        [SerializeField] private int _commandSerial;
        [SerializeField] private string _currentCommand;
        [SerializeField] private GhostActionShape _currentShape;
        [SerializeField] private Vector3 _movePosition;
        [SerializeField] private string _carriedName;

        private AI_Movement _movement;
        private BehaviorGraphAgent _behaviorAgent;
        private AI_Possession _possession;
        private NightOutcomeLedger _ledger;
        private AIBehaviourConfig _config;
        private System.Random _random;
        private readonly List<GhostNightActionData> _actions = new List<GhostNightActionData>();
        private readonly Dictionary<GhostNightActionData, int> _uses = new Dictionary<GhostNightActionData, int>();
        private readonly List<GhostNightActionData> _candidates = new List<GhostNightActionData>();
        private GhostActionSession _session;
        private bool _isSimulating;
        private float _nextDecisionTime;
        private float _nextTargetCheckTime;

        public string GhostId => _ghostId;
        public Vector3 Center => _center;
        public float Radius => _radius;
        public bool IsHunter => _isHunter;
        public GhostActionSession Session => _session;
        public Transform CarrySocket => _carrySocket != null ? _carrySocket : transform;

        private void Awake()
        {
            _movement = GetComponent<AI_Movement>();
            _behaviorAgent = GetComponent<BehaviorGraphAgent>();
        }

        public void Initialize(AI_Possession possession, NightOutcomeLedger ledger, IReadOnlyList<GhostNightActionData> actions, string hostVillagerId, Vector3 center, float radius, bool isHunter, AIBehaviourConfig config, int seed)
        {
            _possession = possession;
            _ledger = ledger;
            _config = config;
            _ghostId = possession != null ? possession.GhostId : string.Empty;
            _hostVillagerId = hostVillagerId;
            _center = center;
            _radius = radius;
            _isHunter = isHunter;
            _random = new System.Random(seed);

            _actions.Clear();
            _uses.Clear();

            if (actions != null)
            {
                foreach (GhostNightActionData action in actions)
                {
                    if (action != null && !_actions.Contains(action))
                    {
                        _actions.Add(action);
                    }
                }
            }

            AILog.Log(AILog.GHOST, _ghostId, $"Entity initialized at {center} radius={radius:F1}m hunter={isHunter} actions={_actions.Count}");
        }

        public void SetSimulationActive(bool active)
        {
            _movement.SetAgentActive(active);

            if (active)
            {
                PushIdle();
                _nextDecisionTime = Time.time;
            }

            _behaviorAgent.enabled = active;
            _isSimulating = active;
        }

        private void Update()
        {
            if (!_isSimulating || _config == null)
            {
                return;
            }

            if (_session == null)
            {
                if (Time.time >= _nextDecisionTime)
                {
                    TryBeginNextCommand();
                }

                return;
            }

            WatchSession();
        }

        public bool IsInsideRadius(Vector3 position)
        {
            Vector3 offset = position - _center;
            offset.y = 0f;
            return offset.sqrMagnitude <= _radius * _radius;
        }

        public GhostActionContext CreateContext()
        {
            return CreateContext(_session != null ? _session.Action : null);
        }

        private GhostActionContext CreateContext(GhostNightActionData action)
        {
            return new GhostActionContext(_possession, this, _ledger, action, _center, _radius);
        }

        private void TryBeginNextCommand()
        {
            _candidates.Clear();
            float totalWeight = 0f;

            foreach (GhostNightActionData action in _actions)
            {
                if (IsAvailable(action))
                {
                    _candidates.Add(action);
                    totalWeight += action.Weight;
                }
            }

            if (_candidates.Count == 0 || totalWeight <= 0f)
            {
                _nextDecisionTime = Time.time + _config.GhostCommandRetrySeconds;
                return;
            }

            GhostNightActionData picked = _candidates[_candidates.Count - 1];
            float roll = (float)_random.NextDouble() * totalWeight;

            foreach (GhostNightActionData candidate in _candidates)
            {
                roll -= candidate.Weight;
                if (roll < 0f)
                {
                    picked = candidate;
                    break;
                }
            }

            IGhostTarget target = GhostTargetRegistry.Find(picked.Query, transform.position, CreateContext(picked));
            if (target == null)
            {
                _nextDecisionTime = Time.time + _config.GhostCommandRetrySeconds;
                return;
            }

            _uses[picked] = UsesOf(picked) + 1;

            if (picked.YinCost > 0f)
            {
                _possession.SpendYin(picked.YinCost, $"night action {picked.ActionId} (pre-paid, no refund)");
            }

            _session = new GhostActionSession(++_commandSerial, picked, target, Time.time);
            _nextTargetCheckTime = Time.time + TARGET_CHECK_INTERVAL;
            ApplyCommandToBlackboard();
            AILog.Log(AILog.GHOST, _ghostId, $"Command {_session} (use {UsesOf(picked)}/{(picked.MaxPerNight > 0 ? picked.MaxPerNight.ToString() : "∞")})");
        }

        private bool IsAvailable(GhostNightActionData action)
        {
            if (action == null || _possession == null || action.Weight <= 0f || action.Shape == GhostActionShape.None)
            {
                return false;
            }

            if (action.HunterOnly && !_isHunter)
            {
                return false;
            }

            if (action.MaxPerNight > 0 && UsesOf(action) >= action.MaxPerNight)
            {
                return false;
            }

            if (_possession.Yin < action.MinYin || _possession.Yin < action.YinCost)
            {
                return false;
            }

            return GhostTargetRegistry.HasAny(action.Query, CreateContext(action));
        }

        private int UsesOf(GhostNightActionData action)
        {
            return _uses.TryGetValue(action, out int count) ? count : 0;
        }

        private void WatchSession()
        {
            if (Time.time - _session.StartTime > _config.GhostCommandTimeoutSeconds)
            {
                FailCommand("timeout");
                return;
            }

            if (_session.IsCarrying || Time.time < _nextTargetCheckTime)
            {
                return;
            }

            _nextTargetCheckTime = Time.time + TARGET_CHECK_INTERVAL;

            if (!IsTargetValid())
            {
                FailCommand("target lost");
                return;
            }

            FollowTarget();
        }

        private void FollowTarget()
        {
            Vector3 targetPosition = _session.Target.gameObject.transform.position;
            Vector3 offset = targetPosition - _session.MovePosition;
            offset.y = 0f;

            if (offset.sqrMagnitude >= FOLLOW_UPDATE_DISTANCE * FOLLOW_UPDATE_DISTANCE)
            {
                SetMovePosition(targetPosition);
            }
        }

        public bool IsTargetValid()
        {
            return _session != null && _session.IsTargetAlive && GhostTargetRegistry.IsCandidate(_session.Target, _session.Action.Query, CreateContext());
        }

        private bool IsTargetInRange()
        {
            Vector3 offset = _session.Target.gameObject.transform.position - transform.position;
            offset.y = 0f;
            float range = _session.Action.ArriveDistance + RANGE_TOLERANCE;
            return offset.sqrMagnitude <= range * range;
        }

        private bool IsTargetUsable(out string reason)
        {
            if (!IsTargetValid())
            {
                reason = "target no longer valid";
                return false;
            }

            if (!IsTargetInRange())
            {
                reason = "target out of range";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryPickUpTarget()
        {
            if (_session == null || _session.Shape != GhostActionShape.Carry || _session.IsCarrying)
            {
                return false;
            }

            if (!IsTargetUsable(out string reason) || !(_session.Target is IGhostCarriable carriable))
            {
                FailCommand(string.IsNullOrEmpty(reason) ? "target is not carriable" : reason);
                return false;
            }

            Vector3 from = carriable.gameObject.transform.position;
            if (!carriable.PickUpByGhost(CreateContext()))
            {
                FailCommand("pick up refused");
                return false;
            }

            _session.SetCarried(carriable, from);
            _carriedName = carriable.gameObject.name;

            if (!TryPickHidePosition(from, out Vector3 hidePosition))
            {
                AILog.Log(AILog.GHOST, _ghostId, "No hide position found, dropping near current position");
            }

            SetMovePosition(hidePosition);
            AILog.Log(AILog.GHOST, _ghostId, $"Picked up {carriable.Kind} {_carriedName}, hiding at {hidePosition}");
            return true;
        }

        public bool BeginChannel()
        {
            if (_session == null)
            {
                return false;
            }

            if (!_session.IsCarrying && !IsTargetUsable(out string reason))
            {
                FailCommand(reason);
                return false;
            }

            _session.BeginChannel(Time.time);
            return true;
        }

        public bool TickChannel(out bool finished)
        {
            finished = false;

            if (_session == null || !_session.IsChanneling)
            {
                return false;
            }

            if (!_session.IsCarrying && !IsTargetUsable(out string reason))
            {
                FailCommand($"channel broken ({reason})");
                return false;
            }

            finished = Time.time >= _session.ChannelEndTime;
            return true;
        }

        public bool CompleteCommand()
        {
            if (_session == null)
            {
                return false;
            }

            GhostActionContext context = CreateContext();
            Vector3 tracePosition;

            if (_session.Shape == GhostActionShape.Carry)
            {
                if (!_session.IsCarrying)
                {
                    FailCommand("nothing carried");
                    return false;
                }

                tracePosition = transform.position;
                DropCarried(tracePosition);
            }
            else
            {
                if (!IsTargetUsable(out string reason))
                {
                    FailCommand(reason);
                    return false;
                }

                tracePosition = _session.Target.gameObject.transform.position;

                if (!GhostVerbExecutor.TryApply(_session.Target, context))
                {
                    FailCommand("verb not applicable");
                    return false;
                }
            }

            GhostNightActionData action = _session.Action;
            string summary = _session.ToString();

            _ledger.AddYin(_possession, action.YinGain, action.ActionId);

            if (action.TracePrefab != null)
            {
                _ledger.SpawnTrace(action.TracePrefab, tracePosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            }

            AILog.Log(AILog.GHOST, _ghostId, $"Command {summary} succeeded (yin +{action.YinGain} pending)");
            EndSession(0f);
            return true;
        }

        public void FailCommand(string reason)
        {
            if (_session == null)
            {
                return;
            }

            if (_session.IsCarrying)
            {
                DropCarried(transform.position);
            }

            AILog.Log(AILog.GHOST, _ghostId, $"Command {_session} failed ({reason}, cost not refunded)");
            EndSession(_config != null ? _config.GhostCommandRetrySeconds : 0f);
        }

        public void ReleaseForDespawn()
        {
            FailCommand("morning");
            _isSimulating = false;
        }

        private void EndSession(float retryDelay)
        {
            _session = null;
            _nextDecisionTime = Time.time + retryDelay;
            _commandSerial++;
            PushIdle();
        }

        private void DropCarried(Vector3 position)
        {
            IGhostCarriable carried = _session.ReleaseCarried();
            _carriedName = string.Empty;

            if (carried != null && !(carried is Object unityObject && unityObject == null))
            {
                carried.DropByGhost(CreateContext(), position);
                AILog.Log(AILog.GHOST, _ghostId, $"Dropped {carried.Kind} {carried.gameObject.name} at {position}");
            }
        }

        private bool TryPickHidePosition(Vector3 from, out Vector3 position)
        {
            float minDistance = _config.GhostHideMinDistance;
            float sqrMinDistance = minDistance * minDistance;

            for (int i = 0; i < HIDE_POSITION_ATTEMPTS; i++)
            {
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Sqrt((float)_random.NextDouble()) * _radius;
                Vector3 candidate = _center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) || !IsInsideRadius(hit.position))
                {
                    continue;
                }

                Vector3 offset = hit.position - from;
                offset.y = 0f;

                if (offset.sqrMagnitude >= sqrMinDistance)
                {
                    position = hit.position;
                    return true;
                }
            }

            position = transform.position;
            return false;
        }

        private void ApplyCommandToBlackboard()
        {
            _currentCommand = _session.ToString();
            _currentShape = _session.Shape;

            SetBlackboardVariable(BB_ARRIVE_DISTANCE, _session.Action.ArriveDistance);
            SetMovePosition(_session.MovePosition);
            SetBlackboardVariable(BB_ACTION_SHAPE, _session.Shape);
            SetBlackboardVariable(BB_COMMAND_SERIAL, _commandSerial);
        }

        private void PushIdle()
        {
            _currentCommand = string.Empty;
            _currentShape = GhostActionShape.None;

            SetBlackboardVariable(BB_ACTION_SHAPE, GhostActionShape.None);
            SetBlackboardVariable(BB_COMMAND_SERIAL, _commandSerial);
        }

        private void SetMovePosition(Vector3 position)
        {
            _movePosition = position;

            if (_session != null)
            {
                _session.MovePosition = position;
            }

            SetBlackboardVariable(BB_MOVE_POSITION, position);
        }

        private void SetBlackboardVariable<T>(string variableName, T value)
        {
            if (_behaviorAgent != null && _behaviorAgent.BlackboardReference != null)
            {
                _behaviorAgent.BlackboardReference.SetVariableValue(variableName, value);
            }
        }

        private void OnDrawGizmos()
        {
            if (_radius <= 0f)
            {
                return;
            }

            Gizmos.color = _isHunter ? new Color(0.8f, 0.1f, 0.9f) : new Color(0.4f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(_center, _radius);
        }
    }
}
