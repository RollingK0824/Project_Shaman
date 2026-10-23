using Mirror;
using TMPro;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Dialogue;
using System.Collections.Generic;
using ProjectShaman.AI.Interaction;
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
        private const string TOPIC_TOOL_GIVE = "tool.give";
        private const string TOPIC_TOOL_LATER = "tool.later";

        [Header("Quest marker")]
        [SerializeField] private float _markerFontSize = 6f;
        [SerializeField] private Color _markerColor = new Color(1f, 0.85f, 0.2f);

        [Header("Dialogue")]
        [SerializeField] private float _dialogueHoldSeconds = 60f;
        [SerializeField] private float _dialogueIdleTimeout = 40f;
        [SerializeField] private float _dialogueLeaveDistance = 6f;

        private AI_Conversation _conversation;
        private AI_Interaction _interaction;
        private NetworkConnectionToClient _dialogueConnection;
        private GameObject _dialoguePlayer;
        private int _holdSerial;
        private bool _holdsVillager;
        private bool _questOffer;
        private bool _toolDelivery;
        private float _dialogueDeadline;
        private TextMeshPro _marker;
        private QuestManager _subscribedQuests;

        [SyncVar(hook = nameof(OnQuestOfferChanged))] private bool _hasQuestOffer;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _conversation = GetComponent<AI_Conversation>();
            _interaction = GetComponent<AI_Interaction>();
        }

        public override void OnStartServer()
        {
            _subscribedQuests = QuestManager.Existing;
            if (_subscribedQuests == null)
            {
                return;
            }

            _subscribedQuests.QuestOffered += HandleQuestChanged;
            _subscribedQuests.QuestStarted += HandleQuestChanged;
            _subscribedQuests.QuestEnded += HandleQuestChanged;
            RefreshQuestOffer();
        }

        public override void OnStopServer()
        {
            EndDialogue(null);

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
            ApplyMarker(_hasQuestOffer);
        }

        private void HandleQuestChanged(IQuestView quest)
        {
            if (quest.GiverId == _conversation.VillagerId)
            {
                RefreshQuestOffer();
            }
        }

        private void RefreshQuestOffer()
        {
            _hasQuestOffer = _subscribedQuests != null && _subscribedQuests.HasOffer(_conversation.VillagerId);
        }

        private void OnQuestOfferChanged(bool oldValue, bool newValue)
        {
            ApplyMarker(newValue);
        }

        private void ApplyMarker(bool visible)
        {
            if (visible && _marker == null)
            {
                GameObject markerObject = new GameObject("QuestMarker");
                markerObject.transform.SetParent(transform, false);
                markerObject.transform.localPosition = MARKER_LOCAL_POSITION;
                _marker = markerObject.AddComponent<TextMeshPro>();
                _marker.text = "!";
                _marker.fontSize = _markerFontSize;
                _marker.fontStyle = FontStyles.Bold;
                _marker.alignment = TextAlignmentOptions.Center;
                _marker.color = _markerColor;
                _marker.outlineColor = Color.black;
                _marker.outlineWidth = 0.3f;
            }

            if (_marker != null)
            {
                _marker.gameObject.SetActive(visible);
            }
        }

        private void LateUpdate()
        {
            if (_marker == null || !_marker.gameObject.activeSelf)
            {
                return;
            }

            _marker.transform.localPosition = MARKER_LOCAL_POSITION;
            AI_ViewerCamera.Face(_marker.transform);
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

            // A pending offer opens the quest window (accept / decline) instead of the usual greeting
            QuestManager quests = QuestManager.Existing;
            IQuestView offer = quests != null && quests.HasOffer(villagerId) ? FindOffer(quests, villagerId) : null;
            // Bringing back the tool this villager asked for comes before anything else
            IQuestView delivery = FindToolDelivery(quests, villagerId, player, out Net_ToolItem carried);

            if (_dialogueConnection != null && _dialogueConnection != sender)
            {
                _conversation.Say("다른 분이랑 얘기 중이오", "dialogue busy");
            }
            else if (delivery != null)
            {
                StartToolDelivery(sender, player, delivery, carried);
            }
            else if (offer != null)
            {
                StartQuestOffer(sender, player, offer);
            }
            else if (!TryStartDialogue(sender, player))
            {
                _conversation.RespondToPlayer(player);
            }

            GameplayEvents.RaiseActionDone(playerId, QuestActions.TALK, villagerId);
        }

        // ----- Player dialogue (server) -----

        private bool TryStartDialogue(NetworkConnectionToClient sender, GameObject player)
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
            _toolDelivery = false;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, config), out string[] ids, out string[] labels);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Dialogue with {player.name} ({ids.Length} topics)");
            TargetOpenDialogue(sender, _conversation.DisplayName, VillagerDialogueService.Greeting(), ids, labels);
            return true;
        }

        // The quest window: what is asked and the reward, answered with accept or decline.
        // The villager is held still while it is open when it can be (a working villager keeps working).
        private void StartQuestOffer(NetworkConnectionToClient sender, GameObject player, IQuestView offer)
        {
            if (_conversation.IsInSession)
            {
                _conversation.Leave("player came about a request");
            }

            _holdsVillager = _interaction != null && _interaction.TryBegin(PlayerTalkTarget.For(player), InteractionResponse.Stop, _dialogueHoldSeconds);
            _holdSerial = _holdsVillager ? _interaction.Serial : -1;
            _dialogueConnection = sender;
            _dialoguePlayer = player;
            _questOffer = true;
            _toolDelivery = false;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Quest offer shown to {player.name}: #{offer.InstanceId} {offer.TitleText} (held={_holdsVillager})");
            TargetOpenDialogue(sender, _conversation.DisplayName, DescribeOffer(offer), new[] { TOPIC_QUEST_ACCEPT, TOPIC_QUEST_DECLINE }, new[] { "맡겠소 (수락)", "다음에 하겠소 (거절)" });
        }

        private static string DescribeOffer(IQuestView offer)
        {
            string text = $"<b>{offer.TitleText}</b>\n{offer.ObjectiveText}";
            QuestEffect reward = offer.Definition.Reward;
            if (!string.IsNullOrEmpty(reward.Description))
            {
                text += $"\n<color=#E6C46A>보상: {reward.Description}</color>";
            }

            return text;
        }

        private void AnswerQuestOffer(NetworkConnectionToClient sender, string topicId)
        {
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

            _holdsVillager = _interaction != null && _interaction.TryBegin(PlayerTalkTarget.For(player), InteractionResponse.Stop, _dialogueHoldSeconds);
            _holdSerial = _holdsVillager ? _interaction.Serial : -1;
            _dialogueConnection = sender;
            _dialoguePlayer = player;
            _questOffer = false;
            _toolDelivery = true;
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            string toolName = AIDisplayNames.Tool(carried.Tool.TypeId);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Tool delivery shown to {player.name}: #{quest.InstanceId} {carried.Tool.InstanceId} (held={_holdsVillager})");
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
            carried = player != null ? Net_ToolItem.FindCarriedBy(player) : null;
            if (quests == null || carried == null)
            {
                return null;
            }

            foreach (IQuestView quest in quests.Quests)
            {
                if (quest.GiverId == giverId && quest.State == QuestState.Active &&
                    quest.Definition.ObjectiveType == QuestActions.DELIVER && quest.TargetId == carried.Tool.TypeId)
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
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Asked {topicId}: {answer}");

            if (ends)
            {
                TargetFinal(sender, answer);
                EndDialogue("finished");
                return;
            }

            _dialogueDeadline = Time.time + _dialogueIdleTimeout;
            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, config), out string[] ids, out string[] labels);
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
                    TargetFinal(_dialogueConnection, lost ? "아이고, 이만 가 봐야겠소" : "살펴 가시오.");
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
