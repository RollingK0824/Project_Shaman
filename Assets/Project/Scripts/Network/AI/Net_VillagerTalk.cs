using Mirror;
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

        private AI_Conversation _conversation;

        public string InteractionPrompt => _interactionPrompt;

        private void Awake()
        {
            _conversation = GetComponent<AI_Conversation>();
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

            _conversation.RespondToPlayer(player);
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
