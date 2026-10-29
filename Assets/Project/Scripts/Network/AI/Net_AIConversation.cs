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
        // Gesture layer state played on each client that hears the line
        private const string TALK_GESTURE = "Talk";

        private AI_Conversation _conversation;
        private AI_SpeechBubble _bubble;
        private AI_VisualController _visual;

        private void Awake()
        {
            _conversation = GetComponent<AI_Conversation>();
            _bubble = GetComponent<AI_SpeechBubble>();
            _visual = GetComponent<AI_VisualController>();
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
            float hearingDistance = _conversation.HearingDistance;
            if (hearingDistance <= 0f)
            {
                RpcSay(line.Text);
                return;
            }

            float sqrDistance = hearingDistance * hearingDistance;

            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection == null || connection.identity == null)
                {
                    continue;
                }

                if ((connection.identity.transform.position - transform.position).sqrMagnitude <= sqrDistance)
                {
                    TargetSay(connection, line.Text);
                }
            }
        }

        [ClientRpc]
        private void RpcSay(string text)
        {
            ShowLine(text);
        }

        [TargetRpc]
        private void TargetSay(NetworkConnectionToClient target, string text)
        {
            ShowLine(text);
        }

        private void ShowLine(string text)
        {
            if (_visual != null)
            {
                _visual.PlayGesture(TALK_GESTURE);
            }

            if (_bubble == null)
            {
                AILog.Warn(AILog.NET, $"[{name}] Speech bubble missing, line dropped: {text}");
                return;
            }

            _bubble.Show(text);
        }
    }
}
