using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Interaction
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Interaction))]
    public class AI_Conversation : MonoBehaviour, IVillagerInteractable, IAIConfigurable
    {
        private const float LOOK_HEIGHT = 1.6f;
        private static readonly List<AI_Conversation> _all = new List<AI_Conversation>();
        private static readonly string[] BUSY_BARKS = { "지금 바빠유!", "이따 다시 와주시겠소" };
        private static readonly string[] GREETING_BARKS = { "무슨 일이오?", "어서 오시오" };
        private static readonly System.Random _lineRandom = new System.Random();

        [SerializeField] private InteractionProfile _profile;

        [Header("Runtime")]
        [SerializeField] private string _sessionDebug;
        [SerializeField] private int _conversationsThisRoutine;

        private AI_Core _core;
        private AI_Interaction _interaction;
        private AIRuntimeContext _context;
        private int _countedRoutineSerial = -1;
        private int _joinedRoutineSerial;

        public string VillagerId => _core != null ? _core.LogId : name;
        public string DisplayName => _core != null && _core.PublicInfo != null && !string.IsNullOrEmpty(_core.PublicInfo.DisplayName) ? _core.PublicInfo.DisplayName : VillagerId;
        public AI_Core Core => _core;
        public AI_Interaction Interaction => _interaction;
        public ConversationSession Session { get; private set; }
        public int JoinedInteractionSerial { get; private set; }
        public bool IsInSession => Session != null;
        public float HearingDistance => _context != null && _context.Config != null ? _context.Config.HearingDistance : 0f;
        public AIBehaviourConfig Config => _context != null ? _context.Config : null;

        public string InteractableId => VillagerId;
        public InteractionProfile Profile => _profile;
        public Vector3 LookPoint => transform.position + Vector3.up * LOOK_HEIGHT;

        public event Action<ConversationSession, ConversationLine> OnLineSpoken;

        public static IReadOnlyList<AI_Conversation> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _all.Clear();
        }

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _interaction = GetComponent<AI_Interaction>();
        }

        private void OnEnable()
        {
            if (!_all.Contains(this))
            {
                _all.Add(this);
            }

            if (_core != null)
            {
                _core.OnSymptomTriggered += HandleSymptom;
            }
        }

        private void OnDisable()
        {
            _all.Remove(this);

            if (_core != null)
            {
                _core.OnSymptomTriggered -= HandleSymptom;
            }
        }

        // A host talking nonsense mutters to itself about its own name, job and routine (all from the villager's data)
        private void HandleSymptom(SymptomType symptom)
        {
            if (symptom != SymptomType.Nonsense)
            {
                return;
            }

            VillagerPublicInfo info = _core.PublicInfo;
            string name = info != null ? info.DisplayName : null;
            string job = info != null ? info.JobName : null;
            string routine = _core.HasRoutine && _core.CurrentRoutine.Source != null ? _core.CurrentRoutine.Source.DisplayName : null;

            // Templates that need a value the villager does not have are left out
            List<string> templates = new List<string>();
            if (!string.IsNullOrEmpty(name))
            {
                templates.Add("{name}… {name}… 그게 내 이름이 맞는가…");
                templates.Add("누가 자꾸 {name:을} 부르는디…");
            }

            if (!string.IsNullOrEmpty(job))
            {
                templates.Add("나는 {job:이} 아니여… 아니라니께…");
                templates.Add("{job} 노릇은 이제 싫다…");
            }

            if (!string.IsNullOrEmpty(routine))
            {
                templates.Add("{routine:을} 해야 하는디… 손이 말을 안 듣는구먼…");
                templates.Add("또 {routine}… 언제까지 해야 하는 것이여…");
            }

            if (templates.Count == 0)
            {
                return;
            }

            string line = TextTemplate.Fill(templates[_lineRandom.Next(templates.Count)], key =>
                key == "name" ? name : key == "job" ? job : key == "routine" ? routine : null);
            Say(line, "nonsense symptom");
        }

        public void Configure(AIRuntimeContext context)
        {
            _context = context;
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
                    reason = $"interaction ended ({_interaction.LastEndReason})";
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

        // A player can stop a villager to talk while it rests, works, walks about its routine or goes looking for a tool
        // (the behaviour tree restarts on the interaction and picks the routine or the search up again afterwards).
        // Not while it heads home before nightfall or is already held by something else
        private static bool IsTalkableActivity(VillagerActivity activity)
        {
            return activity == VillagerActivity.Rest || activity == VillagerActivity.Work || activity == VillagerActivity.Moving
                || activity == VillagerActivity.ToolSearch || activity == VillagerActivity.ReturningHome;
        }

        // Resting, at work or on the way home (alone, or in a chat with another villager): a player can stop it and talk.
        // A talk on the way home holds it up; one that falls behind runs the rest of the way (C121)
        public bool CanTalkWithPlayer()
        {
            // Home for the evening and still up: it opens the door and talks (asleep it only mumbles, Net_VillagerTalk)
            if (_core != null && _core.IsSimulating && _context != null && _core.IsHomeForNight)
            {
                return !_core.IsAsleep && !IsInSession && !_interaction.IsInteracting;
            }

            if (_core == null || !_core.IsSimulating || !_core.HasRoutine || _context == null)
            {
                return false;
            }

            VillagerActivity activity = CurrentActivity();
            if (IsInSession)
            {
                // The player outranks a chat between villagers, a rest talk or a work talk (TryStartDialogue leaves it)
                return true;
            }

            return IsTalkableActivity(activity) && !_interaction.IsInteracting;
        }

        public void RespondToPlayer(GameObject player)
        {
            string playerName = player != null ? player.name : "(test)";
            VillagerActivity activity = CurrentActivity();

            if (IsInSession || activity == VillagerActivity.Conversation)
            {
                AILog.Log(AILog.INTERACTION, VillagerId, $"Ignored {playerName} (already in conversation)");
                return;
            }

            // Already in: no "must get home" any more, just a late caller at the door
            if (_core != null && _core.IsHomeForNight)
            {
                Say(ProjectShaman.AI.Dialogue.DialogueLibrary.Line("greet.home", _lineRandom), $"at home, busy for {playerName}");
                return;
            }

            if (!IsTalkableActivity(activity) || _interaction.IsInteracting)
            {
                Bark(BUSY_BARKS, $"refused {playerName} ({activity})");
                return;
            }

            AILog.Log(AILog.STUB, VillagerId, $"Player talk with {playerName} accepted, conversation UI not implemented (dialogue branch)");
            Bark(GREETING_BARKS, $"greeted {playerName}");
        }

        // Server-side one-off line outside the random barks (quest hand-over etc.)
        public void Say(string text, string reason)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AILog.Log(AILog.INTERACTION, VillagerId, $"Say: {text} ({reason})");
            OnLineSpoken?.Invoke(null, new ConversationLine(VillagerId, text));
        }

        private void Bark(string[] lines, string reason)
        {
            string text = lines[UnityEngine.Random.Range(0, lines.Length)];
            AILog.Log(AILog.INTERACTION, VillagerId, $"Bark: {text} ({reason})");
            OnLineSpoken?.Invoke(null, new ConversationLine(VillagerId, text));
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
