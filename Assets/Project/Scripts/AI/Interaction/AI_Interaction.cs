using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.Work;
using Random = UnityEngine.Random;

namespace ProjectShaman.AI.Interaction
{
    [RequireComponent(typeof(AI_Core))]
    public class AI_Interaction : MonoBehaviour, IAIConfigurable
    {
        public const string BB_IS_INTERACTING = "IsInteracting";
        public const string BB_INTERACTION_SERIAL = "InteractionSerial";
        public const string BB_INTERACTION_RESPONSE = "InteractionResponse";
        public const string BB_INTERACTION_POSITION = "InteractionPosition";
        public const string BB_INTERACTION_LOOK_POINT = "InteractionLookPoint";
        public const string BB_INTERACTION_ARRIVE_DISTANCE = "InteractionArriveDistance";
        public const string BB_INTERACTION_DURATION = "InteractionDuration";

        private const float MOVING_SPEED_THRESHOLD = 0.2f;
        private const float TIMEOUT_MARGIN_SECONDS = 20f;

        [Header("Runtime")]
        [SerializeField] private string _activeDebug;
        [SerializeField] private List<string> _seenDebug = new List<string>();

        private readonly List<IVillagerInteractable> _candidates = new List<IVillagerInteractable>();
        private readonly HashSet<string> _seenToday = new HashSet<string>();

        private AI_Core _core;
        private AI_ToolHandler _toolHandler;
        private AI_Status _status;
        private INetworkMovable _movable;
        private AIRuntimeContext _context;

        private IVillagerInteractable _active;
        private InteractionResponse _activeResponse;
        private string _activeId;
        private InteractionKind _activeKind;
        private bool _startedWhileHoming;
        private float _deadline;
        private float _nextScanTime;
        private int _serial;
        private int _seenDay = -1;

        public bool IsInteracting => _active != null;
        public IVillagerInteractable Active => _active;
        public InteractionResponse ActiveResponse => _activeResponse;
        public int Serial => _serial;

        public event Action<InteractionResult, InteractionEndReason> OnInteractionEnded;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _toolHandler = GetComponent<AI_ToolHandler>();
            _status = GetComponent<AI_Status>();
            _movable = GetComponent<INetworkMovable>();

            _core.OnRoutineApplied += HandleRoutineApplied;
            _core.OnRoutineCleared += HandleRoutineCleared;

            if (_status != null)
            {
                _status.OnDied += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (_core != null)
            {
                _core.OnRoutineApplied -= HandleRoutineApplied;
                _core.OnRoutineCleared -= HandleRoutineCleared;
            }

            if (_status != null)
            {
                _status.OnDied -= HandleDied;
            }
        }

        public void Configure(AIRuntimeContext context)
        {
            _context = context;
        }

        private void Update()
        {
            if (_context == null)
            {
                return;
            }

            if (!_core.IsSimulating)
            {
                Cancel(InteractionEndReason.RoutineChanged);
                return;
            }

            CheckActive();

            if (Time.time < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.time + _context.Config.InteractionScanInterval;
            Scan();
        }

        public static InteractionResponse ResolveResponse(InteractionProfile profile, int resistance)
        {
            if (profile == null || profile.MaxResponse == InteractionResponse.None)
            {
                return InteractionResponse.None;
            }

            if (profile.MaxResponse == InteractionResponse.Glance || profile.Priority <= resistance)
            {
                return InteractionResponse.Glance;
            }

            return profile.MaxResponse;
        }

        public InteractionContext CreateContext()
        {
            RefreshSeenDay();
            float now = _context != null ? _context.GameNow : Time.time;
            return new InteractionContext(_core.LogId, transform.position, ResolveActivity(), _seenDay, now);
        }

        public bool CanPreempt(int priority)
        {
            return _active == null || _active.Profile == null || priority > _active.Profile.Priority;
        }

        public bool TryBegin(IVillagerInteractable target, InteractionResponse response, float durationOverride = -1f)
        {
            if (!InteractableRegistry.IsAlive(target) || target.Profile == null || response == InteractionResponse.None || response == InteractionResponse.Glance)
            {
                return false;
            }

            if (!CanPreempt(target.Profile.Priority))
            {
                return false;
            }

            Begin(target, response, CreateContext(), durationOverride);
            return true;
        }

        public bool CompleteActive(int serial)
        {
            if (_active == null || serial != _serial)
            {
                return false;
            }

            End(InteractionEndReason.Completed);
            return true;
        }

        public void Cancel(InteractionEndReason reason)
        {
            if (_active != null)
            {
                End(reason);
            }
        }

        private void CheckActive()
        {
            if (_active == null)
            {
                return;
            }

            if (!InteractableRegistry.IsAlive(_active))
            {
                End(InteractionEndReason.TargetLost);
            }
            else if (!_startedWhileHoming && IsMustGoHome())
            {
                End(InteractionEndReason.MustGoHome);
            }
            else if (Time.time > _deadline)
            {
                AILog.Warn(AILog.INTERACTION, $"[{_core.LogId}] Interaction timed out {_activeKind}:{_activeId}");
                End(InteractionEndReason.TargetLost);
            }
        }

        private void Scan()
        {
            InteractionContext context = CreateContext();
            int resistance = _context.Config.GetInterruptResistance(context.Activity);
            InteractableRegistry.CollectNoticeable(context, _context.Config.InteractionNoticeRadius, _candidates);

            foreach (IVillagerInteractable candidate in _candidates)
            {
                if (candidate == _active || candidate.gameObject == gameObject || _seenToday.Contains(candidate.InteractableId))
                {
                    continue;
                }

                InteractionProfile profile = candidate.Profile;
                if (profile.MaxResponse == InteractionResponse.Converse)
                {
                    continue;
                }

                InteractionResponse response = ResolveResponse(profile, resistance);
                if (response == InteractionResponse.None)
                {
                    continue;
                }

                if (response != InteractionResponse.Glance && CanPreempt(profile.Priority))
                {
                    Begin(candidate, response, context);
                    continue;
                }

                Glance(candidate, context);
            }

            _candidates.Clear();
        }

        private void Glance(IVillagerInteractable target, in InteractionContext context)
        {
            MarkSeen(target.InteractableId);
            target.BeginInteraction(context, InteractionResponse.Glance);
            InteractionResult result = target.EndInteraction(context, InteractionResponse.Glance, InteractionEndReason.Completed);
            AILog.Log(AILog.INTERACTION, _core.LogId, $"Glance {target.Profile.Label} ({context.Activity})");
            ApplyResult(result, InteractionEndReason.Completed);
        }

        private void Begin(IVillagerInteractable target, InteractionResponse response, in InteractionContext context, float durationOverride = -1f)
        {
            if (_active != null)
            {
                End(InteractionEndReason.Preempted);
            }

            InteractionProfile profile = target.Profile;
            float duration = durationOverride >= 0f ? durationOverride : Random.Range(profile.DurationMin, profile.DurationMax);
            Vector3 position = response == InteractionResponse.Approach ? target.gameObject.transform.position : transform.position;

            _active = target;
            _activeResponse = response;
            _activeId = target.InteractableId;
            _activeKind = profile.Kind;
            _startedWhileHoming = IsMustGoHome();
            _deadline = Time.time + duration + TIMEOUT_MARGIN_SECONDS;
            _serial++;

            if (response != InteractionResponse.Converse)
            {
                MarkSeen(target.InteractableId);
            }

            target.BeginInteraction(context, response);

            _core.SetBlackboardVariable(BB_INTERACTION_RESPONSE, response);
            _core.SetBlackboardVariable(BB_INTERACTION_POSITION, position);
            _core.SetBlackboardVariable(BB_INTERACTION_LOOK_POINT, target.LookPoint);
            _core.SetBlackboardVariable(BB_INTERACTION_ARRIVE_DISTANCE, profile.ApproachDistance);
            _core.SetBlackboardVariable(BB_INTERACTION_DURATION, duration);
            _core.SetBlackboardVariable(BB_IS_INTERACTING, true);
            _core.SetBlackboardVariable(BB_INTERACTION_SERIAL, _serial);
            _core.SetState(AIState.Interacting);

            _activeDebug = $"#{_serial} {response} {profile.Label} ({duration:F1}s)";
            AILog.Log(AILog.INTERACTION, _core.LogId, $"Begin {_activeDebug} priority={profile.Priority} activity={context.Activity}");
        }

        private void End(InteractionEndReason reason)
        {
            IVillagerInteractable target = _active;
            InteractionResponse response = _activeResponse;
            string id = _activeId;
            InteractionKind kind = _activeKind;

            _active = null;
            _activeResponse = InteractionResponse.None;
            _activeId = null;
            _activeKind = InteractionKind.None;
            _serial++;

            InteractionResult result = InteractableRegistry.IsAlive(target)
                ? target.EndInteraction(CreateContext(), response, reason)
                : InteractionResult.None(id, kind, response);

            _core.SetBlackboardVariable(BB_IS_INTERACTING, false);
            _core.SetBlackboardVariable(BB_INTERACTION_RESPONSE, InteractionResponse.None);
            _core.SetBlackboardVariable(BB_INTERACTION_SERIAL, _serial);
            _core.SetState(AIState.Idle);

            _activeDebug = string.Empty;
            ApplyResult(result, reason);
        }

        private void ApplyResult(in InteractionResult result, InteractionEndReason reason)
        {
            AILog.Log(AILog.INTERACTION, _core.LogId, $"End {reason} {result}");

            if (result.Witnessed && result.Stress > 0f)
            {
                AIEvents.RaiseStress(_core.LogId, $"Witness {result.Kind}:{result.InteractableId}", result.Stress);
            }

            OnInteractionEnded?.Invoke(result, reason);
        }

        private VillagerActivity ResolveActivity()
        {
            if (_active != null && _activeResponse == InteractionResponse.Converse)
            {
                return VillagerActivity.Conversation;
            }

            if (IsMustGoHome())
            {
                return VillagerActivity.ReturningHome;
            }

            if (_toolHandler != null && _toolHandler.Search != null && _toolHandler.Search.IsActive && !_toolHandler.Search.HasRequiredTool())
            {
                return VillagerActivity.ToolSearch;
            }

            if (_movable != null && _movable.CurrentSpeed > MOVING_SPEED_THRESHOLD)
            {
                return VillagerActivity.Moving;
            }

            return _core.CurrentRoutine.Category == RoutineCategory.Work ? VillagerActivity.Work : VillagerActivity.Rest;
        }

        private bool IsMustGoHome()
        {
            return _core.TryGetBlackboardVariable(AI_Core.BB_MUST_GO_HOME, out bool mustGoHome) && mustGoHome;
        }

        private void RefreshSeenDay()
        {
            int day = _core.CurrentRoutine.DayCount;
            if (day <= 0 || day == _seenDay)
            {
                return;
            }

            _seenDay = day;
            _seenToday.Clear();
            _seenDebug.Clear();
        }

        private void MarkSeen(string interactableId)
        {
            if (!string.IsNullOrEmpty(interactableId) && _seenToday.Add(interactableId))
            {
                _seenDebug.Add(interactableId);
            }
        }

        private void HandleRoutineApplied(ResolvedRoutine routine)
        {
            Cancel(InteractionEndReason.RoutineChanged);
        }

        private void HandleRoutineCleared(string reason)
        {
            Cancel(InteractionEndReason.RoutineChanged);
        }

        private void HandleDied(AI_Status status, GameObject source)
        {
            Cancel(InteractionEndReason.Died);
        }
    }
}
