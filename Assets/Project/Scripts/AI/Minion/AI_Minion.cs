using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Minion
{
    [RequireComponent(typeof(AI_Movement), typeof(AI_Status))]
    public class AI_Minion : MonoBehaviour
    {
        private const float RANGE_TOLERANCE = 0.3f;
        private const float REPATH_MOVE_DISTANCE = 0.5f;

        [SerializeField] private MinionData _data;

        [Header("Runtime (server only)")]
        [SerializeField] private MinionState _state = MinionState.Inactive;
        [SerializeField] private string _targetName;
        [SerializeField] private float _distanceToObjective;

        private AI_Movement _movement;
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
        private float _attackHitTime;

        public MinionState State => _state;
        public MinionData Data => _data;

        public event Action<AI_Minion> OnRemoved;

        private void Awake()
        {
            _movement = GetComponent<AI_Movement>();
            _status = GetComponent<AI_Status>();
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

        public void Initialize(MinionBattlefield battlefield)
        {
            _battlefield = battlefield;
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

            MoveToward(_battlefield.Objective.position, _data.MoveSpeed);
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

            if (distance <= _data.AttackRange)
            {
                _movement.Stop();
                FaceToward(playerPosition);

                if (Time.time >= _nextAttackTime)
                {
                    EnterAttack();
                }

                return;
            }

            MoveToward(playerPosition, _data.ChaseSpeed);
        }

        private bool ShouldGiveUpChase(float distance)
        {
            if (distance > _data.LoseAggroDistance)
            {
                return true;
            }

            return _data.TargetMode != MinionTargetMode.PlayerFirst && MinionBattlefield.FlatDistance(transform.position, _chaseOrigin) > _data.LeashDistance;
        }

        private void TickAttack()
        {
            if (!_player.IsValid)
            {
                EnterMoveToObjective();
                return;
            }

            Vector3 playerPosition = _player.Position;
            FaceToward(playerPosition);

            if (Time.time < _attackHitTime)
            {
                return;
            }

            float distance = MinionBattlefield.FlatDistance(transform.position, playerPosition);
            _nextAttackTime = Time.time + _data.AttackInterval;

            if (distance <= _data.AttackRange + RANGE_TOLERANCE)
            {
                _player.Damageable.ReceiveDamage(_data.AttackDamage, gameObject);
                AILog.Log(AILog.MINION, name, $"Attack {_targetName} damage={_data.AttackDamage:F1} ({DamageTypeTag.Resolve(gameObject)})");
            }
            else
            {
                AILog.Log(AILog.MINION, name, $"Attack on {_targetName} missed (distance {distance:F1}m)");
            }

            _state = MinionState.ChasePlayer;
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
            _state = MinionState.Attack;
            _attackHitTime = Time.time + _data.AttackWindup;
        }

        private void MoveToward(Vector3 destination, float speed)
        {
            if (Time.time < _nextRepathTime && MinionBattlefield.FlatDistance(destination, _lastDestination) < REPATH_MOVE_DISTANCE)
            {
                return;
            }

            _nextRepathTime = Time.time + _data.RepathInterval;
            _lastDestination = destination;
            _movement.SetSpeed(speed);
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
