using Mirror;
using UnityEngine;
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

        private GhostTrace _trace;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _trace = GetComponent<GhostTrace>();
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
