using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Trace;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(GhostTrace))]
    public class Net_GhostTrace : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string _interactionPrompt = "치우기";
        [SerializeField] private float _maxClearDistance = 4f;
        [SerializeField] private float _inspectDistance = 3f;

        [Header("Quest marker")]
        [SerializeField] private Vector3 _markerLocalPosition = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float _markerFontSize = 6f;
        [SerializeField] private Color _markerColor = new Color(1f, 0.85f, 0.2f);

        private const float INSPECT_CHECK_INTERVAL = 0.5f;
        private float _nextInspectCheck;

        private GhostTrace _trace;
        private AI_QuestMarker _marker;
        private QuestManager _subscribedQuests;

        // An accepted quest is finished here (clear, cleanse, check on the missing villager)
        [SyncVar(hook = nameof(OnActiveQuestChanged))] private bool _hasActiveQuest;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _trace = GetComponent<GhostTrace>();
        }

        public override void OnStartServer()
        {
            _subscribedQuests = QuestManager.Existing;
            if (_subscribedQuests == null)
            {
                return;
            }

            _subscribedQuests.QuestStarted += HandleQuestChanged;
            _subscribedQuests.QuestEnded += HandleQuestChanged;
            RefreshActiveQuest();
        }

        public override void OnStopServer()
        {
            if (_subscribedQuests == null)
            {
                return;
            }

            _subscribedQuests.QuestStarted -= HandleQuestChanged;
            _subscribedQuests.QuestEnded -= HandleQuestChanged;
            _subscribedQuests = null;
        }

        public override void OnStartClient()
        {
            ApplyMarker(_hasActiveQuest);
        }

        private void HandleQuestChanged(IQuestView quest)
        {
            if (quest.ContextId == _trace.QuestContextId)
            {
                RefreshActiveQuest();
            }
        }

        private void RefreshActiveQuest()
        {
            _hasActiveQuest = _subscribedQuests != null && _subscribedQuests.HasActiveAt(_trace.QuestContextId);
        }

        private void OnActiveQuestChanged(bool oldValue, bool newValue)
        {
            ApplyMarker(newValue);
        }

        private void ApplyMarker(bool visible)
        {
            if (visible && _marker == null)
            {
                _marker = AI_QuestMarker.Create(transform, _markerLocalPosition, _markerFontSize, _markerColor);
            }

            if (_marker != null)
            {
                _marker.Show(visible ? AI_QuestMarker.IN_PROGRESS : null);
            }
        }

        [ServerCallback]
        private void Update()
        {
            if (!_trace.IsActive || Time.time < _nextInspectCheck)
            {
                return;
            }

            _nextInspectCheck = Time.time + INSPECT_CHECK_INTERVAL;
            string actionId = _trace.VictimId ?? _trace.Kind.ToString();
            if (!_trace.Accepts(QuestActions.INSPECT, actionId))
            {
                return;
            }

            float sqr = _inspectDistance * _inspectDistance;
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                GameObject player = connection != null && connection.identity != null ? connection.identity.gameObject : null;
                if (player != null && (player.transform.position - transform.position).sqrMagnitude <= sqr)
                {
                    QuestTargets.ReportAction(player, QuestActions.INSPECT, actionId, _trace);
                }
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && netId != 0 && _trace.ClearableByHand && PlayerActionGuard.CanAct(interactor);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            CmdRequestClear();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestClear(NetworkConnectionToClient sender = null)
        {
            GameObject player = sender != null && sender.identity != null ? sender.identity.gameObject : null;
            if (player == null)
            {
                AILog.Warn(AILog.NET, $"[{name}] Clear request without player object, ignored");
                return;
            }

            if (!_trace.ClearableByHand || !_trace.IsActive)
            {
                AILog.Warn(AILog.NET, $"[{name}] Clear request from {player.name} not applicable, ignored");
                return;
            }

            if ((player.transform.position - transform.position).sqrMagnitude > _maxClearDistance * _maxClearDistance)
            {
                AILog.Warn(AILog.NET, $"[{name}] Clear request from {player.name} too far, ignored");
                return;
            }

            AILog.Log(AILog.TRACE, _trace.InteractableId, $"Cleared by {player.name}");
            QuestTargets.ReportAction(player, QuestActions.INSPECT, _trace.VictimId ?? _trace.Kind.ToString(), _trace);
            QuestTargets.ReportAction(player, QuestActions.CLEAR, _trace.Kind.ToString(), _trace);
            _trace.Resolve(TraceResolveReason.ClearedByPlayer);
        }

        [ContextMenu("Test Clear (server)")]
        private void TestClear()
        {
            if (!isServer)
            {
                AILog.Warn(AILog.NET, $"[{name}] Test Clear needs the server (Host)");
                return;
            }

            _trace.Resolve(TraceResolveReason.ClearedByPlayer);
        }
    }
}
