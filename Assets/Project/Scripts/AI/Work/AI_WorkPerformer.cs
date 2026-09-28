using System;
using System.Collections.Generic;
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
        private const string WORK_ACTION_PARAM = "WorkAction";

        [Serializable]
        public class ActionAnimationBinding
        {
            public string ActionId;
            public int AnimatorValue;
        }

        [SerializeField] private bool _useAnimationEvent;
        [SerializeField] private float _hitIntervalSeconds = 2f;
        [SerializeField] private float _turnSpeedDegrees = 360f;
        [SerializeField] private List<ActionAnimationBinding> _actionBindings = new List<ActionAnimationBinding>();

        [Header("Runtime")]
        [SerializeField] private bool _isWorking;
        [SerializeField] private string _currentActionId;
        [SerializeField] private string _currentStationId;
        [SerializeField] private int _hitCount;

        private AI_Core _core;
        private AI_VisualController _visual;
        private WorkStation _station;
        private float _nextHitTime;

        public bool IsWorking => _isWorking;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _visual = GetComponent<AI_VisualController>();
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
            _visual.SetIntegerIfExists(WORK_ACTION_PARAM, GetAnimatorValue(_currentActionId));

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
            _visual.SetIntegerIfExists(WORK_ACTION_PARAM, 0);
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

            if (_station == null)
            {
                AILog.Log(AILog.WORK, _core.LogId, $"Hit #{_hitCount} (no station)");
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

        private int GetAnimatorValue(string actionId)
        {
            foreach (ActionAnimationBinding binding in _actionBindings)
            {
                if (binding.ActionId == actionId)
                {
                    return binding.AnimatorValue;
                }
            }

            return 0;
        }
    }
}
