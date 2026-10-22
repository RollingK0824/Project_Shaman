using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Trace
{
    public class GhostTrace : MonoBehaviour, IVillagerInteractable, IQuestTarget
    {
        private static int _nextSerial;

        [SerializeField] private GhostTraceKind _kind = GhostTraceKind.LivestockCorpse;
        [SerializeField] private InteractionProfile _profile;
        [SerializeField] private Transform _lookPoint;
        [SerializeField] private string _placeId;
        [SerializeField] private bool _clearableByHand = true;
        [Tooltip("Items that cleanse this trace when used on it (UseOn quests)")]
        [SerializeField] private List<string> _cleansingItemIds = new List<string> { "item_salt" };
        [SerializeField] private bool _activeOnEnable = true;

        [Header("Runtime (server only)")]
        [SerializeField] private int _serial;
        [SerializeField] private int _createdDay = -1;
        [SerializeField] private bool _isActive;
        [SerializeField] private List<string> _witnessedDebug = new List<string>();

        private readonly HashSet<string> _witnessed = new HashSet<string>();
        private AIBehaviourConfig _config;

        public GhostTraceKind Kind => _kind;
        public string PlaceId => _placeId;
        public string BundleKey => $"{_kind}@{(string.IsNullOrEmpty(_placeId) ? "-" : _placeId)}";
        public bool ClearableByHand => _clearableByHand;
        public bool IsActive => _isActive;
        public int Serial => _serial;
        public int CreatedDay => _createdDay;
        public int WitnessCount => _witnessed.Count;

        public string InteractableId => $"Trace_{_kind}_{_serial}";
        public InteractionProfile Profile => _profile;
        public string QuestContextId => InteractableId;
        public string VictimId { get; private set; }
        public string DisplayName => AIDisplayNames.Trace(_kind);
        public Vector3 Position => transform.position;
        public Vector3 LookPoint => _lookPoint != null ? _lookPoint.position : transform.position;

        public event Action<GhostTrace, TraceResolveReason> OnResolved;
        public event Action<GhostTrace, string, bool> OnWitnessed;

        private void OnEnable()
        {
            if (_activeOnEnable)
            {
                Activate(_createdDay);
            }
        }

        private void OnDisable()
        {
            InteractableRegistry.Unregister(this);
            GhostTraceRegistry.Unregister(this);
            QuestTargets.Unregister(this);
        }

        public void Initialize(AIBehaviourConfig config, int createdDay, string placeId)
        {
            _config = config;
            _createdDay = createdDay;

            if (!string.IsNullOrEmpty(placeId))
            {
                _placeId = placeId;
            }
            else if (string.IsNullOrEmpty(_placeId) && PlaceRegistry.TryFindContaining(transform.position, out PlaceArea place))
            {
                _placeId = place.PlaceId;
            }

            AILog.Log(AILog.TRACE, InteractableId, $"Placed {BundleKey} (day {_createdDay})");
        }

        public void Activate(int createdDay)
        {
            if (_isActive)
            {
                return;
            }

            _serial = ++_nextSerial;
            _createdDay = createdDay;
            _isActive = true;
            _witnessed.Clear();
            _witnessedDebug.Clear();

            InteractableRegistry.Register(this);
            GhostTraceRegistry.Register(this);
            QuestTargets.Register(this);
            AILog.Log(AILog.TRACE, InteractableId, "Active");
        }

        public void Resolve(TraceResolveReason reason)
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            InteractableRegistry.Unregister(this);
            QuestTargets.Unregister(this);
            AILog.Log(AILog.TRACE, InteractableId, $"Resolved ({reason}), witnesses={_witnessed.Count}");

            OnResolved?.Invoke(this, reason);
            GhostTraceRegistry.NotifyResolved(this, reason);
            GhostTraceRegistry.Unregister(this);
        }

        public float GetNeglectMultiplier(int dayCount)
        {
            if (_config == null || _createdDay < 0)
            {
                return 1f;
            }

            int neglectedDays = Mathf.Max(0, dayCount - _createdDay - 1);
            return Mathf.Min(_config.TraceNeglectStressMax, 1f + _config.TraceNeglectStressPerDay * neglectedDays);
        }

        // Set by the night ledger when a hunt leaves a body, so a check-on request can point at it
        public void SetVictim(string villagerId)
        {
            VictimId = villagerId;
        }

        public bool Accepts(string actionType, string actionId)
        {
            if (!_isActive)
            {
                return false;
            }

            if (actionType == QuestActions.INSPECT)
            {
                return actionId == (VictimId ?? _kind.ToString());
            }

            if (actionType == QuestActions.CLEAR)
            {
                return _clearableByHand && actionId == _kind.ToString();
            }

            return actionType == QuestActions.USE_ON && _cleansingItemIds != null && _cleansingItemIds.Contains(actionId);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return _isActive && _profile != null && !_witnessed.Contains(context.VillagerId);
        }

        public void BeginInteraction(in InteractionContext context, InteractionResponse response)
        {
        }

        public InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason)
        {
            if (_profile == null || response == InteractionResponse.None)
            {
                return InteractionResult.None(InteractableId, InteractionKind.GhostTrace, response);
            }

            bool detailed = response == InteractionResponse.Approach && reason == InteractionEndReason.Completed;
            float baseStress = detailed ? _profile.DetailedWitnessStress : _profile.WitnessStress;
            float stress = baseStress * GetNeglectMultiplier(context.DayCount);

            if (_witnessed.Add(context.VillagerId))
            {
                _witnessedDebug.Add(context.VillagerId);
            }

            OnWitnessed?.Invoke(this, context.VillagerId, detailed);
            return new InteractionResult(InteractableId, InteractionKind.GhostTrace, response, true, detailed, stress, null, _placeId, LookPoint);
        }

        private void OnDrawGizmosSelected()
        {
            if (_profile == null || !_profile.HasNoticeRadius)
            {
                return;
            }

            Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _profile.NoticeRadius);
        }
    }
}
