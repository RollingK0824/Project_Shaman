using Mirror;
using TMPro;
using UnityEngine;
using ProjectShaman.AI.Core;
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

        private AI_Conversation _conversation;
        private TextMeshPro _marker;
        private QuestManager _subscribedQuests;

        [SyncVar(hook = nameof(OnQuestOfferChanged))] private bool _hasQuestOffer;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _conversation = GetComponent<AI_Conversation>();
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
                    _conversation.Say(offer != null ? offer.Definition.Objective : "부탁 하나만 들어주시오", "quest accepted");
                }
                else
                {
                    _conversation.Say("지금은 더 맡기기 어렵겠소", "quest refused (limit or no target)");
                }
            }
            else
            {
                _conversation.RespondToPlayer(player);
            }

            GameplayEvents.RaiseActionDone(playerId, "Talk", villagerId);
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
