using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Minion
{
    [RequireComponent(typeof(AI_Status))]
    public class AI_Minion : MonoBehaviour, IItemControlReceiver
    {
        private const float RANGE_TOLERANCE = 0.3f;
        private const float REPATH_MOVE_DISTANCE = 0.5f;
        private const float PIERCE_HEIGHT_TOLERANCE = 2f;
        // Extra time over the straight-line flight before a lunge is called finished (the flyer curves and accelerates)
        private const float PIERCE_TIME_MARGIN = 0.6f;

        [SerializeField] private MinionData _data;
        // x2 per ghost sealed so far (MinionSpawner)
        private float _speedMultiplier = 1f;
        // Salt slow: speed × multiplier until this time
        private float _slowUntil = float.NegativeInfinity;
        private float _slowMultiplier = 1f;

        [Header("Pierce attack")]
        [Tooltip("Flat distance to the player from which the minion lunges in (the hit is judged against AttackRange)")]
        [SerializeField, Min(0.1f)] private float _lungeStartDistance = 3f;
        [SerializeField, Min(0.1f)] private float _lungeSpeed = 9f;
        [Tooltip("The lunge is aimed this far past the player, so the minion flies straight through the body")]
        [SerializeField, Min(0f)] private float _pierceThroughDistance = 3f;
        [Tooltip("After piercing a player the minion hangs there this long (fade presentation), then despawns")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1.2f;
        [Tooltip("The bell carries further so a ghost glows from afar; a minion is staggered only this close to the ringer")]
        [SerializeField, Min(0f)] private float _bellStaggerRadius = 5f;

        [Header("Fear (gong)")]
        [Tooltip("While feared the minion flies to a point this far beyond itself, straight away from where the gong rang")]
        [SerializeField, Min(0.5f)] private float _fleeLookAhead = 4f;

        [Header("Runtime (server only)")]
        [SerializeField] private MinionState _state = MinionState.Inactive;
        [SerializeField] private string _targetName;
        [SerializeField] private float _distanceToObjective;

        private INetworkMovable _movement;
        private AI_Status _status;
        private MinionBattlefield _battlefield;
        private MinionPlayer _player;
        private Vector3 _chaseOrigin;
        private Vector3 _lastDestination;
        private bool _isSimulating;
        private bool _isRemoved;
        private float _nextAggroCheckTime;
        private float _aggroBlockedUntil;
        private float _nextRepathTime;
        private float _nextAttackTime;
        private float _staggerUntil;
        private float _fearUntil;
        private Vector3 _fearSource;
        private readonly ItemReactionGate _itemGate = new ItemReactionGate();
        private Vector3 _pierceEnd;
        private Vector3 _lastPiercePosition;
        private float _pierceUntil;
        private bool _pierceHit;
        private float _fadeEndTime;

        public MinionState State => _state;
        public MinionData Data => _data;

        public event Action<AI_Minion> OnRemoved;
        public event Action<AI_Minion> OnAttackStarted;
        public event Action<AI_Minion, bool> OnAttackResolved;
        public event Action<AI_Minion, ItemControlEffect> OnControlApplied;
        // (minion, seconds until despawn) once it has pierced a player
        public event Action<AI_Minion, float> OnPierced;

        private void Awake()
        {
            _movement = GetComponent<INetworkMovable>();
            _status = GetComponent<AI_Status>();

            if (_movement == null)
            {
                AILog.Error(AILog.MINION, $"{name} has no INetworkMovable (AI_FlyingMovement or AI_Movement)");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _status.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _status.OnDied -= HandleDied;
        }

        private void OnDestroy()
        {
            NotifyRemoved();
        }

        public void Initialize(MinionBattlefield battlefield, float speedMultiplier = 1f)
        {
            _battlefield = battlefield;
            _speedMultiplier = Mathf.Max(1f, speedMultiplier);
            _state = MinionState.Inactive;
            _isRemoved = false;
            _player = default;

            if (_data == null)
            {
                AILog.Error(AILog.MINION, $"{name} has no MinionData");
            }
        }

        public void SetSimulationActive(bool active)
        {
            _movement.SetAgentActive(active);
            _isSimulating = active && _data != null && _battlefield != null;

            if (_isSimulating && _state == MinionState.Inactive)
            {
                EnterMoveToObjective();
            }
        }

        private void Update()
        {
            if (!_isSimulating || _state == MinionState.Dead)
            {
                return;
            }

            if (_battlefield.Objective == null)
            {
                Remove("objective gone");
                return;
            }

            _distanceToObjective = MinionBattlefield.FlatDistance(transform.position, _battlefield.Objective.position);

            if (_state == MinionState.Fading)
            {
                if (Time.time >= _fadeEndTime)
                {
                    Remove("pierced a player");
                }

                return;
            }

            if (Time.time < _staggerUntil)
            {
                return;
            }

            if (Time.time < _fearUntil)
            {
                TickFlee();
                return;
            }

            switch (_state)
            {
                case MinionState.MoveToObjective:
                    TickMoveToObjective();
                    break;

                case MinionState.ChasePlayer:
                    TickChase();
                    break;

                case MinionState.Attack:
                    TickAttack();
                    break;
            }
        }

        private void TickMoveToObjective()
        {
            if (_distanceToObjective <= _data.ObjectiveContactDistance)
            {
                HitObjective();
                return;
            }

            if (Time.time >= _nextAggroCheckTime)
            {
                _nextAggroCheckTime = Time.time + _data.AggroCheckInterval;

                if (CanAggro() && _battlefield.TryFindNearestPlayer(transform.position, _data.AggroRadius, out MinionPlayer player))
                {
                    EnterChase(player);
                    return;
                }
            }

            MoveToward(_battlefield.Objective.position, _data.MoveSpeed * _speedMultiplier);
        }

        private bool CanAggro()
        {
            switch (_data.TargetMode)
            {
                case MinionTargetMode.ObjectiveOnly:
                    return false;

                case MinionTargetMode.PlayerFirst:
                    return true;

                default:
                    return Time.time >= _aggroBlockedUntil && _distanceToObjective > _data.CommitToObjectiveDistance;
            }
        }

        private void TickChase()
        {
            if (!_player.IsValid)
            {
                EnterMoveToObjective();
                return;
            }

            Vector3 playerPosition = _player.Position;
            float distance = MinionBattlefield.FlatDistance(transform.position, playerPosition);

            if (ShouldGiveUpChase(distance))
            {
                _aggroBlockedUntil = Time.time + _data.ReaggroCooldown;
                AILog.Log(AILog.MINION, name, $"Chase on {_targetName} dropped (distance {distance:F1}m), back to objective");
                EnterMoveToObjective();
                return;
            }

            if (distance <= _lungeStartDistance && Time.time >= _nextAttackTime)
            {
                EnterAttack();
                return;
            }

            if (distance <= _data.AttackRange)
            {
                _movement.Stop();
                FaceToward(playerPosition);
                return;
            }

            MoveToward(playerPosition, _data.ChaseSpeed * _speedMultiplier);
        }

        private bool ShouldGiveUpChase(float distance)
        {
            if (distance > _data.LoseAggroDistance)
            {
                return true;
            }

            return _data.TargetMode != MinionTargetMode.PlayerFirst && MinionBattlefield.FlatDistance(transform.position, _chaseOrigin) > _data.LeashDistance;
        }

        // The lunge is committed to a line through the player: it hits once on the way through, never turns to follow
        private void TickAttack()
        {
            Vector3 position = transform.position;

            if (!_pierceHit && _player.IsValid && PassedThrough(_lastPiercePosition, position, _player.Position))
            {
                _pierceHit = true;
                _player.Damageable.ReceiveDamage(_data.AttackDamage, gameObject);
                AILog.Log(AILog.MINION, name, $"Pierced {_targetName} damage={_data.AttackDamage:F1} ({DamageTypeTag.Resolve(gameObject)})");
                OnAttackResolved?.Invoke(this, true);
            }

            _lastPiercePosition = position;

            bool through = MinionBattlefield.FlatDistance(position, _pierceEnd) <= RANGE_TOLERANCE || Time.time >= _pierceUntil;
            if (!through)
            {
                MoveDirect(_pierceEnd, _lungeSpeed * _speedMultiplier);
                return;
            }

            if (_pierceHit)
            {
                EnterFade();
                return;
            }

            // Missed (the player stepped aside): come round and try again
            _nextAttackTime = Time.time + _data.AttackInterval;
            AILog.Log(AILog.MINION, name, $"Pierce on {_targetName} missed");
            OnAttackResolved?.Invoke(this, false);

            if (_player.IsValid)
            {
                _state = MinionState.ChasePlayer;
            }
            else
            {
                EnterMoveToObjective();
            }
        }

        // Did this frame's flight segment come within AttackRange of the player (flat), at about body height?
        private bool PassedThrough(Vector3 from, Vector3 to, Vector3 playerPosition)
        {
            if (Mathf.Abs(to.y - playerPosition.y) > PIERCE_HEIGHT_TOLERANCE)
            {
                return false;
            }

            Vector2 a = new Vector2(from.x, from.z);
            Vector2 b = new Vector2(to.x, to.z);
            Vector2 p = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, segment) / lengthSquared) : 0f;
            return Vector2.Distance(a + segment * t, p) <= _data.AttackRange;
        }

        private void EnterFade()
        {
            _state = MinionState.Fading;
            _movement.Stop();
            _player = default;
            _fadeEndTime = Time.time + _fadeSeconds;
            AILog.Log(AILog.MINION, name, $"Fading out ({_fadeSeconds:F1}s)");
            OnPierced?.Invoke(this, _fadeSeconds);
        }

        private void HitObjective()
        {
            IDamageable objective = _battlefield.ObjectiveDamageable;

            if (objective != null && !objective.IsDead)
            {
                objective.ReceiveDamage(_data.ObjectiveDamage, gameObject);
                AILog.Log(AILog.MINION, name, $"Reached objective, damage={_data.ObjectiveDamage:F1}");
            }
            else
            {
                AILog.Log(AILog.STUB, name, "Reached objective without live IDamageable (flag not implemented yet), despawn only");
            }

            Remove("reached objective");
        }

        private void EnterMoveToObjective()
        {
            _player = default;
            _state = MinionState.MoveToObjective;
            _targetName = _battlefield.Objective != null ? _battlefield.Objective.name : string.Empty;
            _lastDestination = new Vector3(float.MaxValue, 0f, 0f);
            _nextRepathTime = 0f;
        }

        private void EnterChase(MinionPlayer player)
        {
            _player = player;
            _chaseOrigin = transform.position;
            _targetName = player.Name;
            _state = MinionState.ChasePlayer;
            _nextRepathTime = 0f;
            AILog.Log(AILog.MINION, name, $"Aggro on {_targetName}");
        }

        private void EnterAttack()
        {
            Vector3 position = transform.position;
            Vector3 playerPosition = _player.Position;
            Vector3 direction = playerPosition - position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            _state = MinionState.Attack;
            _pierceHit = false;
            _lastPiercePosition = position;
            _pierceEnd = playerPosition + direction * _pierceThroughDistance;
            float length = MinionBattlefield.FlatDistance(position, _pierceEnd);
            _pierceUntil = Time.time + length / (_lungeSpeed * _speedMultiplier) + PIERCE_TIME_MARGIN;
            FaceToward(playerPosition);
            OnAttackStarted?.Invoke(this);
        }

        private bool IsWithinBellStagger(GameObject source)
        {
            return source == null || (source.transform.position - transform.position).sqrMagnitude <= _bellStaggerRadius * _bellStaggerRadius;
        }

        public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
        {
            if (!_isSimulating || _state == MinionState.Dead || _state == MinionState.Fading || !effect.IsValid)
            {
                return false;
            }

            float until = Time.time + effect.Duration;
            string sourceName = source != null ? source.name : "unknown";
            PlayerItemKind item;

            switch (effect.Kind)
            {
                // Bell: stagger, and a lunge on its way in is called off. Once through the player the hit already landed
                case ItemControlKind.Stagger:
                    item = PlayerItemKind.Bell;
                    if (_pierceHit || !_itemGate.IsReady(item) || !IsWithinBellStagger(source))
                    {
                        return false;
                    }

                    _staggerUntil = Mathf.Max(_staggerUntil, until);
                    _movement.Stop();

                    if (_state == MinionState.Attack)
                    {
                        _state = MinionState.ChasePlayer;
                        _nextAttackTime = Mathf.Max(_nextAttackTime, _staggerUntil);
                    }

                    _nextRepathTime = 0f;
                    break;

                // Gong: fear. It drops whatever it was after and flies away from where the gong rang
                case ItemControlKind.SensoryInterference:
                    item = PlayerItemKind.Gong;
                    if (!_itemGate.IsReady(item))
                    {
                        return false;
                    }

                    if (_state == MinionState.Attack && _pierceHit)
                    {
                        // Already through the player: the strike stands, it fades as planned
                        return false;
                    }

                    _fearUntil = Mathf.Max(_fearUntil, until);
                    _fearSource = source != null ? source.transform.position : transform.position - transform.forward;
                    _aggroBlockedUntil = Mathf.Max(_aggroBlockedUntil, _fearUntil);
                    EnterMoveToObjective();
                    break;

                // Salt: slowed for a while (every move, the lunge included)
                case ItemControlKind.Slow:
                    item = PlayerItemKind.Salt;
                    if (!_itemGate.IsReady(item))
                    {
                        return false;
                    }

                    _slowUntil = Mathf.Max(_slowUntil, until);
                    _slowMultiplier = Mathf.Clamp(effect.SpeedMultiplier, 0.01f, 1f);
                    _nextRepathTime = 0f;
                    break;

                default:
                    return false;
            }

            _itemGate.Consume(item, _data.ItemCooldowns);
            AILog.Log(AILog.MINION, name, $"Item control {effect.Kind} {effect.Duration:F1}s by {sourceName}");
            OnControlApplied?.Invoke(this, effect);
            return true;
        }

        private void TickFlee()
        {
            Vector3 away = transform.position - _fearSource;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            MoveDirect(transform.position + away * _fleeLookAhead, _data.ChaseSpeed * _speedMultiplier);
        }

        private void MoveToward(Vector3 destination, float speed)
        {
            if (Time.time < _nextRepathTime && MinionBattlefield.FlatDistance(destination, _lastDestination) < REPATH_MOVE_DISTANCE)
            {
                return;
            }

            _nextRepathTime = Time.time + _data.RepathInterval;
            MoveDirect(destination, speed);
        }

        // Every frame, no repath throttle (lunge, flee)
        private void MoveDirect(Vector3 destination, float speed)
        {
            _lastDestination = destination;
            _movement.SetSpeed(Time.time < _slowUntil ? speed * _slowMultiplier : speed);
            _movement.MoveTo(destination);
        }

        private void FaceToward(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void HandleDied(AI_Status status, GameObject source)
        {
            if (_state == MinionState.Dead)
            {
                return;
            }

            _state = MinionState.Dead;
            _movement.Stop();
            _player = default;
            AILog.Log(AILog.MINION, name, $"Died by {(source != null ? source.name : "unknown")}");
            NotifyRemoved();
        }

        public void Remove(string reason)
        {
            if (_isRemoved)
            {
                return;
            }

            _state = MinionState.Dead;
            _isSimulating = false;
            _movement.Stop();
            _player = default;
            AILog.Log(AILog.MINION, name, $"Removed ({reason})");
            NotifyRemoved();

            if (_battlefield != null && _battlefield.SpawnHandler != null)
            {
                _battlefield.SpawnHandler.Despawn(gameObject);
            }
            else
            {
                AILog.Error(AILog.MINION, $"{name} has no spawn handler, despawn skipped");
            }
        }

        private void NotifyRemoved()
        {
            if (_isRemoved)
            {
                return;
            }

            _isRemoved = true;
            OnRemoved?.Invoke(this);
        }
    }
}
