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

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Conversation))]
    public class Net_VillagerTalk : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string _interactionPrompt = "말 걸기";
        [SerializeField] private float _maxTalkDistance = 4f;

        [Header("Quest marker")]
        [SerializeField] private float _markerHeight = 2.9f;
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
                markerObject.transform.localPosition = Vector3.up * _markerHeight;
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
            if (_marker == null || !_marker.gameObject.activeSelf || Camera.main == null)
            {
                return;
            }

            _marker.transform.rotation = Camera.main.transform.rotation;
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

            // A pending offer is answered by the quest hand-over instead of the usual greeting
            QuestManager quests = QuestManager.Existing;
            if (quests != null && quests.HasOffer(villagerId))
            {
                IQuestView offer = FindOffer(quests, villagerId);

                if (quests.TryAcceptQuest(playerId, villagerId))
                {
                    _conversation.Say(offer != null ? offer.ObjectiveText : "부탁 하나만 들어주시오", "quest accepted");
                }
                else
                {
                    _conversation.Say("지금은 더 맡기기 어렵겠소", "quest refused (limit or no target)");
                }
            }
            else if (_dialogueConnection != null && _dialogueConnection != sender)
            {
                _conversation.Say("다른 분이랑 얘기 중이오", "dialogue busy");
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
            _dialogueDeadline = Time.time + _dialogueIdleTimeout;

            SplitTopics(VillagerDialogueService.BuildTopics(_conversation, config), out string[] ids, out string[] labels);
            AILog.Log(AILog.INTERACTION, _conversation.VillagerId, $"Dialogue with {player.name} ({ids.Length} topics)");
            TargetOpenDialogue(sender, _conversation.DisplayName, VillagerDialogueService.Greeting(), ids, labels);
            return true;
        }

        [Command(requiresAuthority = false)]
        private void CmdAsk(string topicId, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender != _dialogueConnection)
            {
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

            bool lost = _dialoguePlayer == null || !_interaction.IsInteracting || _interaction.Serial != _holdSerial;
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

            if (_interaction != null)
            {
                _interaction.CompleteActive(_holdSerial);
            }

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
            AI_SpeechBubble bubble = GetComponent<AI_SpeechBubble>();
            VillagerDialoguePanel.Show(bubble != null ? bubble.Font : null, speaker, line, ids, labels, topicId => CmdAsk(topicId), () => CmdCloseDialogue());
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
