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
        private const float IDLE_ARRIVE_DISTANCE = 0.8f;

        [SerializeField] private Transform _carrySocket;

        [Header("Runtime (server only)")]
        [SerializeField] private string _ghostId;
        [SerializeField] private string _hostVillagerId;
        [SerializeField] private Vector3 _center;
        [SerializeField] private float _radius;
        [SerializeField] private bool _isHunter;
        [SerializeField] private bool _canChasePlayer;
        [SerializeField] private int _commandSerial;
        [SerializeField] private string _currentCommand;
        [SerializeField] private GhostActionShape _currentShape;
        [SerializeField] private Vector3 _movePosition;
        [SerializeField] private string _carriedName;

        [Header("Debug (server only)")]
        [SerializeField] private float _yin;
        [SerializeField] private float _pendingYin;
        [SerializeField] private float _nextDecisionIn;
        [SerializeField] private string _lastDecision;
        [SerializeField] private string _idleState;
        [SerializeField] private int _huntKillsRemaining;
        [SerializeField] private bool _huntIgnoresRadius;
        [SerializeField] private List<string> _huntTargets = new List<string>();
        [SerializeField] private List<string> _actionUses = new List<string>();

        private AI_Movement _movement;
        private BehaviorGraphAgent _behaviorAgent;
        private AI_Possession _possession;
        private NightOutcomeLedger _ledger;
        private AIBehaviourConfig _config;
        private System.Random _random;
        private readonly List<GhostNightActionData> _actions = new List<GhostNightActionData>();
        private readonly Dictionary<GhostNightActionData, int> _uses = new Dictionary<GhostNightActionData, int>();
        private readonly List<GhostNightActionData> _candidates = new List<GhostNightActionData>();
        private readonly List<string> _unavailableReasons = new List<string>();
        private GhostActionSession _session;
        private GhostHuntPlan _hunt;
        private GhostNightActionData _huntAction;
        private GhostIdleController _idle;
        private float _lastPlayerAttackTime = float.NegativeInfinity;
        private bool _isSimulating;
        private float _nextDecisionTime;
        private float _nextTargetCheckTime;

        // Server: a channel with time started or its timing changed (read Session), and when it stops for any reason
        public event System.Action<AI_GhostEntity> OnChannelChanged;
        public event System.Action<AI_GhostEntity> OnChannelEnded;
        public event System.Action<AI_GhostEntity, float> OnRevealed;

        public AIBehaviourConfig Config => _config;

        public string GhostId => _ghostId;
        public bool IsChanneling => _session != null && _session.IsChanneling;
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

        public void Initialize(AI_Possession possession, NightOutcomeLedger ledger, IReadOnlyList<GhostNightActionData> actions, string hostVillagerId, Vector3 center, float radius, int huntKills, bool huntIgnoresRadius, bool canChasePlayer, AIBehaviourConfig config, int seed)
        {
            _possession = possession;
            _ledger = ledger;
            _config = config;
            _ghostId = possession != null ? possession.GhostId : string.Empty;
            _hostVillagerId = hostVillagerId;
            _center = center;
            _radius = radius;
            _random = new System.Random(seed);
            _hunt = new GhostHuntPlan(_ghostId, huntKills, huntIgnoresRadius);
            _isHunter = _hunt.HasKills;
            _canChasePlayer = _isHunter && canChasePlayer;
            _idle = new GhostIdleController(this, _movement, config, _random);

            _actions.Clear();
            _uses.Clear();
            _huntAction = null;

            if (actions != null)
            {
                foreach (GhostNightActionData action in actions)
                {
                    if (action != null && !_actions.Contains(action))
                    {
                        _actions.Add(action);

                        if (action.IsHunt && _huntAction == null)
                        {
                            _huntAction = action;
                        }
                    }
                }
            }

            if (_hunt.HasKills)
            {
                if (_huntAction != null)
                {
                    _hunt.Refill(_huntAction.Query, _center, CreateContext(_huntAction));
                }
                else
                {
                    AILog.Warn(AILog.GHOST, $"{_ghostId} has hunt kills but no night action marked as hunt");
                }
            }

            RefreshActionUses();
            RefreshHuntDebug();
            AILog.Log(AILog.GHOST, _ghostId, $"Entity initialized at {center} radius={radius:F1}m huntKills={_hunt.KillsRemaining}{(_hunt.IgnoresRadius ? " (ignores radius)" : string.Empty)} reserved={_hunt.TargetCount} chasePlayer={_canChasePlayer} actions={_actions.Count}");
        }

        private void OnDestroy()
        {
            _idle?.Stop("destroyed");

            if (!string.IsNullOrEmpty(_ghostId))
            {
                GhostTargetRegistry.ReleaseAll(_ghostId);
            }
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

            RefreshDebug();

            if (_session == null)
            {
                if (Time.time >= _nextDecisionTime)
                {
                    TryBeginNextCommand(false);
                }
                else if (CanCutCooldownForChase())
                {
                    TryBeginNextCommand(true);
                }

                if (_session == null)
                {
                    _idle.Tick();
                }

                return;
            }

            WatchSession();
        }

        private bool CanCutCooldownForChase()
        {
            return _canChasePlayer
                && Time.time - _lastPlayerAttackTime >= _config.GhostPlayerAttackCooldown
                && _idle.DistanceToStalkTarget <= _config.GhostPlayerDetectDistance;
        }

        public void SetIdleDestination(Vector3 position)
        {
            if (_session == null)
            {
                SetMovePosition(position);
            }
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
            bool ignoreRadius = _hunt != null && _hunt.IgnoresRadius && action != null && action.IsHunt;
            return new GhostActionContext(_possession, this, _ledger, action, _center, _radius, ignoreRadius);
        }

        private void TryBeginNextCommand(bool chaseOnly)
        {
            _candidates.Clear();
            _unavailableReasons.Clear();

            foreach (GhostNightActionData action in _actions)
            {
                if (chaseOnly && (action == null || !action.RequiresChaseTier))
                {
                    continue;
                }

                if (IsAvailable(action, out string reason))
                {
                    _candidates.Add(action);
                }
                else if (action != null)
                {
                    _unavailableReasons.Add($"{action.ActionId}: {reason}");
                }
            }

            if (_candidates.Exists(candidate => candidate.RequiresChaseTier))
            {
                _candidates.RemoveAll(candidate => !candidate.RequiresChaseTier);
            }
            else if (_candidates.Exists(candidate => candidate.IsHunt))
            {
                _candidates.RemoveAll(candidate => !candidate.IsHunt);
            }

            float totalWeight = 0f;

            foreach (GhostNightActionData candidate in _candidates)
            {
                totalWeight += candidate.Weight;
            }

            if (_candidates.Count == 0 || totalWeight <= 0f)
            {
                if (!chaseOnly)
                {
                    _lastDecision = $"no action ({string.Join(", ", _unavailableReasons)})";
                    _nextDecisionTime = Time.time + _config.GhostCommandRetrySeconds;
                }

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

            IGhostTarget target = picked.IsHunt ? _hunt.PickNearest(transform.position) : GhostTargetRegistry.Find(picked.Query, transform.position, CreateContext(picked));
            if (target == null || !GhostTargetRegistry.TryClaim(target, _ghostId))
            {
                _lastDecision = $"{picked.ActionId}: target unavailable";
                _nextDecisionTime = Time.time + _config.GhostCommandRetrySeconds;
                return;
            }

            _idle.Stop("command");
            _uses[picked] = UsesOf(picked) + 1;
            RefreshActionUses();

            if (picked.YinCost > 0f)
            {
                _possession.SpendYin(picked.YinCost, $"night action {picked.ActionId} (pre-paid, no refund)");
            }

            _session = new GhostActionSession(++_commandSerial, picked, target, Time.time);
            _nextTargetCheckTime = Time.time + TARGET_CHECK_INTERVAL;
            ApplyCommandToBlackboard();
            _lastDecision = _session.ToString();
            AILog.Log(AILog.GHOST, _ghostId, $"Command {_session} (use {UsesOf(picked)}/{FormatMax(picked)})");
        }

        private bool IsAvailable(GhostNightActionData action, out string reason)
        {
            if (action == null || _possession == null || action.Weight <= 0f || action.Shape == GhostActionShape.None)
            {
                reason = "disabled";
                return false;
            }

            if (action.HunterOnly && !_isHunter)
            {
                reason = "hunter only";
                return false;
            }

            if (action.RequiresChaseTier && !_canChasePlayer)
            {
                reason = "chase tier only";
                return false;
            }

            if (action.IsHunt)
            {
                return IsHuntAvailable(action, out reason);
            }

            if (action.MaxPerNight > 0 && UsesOf(action) >= action.MaxPerNight)
            {
                reason = "max per night";
                return false;
            }

            if (_possession.Yin < action.MinYin || _possession.Yin < action.YinCost)
            {
                reason = "not enough yin";
                return false;
            }

            if (action.RequiresChaseTier)
            {
                return IsPlayerDetected(action, out reason);
            }

            if (!GhostTargetRegistry.HasAny(action.Query, CreateContext(action)))
            {
                reason = "no target";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool IsHuntAvailable(GhostNightActionData action, out string reason)
        {
            if (!_hunt.HasKills)
            {
                reason = "no kills left";
                return false;
            }

            _hunt.Refill(action.Query, transform.position, CreateContext(action));
            RefreshHuntDebug();

            if (_hunt.TargetCount == 0)
            {
                reason = "no target";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool IsPlayerDetected(GhostNightActionData action, out string reason)
        {
            if (Time.time - _lastPlayerAttackTime < _config.GhostPlayerAttackCooldown)
            {
                reason = "attack cooldown";
                return false;
            }

            IGhostTarget player = GhostTargetRegistry.Find(action.Query, transform.position, CreateContext(action));
            if (player == null)
            {
                reason = "no target";
                return false;
            }

            Vector3 offset = player.gameObject.transform.position - transform.position;
            offset.y = 0f;
            float detect = _config.GhostPlayerDetectDistance;

            if (offset.sqrMagnitude > detect * detect)
            {
                reason = "player not detected";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static string FormatMax(GhostNightActionData action)
        {
            return action.MaxPerNight > 0 ? action.MaxPerNight.ToString() : "∞";
        }

        private void RefreshActionUses()
        {
            _actionUses.Clear();

            foreach (GhostNightActionData action in _actions)
            {
                _actionUses.Add($"{action.ActionId} {UsesOf(action)}/{FormatMax(action)}");
            }
        }

        private void RefreshDebug()
        {
            if (_possession != null)
            {
                _yin = _possession.Yin;
                _pendingYin = _possession.PendingYin;
            }

            _nextDecisionIn = _session == null ? Mathf.Max(0f, _nextDecisionTime - Time.time) : 0f;
            _idleState = _session == null && _idle != null ? _idle.State : "command";
        }

        private void RefreshHuntDebug()
        {
            if (_hunt == null)
            {
                return;
            }

            _huntKillsRemaining = _hunt.KillsRemaining;
            _huntIgnoresRadius = _hunt.IgnoresRadius;
            _hunt.WriteDebug(_huntTargets);
        }

        private int UsesOf(GhostNightActionData action)
        {
            return _uses.TryGetValue(action, out int count) ? count : 0;
        }

        private void WatchSession()
        {
            float timeout = _session.Action.IsHunt ? _config.GhostHuntTimeoutSeconds : _config.GhostCommandTimeoutSeconds;
            if (Time.time - _session.StartTime > timeout)
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

            EndChannel();
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

            // Carry: the channel is the pick-up, so the drop after carrying is instant
            float minimum = _config != null ? _config.GhostMinChannelSeconds : 0f;
            float duration = _session.IsCarrying ? 0f : Mathf.Max(minimum, _session.Action.Duration);
            _session.BeginChannel(Time.time, duration);

            if (duration > 0f)
            {
                AILog.Log(AILog.GHOST, _ghostId, $"Channel {_session} for {duration:F1}s");
                OnChannelChanged?.Invoke(this);
            }

            return true;
        }

        // Something outside the ghost (a player item) broke the channel: no effect, cost not refunded.
        // The ghost holds off for retryDelay before choosing again
        public bool InterruptChannel(string reason, float retryDelay)
        {
            if (!IsChanneling)
            {
                return false;
            }

            FailCommand($"channel interrupted ({reason})", retryDelay);
            return true;
        }

        // Push the channel back by a share of its length (progress never below 0)
        public bool RewindChannel(float ratio, string reason)
        {
            if (!IsChanneling || _session.ChannelDuration <= 0f)
            {
                return false;
            }

            _session.RewindChannel(Time.time, _session.ChannelDuration * Mathf.Clamp01(ratio));
            AILog.Log(AILog.GHOST, _ghostId, $"Channel {_session} rewound {ratio:P0} ({reason}), now {_session.ChannelProgress(Time.time):P0}");
            OnChannelChanged?.Invoke(this);
            return true;
        }

        // Hold the channel where it is for a while; it resumes on its own
        public bool PauseChannel(float seconds, string reason)
        {
            if (!IsChanneling || _session.ChannelDuration <= 0f || seconds <= 0f)
            {
                return false;
            }

            _session.PauseChannel(Time.time, seconds);
            AILog.Log(AILog.GHOST, _ghostId, $"Channel {_session} paused {seconds:F1}s ({reason})");
            OnChannelChanged?.Invoke(this);
            return true;
        }

        // Every player sees this ghost's channel for a while, however far away
        public void Reveal(float seconds, string reason)
        {
            if (!_isSimulating || seconds <= 0f)
            {
                return;
            }

            AILog.Log(AILog.GHOST, _ghostId, $"Revealed for {seconds:F1}s ({reason})");
            OnRevealed?.Invoke(this, seconds);
        }

        private void EndChannel()
        {
            if (_session == null || !_session.IsChanneling)
            {
                return;
            }

            bool hadTime = _session.ChannelDuration > 0f;
            _session.EndChannel();

            if (hadTime)
            {
                OnChannelEnded?.Invoke(this);
            }
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

            finished = _session.IsChannelDone(Time.time);
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
            string victimId = null;

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
                victimId = _session.Target.gameObject.TryGetComponent(out AI_Core victim) ? victim.LogId : null;

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
                GameObject traceObject = _ledger.SpawnTrace(action.TracePrefab, tracePosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                if (action.IsHunt && victimId != null && traceObject != null && traceObject.TryGetComponent(out Trace.GhostTrace body))
                {
                    body.SetVictim(victimId);
                }
            }

            if (action.IsHunt)
            {
                _hunt.OnKilled(_session.Target);
                RefreshHuntDebug();
            }

            if (action.RequiresChaseTier)
            {
                _lastPlayerAttackTime = Time.time;
            }

            float cooldown = action.IsHunt && _hunt.HasKills ? _config.GhostCommandRetrySeconds : PickCooldown();
            string huntInfo = action.IsHunt ? $", kills left {_hunt.KillsRemaining}" : string.Empty;
            AILog.Log(AILog.GHOST, _ghostId, $"Command {summary} succeeded (yin +{action.YinGain} pending{huntInfo}, cooldown {cooldown:F1}s)");
            EndSession(cooldown);
            return true;
        }

        public void FailCommand(string reason)
        {
            FailCommand(reason, _config != null ? _config.GhostCommandRetrySeconds : 0f);
        }

        private void FailCommand(string reason, float retryDelay)
        {
            if (_session == null)
            {
                return;
            }

            if (_session.IsCarrying)
            {
                DropCarried(transform.position);
            }

            _lastDecision = $"failed {_session} ({reason})";
            AILog.Log(AILog.GHOST, _ghostId, $"Command {_session} failed ({reason}, cost not refunded)");
            EndSession(retryDelay);
        }

        public void ReleaseForDespawn()
        {
            FailCommand("morning");
            _idle?.Stop("morning");
            GhostTargetRegistry.ReleaseAll(_ghostId);

            int lost = _hunt != null ? _hunt.Abandon() : 0;
            if (lost > 0)
            {
                AILog.Log(AILog.GHOST, _ghostId, $"Morning: {lost} unused kills lost");
            }

            RefreshHuntDebug();

            _isSimulating = false;
        }

        private float PickCooldown()
        {
            return Mathf.Lerp(_config.GhostActionCooldownMin, _config.GhostActionCooldownMax, (float)_random.NextDouble());
        }

        private void EndSession(float retryDelay)
        {
            EndChannel();

            if (!(_session.Action.IsHunt && _hunt.Contains(_session.Target)))
            {
                GhostTargetRegistry.Release(_session.Target, _ghostId);
            }

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
            float maxDistance = _config.GhostHideMaxDistance;
            float sqrMinDistance = minDistance * minDistance;
            float sqrMaxDistance = (maxDistance + NAVMESH_SAMPLE_DISTANCE) * (maxDistance + NAVMESH_SAMPLE_DISTANCE);

            for (int i = 0; i < HIDE_POSITION_ATTEMPTS; i++)
            {
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Lerp(minDistance, maxDistance, (float)_random.NextDouble());
                Vector3 candidate = from + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas) || !IsInsideRadius(hit.position))
                {
                    continue;
                }

                Vector3 offset = hit.position - from;
                offset.y = 0f;

                if (offset.sqrMagnitude >= sqrMinDistance && offset.sqrMagnitude <= sqrMaxDistance)
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

            SetBlackboardVariable(BB_ARRIVE_DISTANCE, IDLE_ARRIVE_DISTANCE);
            SetMovePosition(transform.position);
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
