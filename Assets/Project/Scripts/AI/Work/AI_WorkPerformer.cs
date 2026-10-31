using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    [RequireComponent(typeof(AI_Core), typeof(AI_VisualController))]
    public class AI_WorkPerformer : MonoBehaviour
    {
        [SerializeField] private bool _useAnimationEvent;
        [SerializeField] private float _hitIntervalSeconds = 2f;
        [SerializeField] private float _turnSpeedDegrees = 120f;

        [Header("Runtime")]
        [SerializeField] private bool _isWorking;
        [SerializeField] private string _currentActionId;
        [SerializeField] private string _currentStationId;
        [SerializeField] private int _hitCount;

        [Header("Rest")]
        [SerializeField] private float _restArriveDistance = 2f;
        [SerializeField] private float _restStillSpeed = 0.1f;
        [Tooltip("Rest animations sitting on the floor: a villager in one stays seated in a conversation")]
        [SerializeField] private string[] _seatedRestAnimations = { "Rest_Sit", "Rest_Drink" };
        [SerializeField] private string _seatedTalkAnimation = "Rest_Sit";
        [SerializeField] private string _restAnimation = string.Empty;

        private AI_Core _core;
        private AI_VisualController _visual;
        private AI_Movement _movement;
        private WorkStation _station;
        private float _nextHitTime;

        public bool IsWorking => _isWorking;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _visual = GetComponent<AI_VisualController>();
            _movement = GetComponent<AI_Movement>();
        }

        // Rest routines have no work node: the rest action plays once the villager stands still at the rest place (server only,
        // the animation reaches clients through the synced action state) and stops when it moves, sleeps or the routine changes.
        // A conversation stands a standing rest up for the full-body talk, but a seated one stays down and only talks
        private void Update()
        {
            if (_isWorking || !_core.IsSimulating)
            {
                return;
            }

            string wanted = string.Empty;
            if (IsRestingInPlace())
            {
                bool seated = System.Array.IndexOf(_seatedRestAnimations, _restAnimation) >= 0;
                wanted = _core.CurrentState != AIState.Interacting ? WorkAnimationTable.Get(_core.CurrentRoutine.ActionId)
                    : seated ? _seatedTalkAnimation : string.Empty;
            }

            if (wanted == _restAnimation)
            {
                return;
            }

            if (wanted.Length > 0)
            {
                _visual.PlayAction(wanted);
            }
            else if (_visual.CurrentAction == _restAnimation)
            {
                _visual.StopAction();
            }

            AILog.Log(AILog.WORK, _core.LogId, wanted.Length > 0 ? $"Rest pose '{wanted}' ({_core.CurrentRoutine.Source.RoutineId})" : $"Rest pose end '{_restAnimation}'");
            _restAnimation = wanted;
        }

        private bool IsRestingInPlace()
        {
            ResolvedRoutine routine = _core.CurrentRoutine;
            if (routine.Source == null || routine.Category != RoutineCategory.Rest || _core.IsAsleep || _core.IsHomeForNight || _core.MustGoHome)
            {
                return false;
            }

            AIState state = _core.CurrentState;
            if (state == AIState.Sleeping || state == AIState.Panicking || state == AIState.Working)
            {
                return false;
            }

            if (_movement != null && _movement.CurrentSpeed > _restStillSpeed)
            {
                return false;
            }

            Vector3 offset = routine.TargetPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _restArriveDistance * _restArriveDistance;
        }

        public bool BeginWork()
        {
            ResolvedRoutine routine = _core.CurrentRoutine;
            if (routine.Source == null || routine.Category != RoutineCategory.Work)
            {
                AILog.Warn(AILog.WORK, $"{_core.LogId} BeginWork without work routine");
                return false;
            }

            if (routine.IsWaitingForStation)
            {
                _isWorking = false;
                _currentStationId = "(waiting)";
                _core.SetState(AIState.Idle);
                AILog.Log(AILog.WORK, _core.LogId, $"Waiting for a free station for '{routine.ActionId}'");
                return true;
            }

            _isWorking = true;
            _station = routine.Station;
            _currentActionId = routine.ActionId;
            _currentStationId = _station != null ? _station.StationId : string.Empty;
            _hitCount = 0;
            _nextHitTime = Time.time + _hitIntervalSeconds;

            _core.SetState(AIState.Working);
            _restAnimation = string.Empty;
            _visual.PlayAction(WorkAnimationTable.Get(_currentActionId));

            AILog.Log(AILog.WORK, _core.LogId, $"BeginWork action='{_currentActionId}' station={(_station != null ? _station.StationId : "none")} mode={(_useAnimationEvent ? "AnimEvent" : "Timer")}");
            return true;
        }

        public void TickWork(float deltaTime)
        {
            if (!_isWorking)
            {
                return;
            }

            FaceStation(deltaTime);

            if (!_useAnimationEvent && Time.time >= _nextHitTime)
            {
                _nextHitTime = Time.time + _hitIntervalSeconds;
                DeliverHit();
            }
        }

        public void EndWork()
        {
            if (!_isWorking)
            {
                return;
            }

            _isWorking = false;
            _visual.StopAction();
            AILog.Log(AILog.WORK, _core.LogId, $"EndWork action='{_currentActionId}' hits={_hitCount}");
            _station = null;
        }

        public void HandleImpactEvent()
        {
            if (_isWorking && _useAnimationEvent)
            {
                DeliverHit();
            }
        }

        private void DeliverHit()
        {
            _hitCount++;

            // Each hit is counted, not logged: EndWork reports the total
            if (_station == null)
            {
                return;
            }

            _station.ReceiveWork(_core.LogId, _currentActionId);
        }

        private void FaceStation(float deltaTime)
        {
            if (_station == null)
            {
                return;
            }

            Vector3 direction = _station.LookPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeedDegrees * deltaTime);
        }
    }
}
