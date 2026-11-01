using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Dialogue;
using ProjectShaman.AI.Interaction;
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
        [Tooltip("A work with its own lines (work.call.<ActionId> in AIDialogue, a hawker calling out) says one on the first hit and every this many after")]
        [SerializeField, Min(1)] private int _callEveryHits = 3;

        [Header("Runtime")]
        [SerializeField] private bool _isWorking;
        [SerializeField] private string _currentActionId;
        [SerializeField] private string _currentStationId;
        [SerializeField] private int _hitCount;

        private const string WORK_CALL_PREFIX = "work.call.";

        [Header("Rest")]
        [SerializeField] private float _restArriveDistance = 2f;
        [SerializeField] private float _restStillSpeed = 0.1f;
        [Tooltip("Rest animations sitting on the floor: a villager in one stays seated in a conversation")]
        // Sitting or lying needs this much room around the hips (the body spreads out behind the feet)
        private const float POSE_CLEARANCE = 0.7f;
        private const float POSE_CHECK_HEIGHT = 0.5f;

        [SerializeField] private string[] _seatedRestAnimations = { "Rest_Sit", "Rest_Drink" };
        [SerializeField] private string _seatedTalkAnimation = "Rest_Sit";
        [SerializeField] private string _sleepAnimation = "Sleep";
        [SerializeField] private string _restAnimation = string.Empty;

        private AI_Core _core;
        private AI_VisualController _visual;
        private AI_Movement _movement;
        private AI_Conversation _conversation;
        private AI_Interaction _interaction;
        private System.Random _lineRandom;
        private WorkStation _station;
        private float _nextHitTime;
        private bool _hitsFromClip;
        // The station is being set up again after this hit (a new log): the swing ends, then the worker stands and waits
        private bool _isWaitingForStation;
        private float _lowerToolAt = -1f;
        // The rest of the swing after the blow lands, before the worker straightens up to wait
        private const float FOLLOW_THROUGH_SECONDS = 0.4f;

        public bool IsWorking => _isWorking;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _visual = GetComponent<AI_VisualController>();
            _movement = GetComponent<AI_Movement>();
            _conversation = GetComponent<AI_Conversation>();
            _interaction = GetComponent<AI_Interaction>();
            _lineRandom = new System.Random(GetInstanceID());
        }

        // Rest routines have no work node: the rest action plays once the villager stands still at the rest place (server only,
        // the animation reaches clients through the synced action state) and stops when it moves, sleeps or the routine changes.
        // A conversation stands a standing rest up for the full-body talk, but a seated one stays down and only talks.
        // Asleep it lies down where it stands and stays down; waking plays "Sleep Stop" (getting up) through StopAction
        private void Update()
        {
            if (_isWorking || !_core.IsSimulating)
            {
                return;
            }

            string wanted = string.Empty;
            if (_core.IsAsleep)
            {
                if (_movement == null || _movement.CurrentSpeed <= _restStillSpeed)
                {
                    wanted = _sleepAnimation;
                }
            }
            else if (IsRestingInPlace())
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
                ClearSpaceForPose();
                _visual.PlayAction(wanted);
            }
            else if (_visual.CurrentAction == _restAnimation)
            {
                _visual.StopAction();
            }

            string reason = _core.IsAsleep ? "asleep" : _core.CurrentRoutine.Source != null ? _core.CurrentRoutine.Source.RoutineId : "no routine";
            AILog.Log(AILog.WORK, _core.LogId, wanted.Length > 0 ? $"Rest pose '{wanted}' ({reason})" : $"Rest pose end '{_restAnimation}'");
            _restAnimation = wanted;
        }

        // Sitting or lying spreads the body out behind the feet: a villager resting against a tree trunk or a wall sank into
        // it. Before the pose, it steps clear of the nearest solid thing around its hips and turns its back to it
        private void ClearSpaceForPose()
        {
            Vector3 hips = transform.position + Vector3.up * POSE_CHECK_HEIGHT;
            Collider nearest = null;
            Vector3 nearestPoint = default;
            float nearestSqr = POSE_CLEARANCE * POSE_CLEARANCE;

            foreach (Collider other in Physics.OverlapSphere(hips, POSE_CLEARANCE, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other is TerrainCollider || other.transform.IsChildOf(transform) || other.attachedRigidbody != null
                    || other.GetComponentInParent<AI_Core>() != null || other.GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                Vector3 point = other.ClosestPoint(hips);
                Vector3 flat = hips - point;
                flat.y = 0f;
                if (flat.sqrMagnitude < nearestSqr)
                {
                    nearest = other;
                    nearestPoint = point;
                    nearestSqr = flat.sqrMagnitude;
                }
            }

            if (nearest == null)
            {
                return;
            }

            Vector3 away = transform.position - nearestPoint;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            float push = POSE_CLEARANCE - Mathf.Sqrt(nearestSqr);
            Vector3 target = transform.position + away * push;
            if (UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit hit, push + 0.5f, UnityEngine.AI.NavMesh.AllAreas)
                && (_movement == null || _movement.Warp(hit.position)))
            {
                transform.rotation = Quaternion.LookRotation(away);
                AILog.Log(AILog.WORK, _core.LogId, $"Stepped {push:F2}m clear of {nearest.name} before the rest pose");
            }
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
            _hitsFromClip = false;
            _isWaitingForStation = false;
            _lowerToolAt = -1f;
            _nextHitTime = Time.time + _hitIntervalSeconds;

            _core.SetState(AIState.Working);
            _restAnimation = string.Empty;

            // Arrival counts within the arrive distance: the agent would otherwise go on walking the last metre to its exact
            // point (or push against someone standing there) and slide along in the work pose
            if (_movement != null)
            {
                _movement.Stop();
            }
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

            if (_isWaitingForStation)
            {
                TickWaitForStation();
                return;
            }

            if (!_useAnimationEvent && !_hitsFromClip && Time.time >= _nextHitTime)
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
            AILog.Log(AILog.WORK, _core.LogId, $"EndWork action='{_currentActionId}' hits={_hitCount} ({(_hitsFromClip ? "clip" : "timer")})");
            _station = null;
        }

        // A clip with an OnWorkImpact event drives the hits itself from its first blow (the axe splits the log as it lands);
        // clips without one keep the timer
        public void HandleImpactEvent()
        {
            if (_isWorking && !_isWaitingForStation)
            {
                _hitsFromClip = true;
                DeliverHit();
            }
        }

        private void DeliverHit()
        {
            _hitCount++;
            CallOut();

            // Each hit is counted, not logged: EndWork reports the total
            if (_station == null)
            {
                return;
            }

            _station.ReceiveWork(_core.LogId, _currentActionId);

            if (!_station.IsReady)
            {
                _isWaitingForStation = true;
                _lowerToolAt = Time.time + FOLLOW_THROUGH_SECONDS;
            }
        }

        private void TickWaitForStation()
        {
            if (_lowerToolAt >= 0f && Time.time >= _lowerToolAt)
            {
                _lowerToolAt = -1f;
                _visual.StopAction();
            }

            if (_station == null || _station.IsReady)
            {
                _isWaitingForStation = false;
                _lowerToolAt = -1f;
                _nextHitTime = Time.time + _hitIntervalSeconds;
                _visual.PlayAction(WorkAnimationTable.Get(_currentActionId));
            }
        }

        // Lines of the work itself, said alone (a chat between villagers at work uses smalltalk.work): not while in a chat or a talk
        private void CallOut()
        {
            string id = WORK_CALL_PREFIX + _currentActionId;
            if (_conversation == null || (_hitCount - 1) % _callEveryHits != 0 || !DialogueLibrary.HasLine(id)
                || _conversation.IsInSession || (_interaction != null && _interaction.IsInteracting))
            {
                return;
            }

            _conversation.Say(DialogueLibrary.Line(id, _lineRandom), $"{_currentActionId} call");
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
