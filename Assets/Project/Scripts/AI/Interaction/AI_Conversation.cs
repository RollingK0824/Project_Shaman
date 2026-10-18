using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Interaction))]
    public class AI_Conversation : MonoBehaviour, IVillagerInteractable
    {
        private const float LOOK_HEIGHT = 1.6f;

        [SerializeField] private InteractionProfile _profile;

        [Header("Runtime")]
        [SerializeField] private string _sessionDebug;
        [SerializeField] private int _conversationsThisRoutine;

        private AI_Core _core;
        private AI_Interaction _interaction;
        private int _countedRoutineSerial = -1;
        private int _joinedRoutineSerial;

        public string VillagerId => _core != null ? _core.LogId : name;
        public string DisplayName => _core != null && _core.PublicInfo != null && !string.IsNullOrEmpty(_core.PublicInfo.DisplayName) ? _core.PublicInfo.DisplayName : VillagerId;
        public AI_Core Core => _core;
        public AI_Interaction Interaction => _interaction;
        public ConversationSession Session { get; private set; }
        public int JoinedInteractionSerial { get; private set; }
        public bool IsInSession => Session != null;

        public string InteractableId => VillagerId;
        public InteractionProfile Profile => _profile;
        public Vector3 LookPoint => transform.position + Vector3.up * LOOK_HEIGHT;

        public event Action<ConversationSession, ConversationLine> OnLineSpoken;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _interaction = GetComponent<AI_Interaction>();
        }

        public int CountThisRoutine()
        {
            RefreshRoutineCount();
            return _conversationsThisRoutine;
        }

        public VillagerActivity CurrentActivity()
        {
            return _interaction.CreateContext().Activity;
        }

        public bool IsAvailable()
        {
            return _core != null && _core.IsSimulating && _core.HasRoutine && _profile != null && !IsInSession && !_interaction.IsInteracting;
        }

        public void Join(ConversationSession session, int interactionSerial)
        {
            RefreshRoutineCount();
            Session = session;
            JoinedInteractionSerial = interactionSerial;
            _joinedRoutineSerial = _core.CurrentRoutine.Serial;
            _conversationsThisRoutine++;
            _sessionDebug = session.ToString();
        }

        public void Leave(string reason)
        {
            if (Session == null)
            {
                return;
            }

            AILog.Log(AILog.INTERACTION, VillagerId, $"Leave conversation #{Session.Serial} ({reason})");

            if (Session.Kind == ConversationKind.RestTalk && IsHoldingConversationInteraction())
            {
                _interaction.CompleteActive(JoinedInteractionSerial);
            }

            Session = null;
            JoinedInteractionSerial = 0;
            _sessionDebug = string.Empty;
        }

        public bool IsStillValid(ConversationSession session, float leaveDistance, out string reason)
        {
            reason = null;

            if (_core == null || !_core.IsSimulating || !_core.HasRoutine)
            {
                reason = "not simulating";
                return false;
            }

            if (session.Kind == ConversationKind.RestTalk)
            {
                if (!IsHoldingConversationInteraction())
                {
                    reason = "interaction ended";
                    return false;
                }

                return true;
            }

            if (_interaction.IsInteracting)
            {
                reason = "interrupted";
                return false;
            }

            if (_core.CurrentRoutine.Serial != _joinedRoutineSerial || CurrentActivity() != VillagerActivity.Work)
            {
                reason = "work changed";
                return false;
            }

            if (!HasPartnerWithin(session, leaveDistance))
            {
                reason = "too far";
                return false;
            }

            return true;
        }

        public void Speak(ConversationSession session, ConversationLine line)
        {
            AILog.Log(AILog.INTERACTION, VillagerId, $"Say #{session.Serial} {session.Kind}: {line.Text}");
            OnLineSpoken?.Invoke(session, line);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return _core != null && _core.IsSimulating && context.VillagerId != VillagerId;
        }

        public void BeginInteraction(in InteractionContext context, InteractionResponse response)
        {
        }

        public InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason)
        {
            return InteractionResult.None(InteractableId, InteractionKind.Conversation, response);
        }

        private bool IsHoldingConversationInteraction()
        {
            return _interaction.IsInteracting && _interaction.ActiveResponse == InteractionResponse.Converse && _interaction.Serial == JoinedInteractionSerial;
        }

        private bool HasPartnerWithin(ConversationSession session, float distance)
        {
            float sqr = distance * distance;

            foreach (AI_Conversation other in session.Participants)
            {
                if (other != null && other != this && (other.transform.position - transform.position).sqrMagnitude <= sqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshRoutineCount()
        {
            int serial = _core.CurrentRoutine.Serial;
            if (serial != _countedRoutineSerial)
            {
                _countedRoutineSerial = serial;
                _conversationsThisRoutine = 0;
            }
        }
    }
}
