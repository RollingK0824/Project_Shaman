using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Dialogue;
using System.Collections.Generic;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.Work;
using ProjectShaman.AI.World;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Conversation))]
    public class Net_VillagerTalk : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string _interactionPrompt = "말 걸기";
        [SerializeField] private float _maxTalkDistance = 4f;

        private static readonly Vector3 MARKER_LOCAL_POSITION = new Vector3(0f, 2f, 0f);
        private const string TOPIC_QUEST_ACCEPT = "quest.accept";
        private const string TOPIC_QUEST_DECLINE = "quest.decline";
        private const string TOPIC_QUEST_TALK = "quest.talk";
        private const string TOPIC_TOOL_GIVE = "tool.give";
        private const string TOPIC_TOOL_LATER = "tool.later";

        [Header("Quest marker")]
        [SerializeField] private float _markerFontSize = 6f;
        [SerializeField] private Color _markerColor = new Color(1f, 0.85f, 0.2f);
        [Tooltip("While the villager works: the request is there, but wait until it rests")]
        [SerializeField] private Color _markerBusyColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);

        [Header("Dialogue")]
        [SerializeField] private float _dialogueHoldSeconds = 60f;
        [SerializeField] private float _dialogueIdleTimeout = 40f;
        [SerializeField] private float _dialogueLeaveDistance = 6f;

        private AI_Core _core;
        private AI_Conversation _conversation;
        private AI_Interaction _interaction;
        private NetworkConnectionToClient _dialogueConnection;
        private GameObject _dialoguePlayer;
        private int _holdSerial;
        private bool _holdsVillager;
        private bool _questOffer;
        private bool _offerCanTalk;
        private bool _toolDelivery;
        // The "today" routine the next question asks about (one at a time, from the first)
        private int _todayNext;
        private int _anomalyNext;
        private float _dialogueDeadline;
        private AI_QuestMarker _marker;
        private QuestManager _subscribedQuests;
        private readonly System.Random _random = new System.Random();

        [SyncVar(hook = nameof(OnQuestMarkerChanged))] private bool _hasQuestOffer;
        // An accepted quest is finished by coming back to this villager (delivery to the giver ...)
        [SyncVar(hook = nameof(OnQuestMarkerChanged))] private bool _hasActiveQuest;
        // Working, a request waits for the rest: the marker greys out and a talk gets a "later" line (no window, no hold)
        [SyncVar(hook = nameof(OnQuestMarkerChanged))] private bool _isWorking;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _conversation = GetComponent<AI_Conversation>();
            _interaction = GetComponent<AI_Interaction>();
        }

        public override void OnStartServer()
        {
            if (_core != null)
            {
                _core.OnMustGoHomeChanged += HandleMustGoHomeChanged;
                _core.OnAsleepChanged += HandleAsleepChanged;
            }

            _subscribedQuests = QuestManager.Existing;
            if (_subscribedQuests == null)
            {
                return;
            }

            _subscribedQuests.QuestOffered += HandleQuestChanged;
            _subscribedQuests.QuestStarted += HandleQuestChanged;
            _subscribedQuests.QuestEnded += HandleQuestChanged;
            RefreshQuestMarker();
        }

        public override void OnStopServer()
        {
            EndDialogue(null);

            if (_core != null)
            {
                _core.OnMustGoHomeChanged -= HandleMustGoHomeChanged;
                _core.OnAsleepChanged -= HandleAsleepChanged;
            }

            if (_subscribedQuests == null)
            {
                return;
            }

            _subscribedQuests.QuestOffered -= HandleQuestChanged;
            _subscribedQuests.QuestStarted -= HandleQuestChanged;
            _subscribedQuests.QuestEnded -= HandleQuestChanged;
            _subscribedQuests = null;
        }

        public override void OnStartClient()
        {
            ApplyMarker();
        }

        private void HandleQuestChanged(IQuestView quest)
        {
            string villagerId = _conversation.VillagerId;
            if (quest.GiverId == villagerId || quest.ContextId == villagerId)
            {
                RefreshQuestMarker();
            }
        }

        private void HandleMustGoHomeChanged(bool mustGoHome)
        {
            RefreshQuestMarker();
        }

        private void HandleAsleepChanged(bool asleep)
        {
            RefreshQuestMarker();
        }

        private bool IsAsleep => _core != null && _core.IsAsleep;
        // On a work routine (resting in place of one, free time and the way home are not work)
        private bool IsWorking => _core != null && _core.HasRoutine && _core.CurrentRoutine.Category == RoutineCategory.Work && !_core.MustGoHome && !_core.IsHomeForNight;

        private void RefreshQuestMarker()
        {
            string villagerId = _conversation.VillagerId;
            // Asleep the villager can neither ask nor take anything. Going home it still can: an offer not taken fails
            // at nightfall (C129), so hiding "!" in the evening would take it away
            _hasQuestOffer = _subscribedQuests != null && _subscribedQuests.HasOffer(villagerId) && !IsAsleep;
            _hasActiveQuest = _subscribedQuests != null && _subscribedQuests.HasActiveAt(villagerId) && !IsAsleep;
        }

        private void OnQuestMarkerChanged(bool oldValue, bool newValue)
        {
            ApplyMarker();
        }

        // An offer waiting here outranks a quest to finish here
        private void ApplyMarker()
        {
            string symbol = _hasQuestOffer ? AI_QuestMarker.OFFER : _hasActiveQuest ? AI_QuestMarker.IN_PROGRESS : null;
            if (symbol != null && _marker == null)
            {
                _marker = AI_QuestMarker.Create(transform, MARKER_LOCAL_POSITION, _markerFontSize, _markerColor);
            }

            if (_marker != null)
            {
                _marker.SetColor(_isWorking && !_hasQuestOffer ? _markerBusyColor : _markerColor);
                _marker.Show(symbol);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && netId != 0 && PlayerActionGuard.CanAct(interactor);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            CmdRequestTalk();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestTalk(NetworkConnectionToClient sender = null)
        {
            GameObject player = sender != null && sender.identity != null ? sender.identity.gameObject : null;
            if (player == null)
            {
                AILog.Warn(AILog.NET, $"[{name}] Talk request without player object, ignored");
                return;
            }

            if ((player.transform.position - transform.position).sqrMagnitude > _maxTalkDistance * _maxTalkDistance)
            {
                AILog.Warn(AILog.NET, $"[{name}] Talk request from {player.name} too far, ignored");
                return;
            }

            uint playerId = player.TryGetComponent(out NetworkIdentity identity) ? identity.netId : 0;
            string villagerId = _conversation.VillagerId;

            // Asleep in the house: no window, no hand-over, it does not count as a talk. A mumble shows it is asleep
            if (IsAsleep)
            {
                _conversation.Say(DialogueLibrary.Line("sleep", _random), $"asleep, {player.name} tried to talk");
                return;
            }

            // A pending offer opens the quest window (accept / decline) instead of the usual greeting, going home included
            // (not while working: see below)
            QuestManager quests = QuestManager.Existing;
            IQuestView offer = quests != null && quests.HasOffer(villagerId) ? FindOffer(quests, villagerId) : null;
            // Bringing back the tool this villager asked for comes before anything else but work, going home included:
            // the villager stops for it even if that makes them late (C120)
            IQuestView delivery = FindToolDelivery(quests, villagerId, player, out Net_ToolItem carried);

            if (_dialogueConnection != null && _dialogueConnection != sender)
            {
                _conversation.Say("다른 분이랑 얘기 중이오", "dialogue busy");
            }
            else if (delivery != null && IsWorking)
            {
                // Working: the hand-over waits for the rest (the grey marker says so). A villager without the tool rests in place
                // of the work that needs it, so this work does not, and its deadline is a later day (C120).
                // An offer is still taken at work or on the way there: otherwise it could hardly ever be accepted
                _conversation.Say("일 끝나고 받으리다. 조금만 기다려 주시오.", "busy working, delivery later");
            }
            else if (delivery != null)
            {
                StartToolDelivery(sender, player, delivery, carried);
            }
            else if (offer != null)
            {
                StartQuestOffer(sender, player, offer);
            }
            else
            {
                // The day after an exorcism the freed villager thanks the player first (in the window, or in passing)
                string thanks = VillagerDialogueService.TakeThanks(_conversation, playerId);
                if (!TryStartDialogue(sender, player, thanks))
                {
                    if (thanks != null)
                    {
                        _conversation.Say(thanks, "thanks for the exorcism");
                    }
                    else
                    {
                        _conversation.RespondToPlayer(player);
                    }
                }
            }

            GameplayEvents.RaiseActionDone(playerId, QuestActions.TALK, villagerId);
        }

        // ----- Player dialogue (server) -----

        private bool TryStartDialogue(NetworkConnectionToClient sender, GameObject player, string greeting = null)
        {
            AIBehaviourConfig config = _conversation.Config;
            if (_interaction == null || config == null || !_conversation.CanTalkWithPlayer())
            {
                return false;
            }

            if (_conversation.IsInSession)
            {
                _conversation.Leave("player came to talk");
            }

            if (!_interaction.TryBegin(PlayerTalkTarget.For(player), InteractionResponse.Stop, _dialogueHoldSeconds))
            {
                return false;
            }

            _dialogueConnection = sender;
            _dialoguePlayer = player;
            _holdSerial = _interaction.Serial;
            _holdsVillager = true;
            _questOffer = false;
            _todayNext = 0;
            _anomalyNext = 0;
            _toolDelivery = false;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, config, _todayNext, _anomalyNext), out string[] ids, out string[] labels);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Dialogue with {player.name} ({ids.Length} topics)");
            // Called on at home in the evening: a late-caller greeting instead of the daytime one
            string opening = greeting ?? (_core != null && _core.IsHomeForNight ? DialogueLibrary.Line("greet.home", _random) : VillagerDialogueService.Greeting());
            TalkGesture();
            TargetOpenDialogue(sender, _conversation.DisplayName, opening, ids, labels);
            return true;
        }

        // The quest window: what is asked and the reward, answered with accept or decline.
        // The villager is held still while it is open when it can be (never opened while it works, CmdRequestTalk).
        private void StartQuestOffer(NetworkConnectionToClient sender, GameObject player, IQuestView offer)
        {
            // Judged as the usual dialogue judges it, before the villager is held (held, it counts as busy)
            bool couldTalk = _conversation.Config != null && _conversation.CanTalkWithPlayer();

            if (_conversation.IsInSession)
            {
                _conversation.Leave("player came about a request");
            }

            // A request outranks an everyday interaction: a villager looking in on a neighbour on the way home stops for it
            _holdsVillager = _interaction != null && _interaction.TryBegin(PlayerTalkTarget.ForRequest(player), InteractionResponse.Stop, _dialogueHoldSeconds);
            _holdSerial = _holdsVillager ? _interaction.Serial : -1;
            _dialogueConnection = sender;
            _dialoguePlayer = player;
            _questOffer = true;
            _offerCanTalk = couldTalk && _holdsVillager;
            _toolDelivery = false;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Quest offer shown to {player.name}: #{offer.InstanceId} {offer.TitleText} (held={_holdsVillager}, talk={_offerCanTalk})");
            TalkGesture();
            // The usual topics stay one click away: with the quest log full the offer can only be declined, and rumours still matter
            if (_offerCanTalk)
            {
                TargetOpenDialogue(sender, _conversation.DisplayName, DescribeOffer(offer), new[] { TOPIC_QUEST_ACCEPT, TOPIC_QUEST_DECLINE, TOPIC_QUEST_TALK },
                    new[] { "맡겠소 (수락)", "다음에 하겠소 (거절)", "다른 얘기를 하고 싶소" });
            }
            else
            {
                TargetOpenDialogue(sender, _conversation.DisplayName, DescribeOffer(offer), new[] { TOPIC_QUEST_ACCEPT, TOPIC_QUEST_DECLINE }, new[] { "맡겠소 (수락)", "다음에 하겠소 (거절)" });
            }
        }

        // From the quest window to the usual topics in the same window, the villager still held. Talking again later
        // brings the offer back while it is open
        private void SwitchOfferToTalk(NetworkConnectionToClient sender)
        {
            if (!_offerCanTalk || !_holdsVillager)
            {
                TalkGesture();
                TargetFinal(sender, "지금은 얘기할 틈이 없소.");
                EndDialogue("talk after offer refused");
                return;
            }

            _questOffer = false;
            _todayNext = 0;
            _anomalyNext = 0;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;
            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, _conversation.Config, _todayNext, _anomalyNext), out string[] ids, out string[] labels);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Quest offer left for the usual dialogue ({ids.Length} topics)");
            TalkGesture();
            TargetAnswer(sender, "그러시오, 무슨 얘기요?", ids, labels);
        }

        private string DescribeOffer(IQuestView offer)
        {
            string text = $"<b>{offer.TitleText}</b>\n{offer.ObjectiveText}";
            string deadline = DescribeToolDeadline(offer);
            if (deadline != null)
            {
                text += $"\n{deadline}";
            }

            QuestEffect reward = offer.Definition.Reward;
            if (!string.IsNullOrEmpty(reward.Description))
            {
                text += $"\n<color=#E6C46A>보상: {reward.Description}</color>";
            }

            return text;
        }

        // A FindTool request lasts until the villager next needs the tool (C120): said up front so the end is no surprise
        private string DescribeToolDeadline(IQuestView offer)
        {
            if (offer.Definition.ObjectiveType != QuestActions.DELIVER || _core == null || _core.Profile == null || _core.Profile.Schedule == null)
            {
                return null;
            }

            // The asking day is given up (the villager rests through its work with that tool), so the deadline is the
            // first use on a later day. Asked on an earlier day and still open: the next use from now
            ResolvedRoutine current = _core.CurrentRoutine;
            VillagerSchedule schedule = _core.Profile.Schedule;
            bool askedToday = offer.DayIssued == current.DayCount;
            int fromSlot = askedToday ? schedule.SlotsPerDay - 1 : current.Slot;
            if (current.DayCount <= 0 || !schedule.TryFindNextToolUse(current.DayCount, fromSlot, offer.TargetId, out int daysAhead, out RoutineEntry routine))
            {
                return null;
            }

            string when = daysAhead == 0 ? "이따" : daysAhead == 1 ? "내일" : daysAhead == 2 ? "모레" : $"{daysAhead}일 뒤";
            string work = string.IsNullOrEmpty(routine.DisplayName) ? routine.RoutineId : routine.DisplayName;
            return $"{(askedToday ? "오늘 일은 글렀고, " : string.Empty)}{when} {work} 전까지만 찾아 주면 되오.";
        }

        private void AnswerQuestOffer(NetworkConnectionToClient sender, string topicId)
        {
            if (topicId == TOPIC_QUEST_TALK)
            {
                SwitchOfferToTalk(sender);
                return;
            }

            QuestManager quests = QuestManager.Existing;
            string villagerId = _conversation.VillagerId;
            string line;

            if (topicId == TOPIC_QUEST_ACCEPT)
            {
                uint playerId = _dialoguePlayer != null && _dialoguePlayer.TryGetComponent(out NetworkIdentity identity) ? identity.netId : 0;
                bool accepted = quests != null && quests.TryAcceptQuest(playerId, villagerId);
                line = accepted ? "고맙소, 잘 부탁하오." : "지금은 더 맡기기 어렵겠소.";
                AILog.Log(AILog.INTERACTION, villagerId, accepted ? "Quest accepted" : "Quest accept refused (limit, taken or gone)");
            }
            else
            {
                line = "알겠소. 생각이 바뀌면 다시 오시오.";
                AILog.Log(AILog.INTERACTION, villagerId, "Quest declined");
            }

            TalkGesture();
            TargetFinal(sender, line);
            EndDialogue(topicId == TOPIC_QUEST_ACCEPT ? "quest answered" : "quest declined");
        }

        // The player carries a tool of the kind this villager asked to find: offer to hand it over
        private void StartToolDelivery(NetworkConnectionToClient sender, GameObject player, IQuestView quest, Net_ToolItem carried)
        {
            if (_conversation.IsInSession)
            {
                _conversation.Leave("player brought a tool");
            }

            _holdsVillager = _interaction != null && _interaction.TryBegin(PlayerTalkTarget.ForRequest(player), InteractionResponse.Stop, _dialogueHoldSeconds);
            _holdSerial = _holdsVillager ? _interaction.Serial : -1;
            _dialogueConnection = sender;
            _dialoguePlayer = player;
            _questOffer = false;
            _toolDelivery = true;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            string toolName = AIDisplayNames.Tool(carried.Tool.TypeId);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Tool delivery shown to {player.name}: #{quest.InstanceId} {carried.Tool.InstanceId} (held={_holdsVillager})");
            TalkGesture();
            TargetOpenDialogue(sender, _conversation.DisplayName, $"<b>{quest.TitleText}</b>\n그 {toolName}, 내 것 같구려. 돌려주시겠소?",
                new[] { TOPIC_TOOL_GIVE, TOPIC_TOOL_LATER }, new[] { $"{toolName}{AIDisplayNames.Josa(toolName, "을", "를")} 건넨다", "나중에 주겠소" });
        }

        private void AnswerToolDelivery(NetworkConnectionToClient sender, string topicId)
        {
            string villagerId = _conversation.VillagerId;
            string line;

            if (topicId != TOPIC_TOOL_GIVE)
            {
                line = "그러시오. 꼭 돌려주시오.";
                AILog.Log(AILog.INTERACTION, villagerId, "Tool delivery put off");
            }
            else if (FindToolDelivery(QuestManager.Existing, villagerId, _dialoguePlayer, out Net_ToolItem carried) == null)
            {
                line = "어허, 그건 내 것이 아니구려.";
                AILog.Log(AILog.INTERACTION, villagerId, "Tool delivery refused (tool or request gone)");
            }
            else
            {
                HandOver(carried);
                line = "찾아 주셨구려, 정말 고맙소!";
            }

            TalkGesture();
            TargetFinal(sender, line);
            EndDialogue(topicId == TOPIC_TOOL_GIVE ? "tool delivered" : "tool delivery put off");
        }

        // The tool leaves the player's hands for the villager's (at their feet when their hands are full),
        // then the delivery is reported so the request completes
        private void HandOver(Net_ToolItem carried)
        {
            string villagerId = _conversation.VillagerId;
            ToolItem tool = carried.Tool;
            uint playerId = _dialoguePlayer.TryGetComponent(out NetworkIdentity identity) ? identity.netId : 0;

            carried.DropFromCarrier(transform.position, $"handed to {villagerId}");

            AI_ToolHandler handler = GetComponent<AI_ToolHandler>();
            bool inHand = handler != null && handler.TryPickUp(tool);
            if (!inHand && handler != null)
            {
                handler.Memory.RecordToolSighting(tool);
            }
            else if (inHand)
            {
                handler.PutAwayUnlessNeeded($"received from {_dialoguePlayer.name}");
            }

            AILog.Log(AILog.TOOL, villagerId, $"Delivered {tool.InstanceId}({tool.TypeId}) by {_dialoguePlayer.name} ({(inHand ? "in hand" : "at feet")})");
            GameplayEvents.RaiseActionDone(playerId, QuestActions.DELIVER, tool.TypeId, villagerId);
        }

        // An active FindTool request of this villager whose tool kind the player is carrying
        private static IQuestView FindToolDelivery(QuestManager quests, string giverId, GameObject player, out Net_ToolItem carried)
        {
            carried = null;
            if (quests == null || player == null)
            {
                return null;
            }

            foreach (IQuestView quest in quests.Quests)
            {
                if (quest.GiverId != giverId || quest.State != QuestState.Active || quest.Definition.ObjectiveType != QuestActions.DELIVER)
                {
                    continue;
                }

                carried = Net_ToolItem.FindCarriedBy(player, quest.TargetId);
                if (carried != null)
                {
                    return quest;
                }
            }

            return null;
        }

        [Command(requiresAuthority = false)]
        private void CmdAsk(string topicId, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender != _dialogueConnection)
            {
                return;
            }

            if (_questOffer)
            {
                AnswerQuestOffer(sender, topicId);
                return;
            }

            if (_toolDelivery)
            {
                AnswerToolDelivery(sender, topicId);
                return;
            }

            AIBehaviourConfig config = _conversation.Config;
            string answer = VillagerDialogueService.Answer(_conversation, topicId, config, out bool ends);
            int todayIndex = VillagerDialogueService.TodayIndex(topicId);
            if (todayIndex >= 0)
            {
                _todayNext = todayIndex + 1;
            }

            int anomalyIndex = VillagerDialogueService.AnomalyIndex(topicId);
            if (anomalyIndex >= 0)
            {
                _anomalyNext = anomalyIndex + 1;
            }

            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Asked {topicId}: {answer}");

            if (ends)
            {
                TalkGesture();
                TargetFinal(sender, answer);
                EndDialogue("finished");
                return;
            }

            _dialogueDeadline = Time.time + _dialogueIdleTimeout;
            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, config, _todayNext, _anomalyNext), out string[] ids, out string[] labels);
            TalkGesture();
            TargetAnswer(sender, answer, ids, labels);
        }

        [Command(requiresAuthority = false)]
        private void CmdCloseDialogue(NetworkConnectionToClient sender = null)
        {
            if (sender != null && sender == _dialogueConnection)
            {
                EndDialogue("closed by player");
            }
        }

        [ServerCallback]
        private void Update()
        {
            // Only sent when it flips (routine change), so a check per frame costs nothing on the wire
            _isWorking = IsWorking;

            if (_dialogueConnection == null)
            {
                return;
            }

            bool lost = _dialoguePlayer == null || (_holdsVillager && (!_interaction.IsInteracting || _interaction.Serial != _holdSerial));
            bool far = !lost && (_dialoguePlayer.transform.position - transform.position).sqrMagnitude > _dialogueLeaveDistance * _dialogueLeaveDistance;

            if (lost || far || Time.time > _dialogueDeadline)
            {
                if (_dialogueConnection.identity != null)
                {
                    TalkGesture();
                    TargetFinal(_dialogueConnection, lost ? "아이고, 이만 가 봐야겠소" : VillagerDialogueService.Farewell(_conversation));
                }

                EndDialogue(lost ? "villager called away" : far ? "player walked off" : "idle");
            }
        }

        private void EndDialogue(string reason)
        {
            if (_dialogueConnection == null)
            {
                return;
            }

            if (_interaction != null && _holdsVillager)
            {
                _interaction.CompleteActive(_holdSerial);
            }

            _holdsVillager = false;
            _questOffer = false;
            _toolDelivery = false;

            if (reason != null)
            {
                AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Dialogue ended ({reason})");
            }

            _dialogueConnection = null;
            _dialoguePlayer = null;
        }

        private static void SplitTopics(List<DialogueTopic> topics, out string[] ids, out string[] labels)
        {
            ids = new string[topics.Count];
            labels = new string[topics.Count];

            for (int i = 0; i < topics.Count; i++)
            {
                ids[i] = topics[i].Id;
                labels[i] = topics[i].Label;
            }
        }

        // The answer text goes only to the talking player; the occasional gesture is seen by everyone nearby
        private void TalkGesture()
        {
            if (TryGetComponent(out Net_AIConversation conversation))
            {
                conversation.TalkGesture();
            }
        }

        // ----- Player dialogue (owning client) -----

        [TargetRpc]
        private void TargetOpenDialogue(NetworkConnectionToClient target, string speaker, string line, string[] ids, string[] labels)
        {
            VillagerDialoguePanel.Show(speaker, line, ids, labels, topicId => CmdAsk(topicId), () => CmdCloseDialogue());
        }

        [TargetRpc]
        private void TargetAnswer(NetworkConnectionToClient target, string line, string[] ids, string[] labels)
        {
            VillagerDialoguePanel.UpdateContent(line, ids, labels);
        }

        [TargetRpc]
        private void TargetFinal(NetworkConnectionToClient target, string line)
        {
            VillagerDialoguePanel.ShowFinal(line);
        }

        private static IQuestView FindOffer(QuestManager quests, string giverId)
        {
            foreach (IQuestView quest in quests.Quests)
            {
                if (quest.GiverId == giverId && quest.State == QuestState.Offered)
                {
                    return quest;
                }
            }

            return null;
        }

        [ContextMenu("Test Talk (server)")]
        private void TestTalk()
        {
            if (!isServer)
            {
                AILog.Warn(AILog.NET, $"[{name}] Test Talk needs the server (Host)");
                return;
            }

            _conversation.RespondToPlayer(null);
        }
    }
}
