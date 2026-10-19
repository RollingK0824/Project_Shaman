using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interaction;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Conversation))]
    public class Net_AIConversation : NetworkBehaviour
    {
        private AI_Conversation _conversation;
        private AI_SpeechBubble _bubble;

        private void Awake()
        {
            _conversation = GetComponent<AI_Conversation>();
            _bubble = GetComponent<AI_SpeechBubble>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _conversation.OnLineSpoken += HandleLineSpoken;
        }

        public override void OnStopServer()
        {
            _conversation.OnLineSpoken -= HandleLineSpoken;
            base.OnStopServer();
        }

        private void HandleLineSpoken(ConversationSession session, ConversationLine line)
        {
            RpcSay(line.Text);
        }

        [ClientRpc]
        private void RpcSay(string text)
        {
            if (_bubble == null)
            {
                AILog.Warn(AILog.NET, $"[{name}] Speech bubble missing, line dropped: {text}");
                return;
            }

            _bubble.Show(text);
        }
    }
}
