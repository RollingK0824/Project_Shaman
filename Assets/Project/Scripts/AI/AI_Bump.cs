using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Dialogue;
using ProjectShaman.AI.Interaction;

namespace ProjectShaman.AI
{
    // A shoulder bump without an animation: the villager is shoved a step off the bumper's line (NavMeshAgent.Move, so it
    // stays on the mesh), its body tilts away and springs back, and now and then it grumbles. A walking villager stops a
    // moment and turns to the bumper before going on; one standing still (talking, working) staggers back to its spot.
    // No stress: bumps happen too often to mean anything, and stress is a clue about ghosts.
    // Server: Shove and the villager-on-villager unjam. Every peer: PlayTilt (Net_VillagerBump relays it)
    [RequireComponent(typeof(NavMeshAgent))]
    public class AI_Bump : MonoBehaviour
    {
        private static readonly List<AI_Bump> _all = new List<AI_Bump>();

        [Header("Shove")]
        [SerializeField, Min(0f)] private float _shoveDistance = 0.3f;
        [SerializeField, Min(0.05f)] private float _shoveSeconds = 0.2f;
        [Tooltip("A walking villager stands this long after the shove, facing the bumper, then goes on")]
        [SerializeField, Min(0f)] private float _pauseSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _faceTurnSpeed = 360f;
        [Tooltip("A villager that was standing still walks back to its spot after this long")]
        [SerializeField, Min(0f)] private float _returnDelay = 0.6f;
        [SerializeField, Min(0.05f)] private float _returnSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _cooldown = 0.8f;

        [Header("Tilt (body only, no animation)")]
        [SerializeField, Range(0f, 30f)] private float _tiltDegrees = 10f;
        [SerializeField, Range(0f, 60f)] private float _turnDegrees = 25f;
        [SerializeField, Min(0.05f)] private float _tiltSeconds = 0.45f;

        [Header("Grumble")]
        [SerializeField, Range(0f, 1f)] private float _sayChance = 0.5f;
        [SerializeField, Min(0f)] private float _sayCooldown = 15f;

        [Header("Villager jam (server)")]
        [Tooltip("A villager walking toward its goal that has moved less than this over the window is jammed")]
        [SerializeField, Min(0.05f)] private float _jamProgress = 0.3f;
        [SerializeField, Min(0.5f)] private float _jamWindow = 1.5f;
        [Tooltip("How close the villager in the way must stand, and how far ahead (dot of directions)")]
        [SerializeField, Min(0.3f)] private float _jamReach = 1.2f;

        private NavMeshAgent _agent;
        private AI_Conversation _conversation;
        private AI_VillagerModel _model;
        private readonly System.Random _random = new System.Random();

        private Vector3 _shoveVelocity;
        private float _shoveUntil;
        private Vector3 _returnTo;
        private float _returnAt = -1f;
        private float _returnUntil;
        private float _nextBumpTime;
        private float _nextSayTime;
        private float _pauseUntil = -1f;
        private float _pausedSpeed;
        private Vector3 _faceTarget;

        private Transform _tilted;
        private Quaternion _tiltBase;
        private Quaternion _tiltPeak;
        private float _tiltStart = -1f;

        private Vector3 _jamAnchor;
        private float _jamSince = -1f;
        private bool _jamLogged;

        // (bump direction on the ground, bumper position): the server raises it, Net_VillagerBump plays it everywhere
        public event Action<Vector3, Vector3> OnBumped;

        public bool IsSimulating { get; set; }
        public Vector3 Velocity => _agent != null && _agent.isActiveAndEnabled ? _agent.velocity : Vector3.zero;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _conversation = GetComponent<AI_Conversation>();
            _model = GetComponent<AI_VillagerModel>();
        }

        private void OnEnable()
        {
            _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
            EndTilt();
            EndPause();
        }

        // Server. direction = away from the bumper on the ground; by = who bumped (null for a villager)
        public bool Shove(Vector3 direction, Vector3 bumperPosition, string by, bool grumble)
        {
            direction.y = 0f;
            if (!IsSimulating || Time.time < _nextBumpTime || direction.sqrMagnitude < 0.0001f || _agent == null || !_agent.isActiveAndEnabled)
            {
                return false;
            }

            direction.Normalize();
            _nextBumpTime = Time.time + _cooldown;
            _shoveVelocity = direction * (_shoveDistance / _shoveSeconds);
            _shoveUntil = Time.time + _shoveSeconds;

            // Standing still: back to its spot afterwards (a worker at its station, a talker in its circle).
            // Walking: stops a moment and faces the bumper
            bool standing = _agent.isStopped || !_agent.hasPath;
            _returnAt = standing ? Time.time + _shoveSeconds + _returnDelay : -1f;
            _returnTo = transform.position;
            if (!standing && _pauseSeconds > 0f)
            {
                StartPause(bumperPosition);
            }

            AILog.Log(AILog.INTERACTION, LogId, $"Bumped by {by ?? "a villager"} ({(standing ? "standing" : "walking")})");
            if (grumble && _conversation != null && Time.time >= _nextSayTime && _random.NextDouble() < _sayChance)
            {
                _nextSayTime = Time.time + _sayCooldown;
                _conversation.Say(DialogueLibrary.Line("bump", _random), $"bumped by {by}");
            }

            OnBumped?.Invoke(direction, bumperPosition);
            return true;
        }

        // Every peer: the body leans away from the bump and half turns to the bumper, then springs back
        public void PlayTilt(Vector3 direction, Vector3 bumperPosition)
        {
            Transform body = _model != null && _model.CurrentModel != null ? _model.CurrentModel : null;
            if (body == null)
            {
                return;
            }

            EndTilt();
            _tilted = body;
            _tiltBase = body.localRotation;

            Vector3 localAway = transform.InverseTransformDirection(direction);
            localAway.y = 0f;
            Vector3 lean = Vector3.Cross(Vector3.up, localAway.normalized) * _tiltDegrees;

            Vector3 toBumper = transform.InverseTransformDirection(bumperPosition - transform.position);
            float turn = Mathf.Clamp(Mathf.Atan2(toBumper.x, toBumper.z) * Mathf.Rad2Deg, -_turnDegrees, _turnDegrees);

            _tiltPeak = Quaternion.Euler(lean.x, turn, lean.z) * _tiltBase;
            _tiltStart = Time.time;
        }

        private void Update()
        {
            if (!IsSimulating || _agent == null || !_agent.isActiveAndEnabled)
            {
                return;
            }

            if (Time.time < _shoveUntil)
            {
                _agent.Move(_shoveVelocity * Time.deltaTime);
            }
            else if (_returnAt >= 0f && Time.time >= _returnAt)
            {
                UpdateReturn();
            }

            UpdatePause();
            UpdateJam();
        }

        // After the Animator has posed the body this frame, so the lean is not written over
        private void LateUpdate()
        {
            UpdateTilt();
        }

        // Speed 0 rather than isStopped: the behaviour tree re-issues MoveTo (isStopped = false) while it walks,
        // and a speed set meanwhile (running home late) is kept for when the pause ends
        private void StartPause(Vector3 bumperPosition)
        {
            if (_pauseUntil < 0f)
            {
                _pausedSpeed = _agent.speed;
            }

            _pauseUntil = Time.time + _shoveSeconds + _pauseSeconds;
            _faceTarget = bumperPosition;
            _agent.speed = 0f;
            _agent.velocity = Vector3.zero;
        }

        private void UpdatePause()
        {
            if (_pauseUntil < 0f)
            {
                return;
            }

            if (Time.time >= _pauseUntil)
            {
                EndPause();
                return;
            }

            if (_agent.speed > 0f)
            {
                _pausedSpeed = _agent.speed;
                _agent.speed = 0f;
            }

            Vector3 facing = _faceTarget - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), _faceTurnSpeed * Time.deltaTime);
            }
        }

        private void EndPause()
        {
            if (_pauseUntil < 0f)
            {
                return;
            }

            _pauseUntil = -1f;
            if (_agent != null && _agent.isActiveAndEnabled)
            {
                _agent.speed = _pausedSpeed;
            }
        }

        private void UpdateReturn()
        {
            if (!_agent.isStopped && _agent.hasPath)
            {
                // It set off somewhere meanwhile: its path wins
                _returnAt = -1f;
                return;
            }

            if (_returnUntil < _returnAt)
            {
                _returnUntil = Time.time + _returnSeconds;
            }

            Vector3 offset = _returnTo - transform.position;
            offset.y = 0f;
            float left = Mathf.Max(Time.deltaTime, _returnUntil - Time.time);
            if (offset.sqrMagnitude < 0.0025f || Time.time >= _returnUntil)
            {
                _returnAt = -1f;
                return;
            }

            _agent.Move(offset * Mathf.Clamp01(Time.deltaTime / left));
        }

        // Diagnostics + fix for two villagers wedged on a path: one walking that has not got anywhere for the window,
        // with a villager standing right ahead, nudges it aside (a bump without a grumble). Logged once per jam
        private void UpdateJam()
        {
            bool walking = _pauseUntil < 0f && !_agent.isStopped && _agent.hasPath && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance + 0.5f;
            Vector3 position = transform.position;
            if (!walking || _jamSince < 0f || (position - _jamAnchor).sqrMagnitude > _jamProgress * _jamProgress)
            {
                _jamAnchor = position;
                _jamSince = walking ? Time.time : -1f;
                _jamLogged = false;
                return;
            }

            if (Time.time - _jamSince < _jamWindow)
            {
                return;
            }

            AI_Bump blocker = FindBlockerAhead();
            if (!_jamLogged)
            {
                _jamLogged = true;
                AILog.Warn(AILog.MANAGER, $"{LogId} jammed for {Time.time - _jamSince:F1}s (priority {_agent.avoidancePriority}, velocity {_agent.velocity.magnitude:F2}) " +
                    (blocker != null ? $"behind {blocker.LogId} (priority {blocker._agent.avoidancePriority}, {(blocker._agent.isStopped ? "standing" : "walking")})" : "with nobody ahead"));
            }

            if (blocker != null)
            {
                Vector3 ahead = _agent.steeringTarget - position;
                Vector3 side = Vector3.Cross(Vector3.up, ahead).normalized;
                if (Vector3.Dot(blocker.transform.position - position, side) < 0f)
                {
                    side = -side;
                }

                if (blocker.Shove(side, position, LogId, false))
                {
                    _jamSince = Time.time;
                }
            }
        }

        private AI_Bump FindBlockerAhead()
        {
            Vector3 position = transform.position;
            Vector3 ahead = _agent.steeringTarget - position;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f)
            {
                return null;
            }

            ahead.Normalize();
            AI_Bump best = null;
            float bestSqr = _jamReach * _jamReach;

            foreach (AI_Bump other in _all)
            {
                if (other == this || !other.IsSimulating)
                {
                    continue;
                }

                Vector3 offset = other.transform.position - position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < bestSqr && Vector3.Dot(offset.normalized, ahead) > 0.3f)
                {
                    best = other;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private void UpdateTilt()
        {
            if (_tiltStart < 0f || _tilted == null)
            {
                return;
            }

            float t = (Time.time - _tiltStart) / _tiltSeconds;
            if (t >= 1f)
            {
                EndTilt();
                return;
            }

            // Quick lean in the first fifth, then an eased return
            float weight = t < 0.2f ? t / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.8f);
            _tilted.localRotation = Quaternion.Slerp(_tiltBase, _tiltPeak, weight);
        }

        private void EndTilt()
        {
            if (_tilted != null)
            {
                _tilted.localRotation = _tiltBase;
            }

            _tilted = null;
            _tiltStart = -1f;
        }

        private string LogId => _conversation != null ? _conversation.VillagerId : name;
    }
}
