using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Core), typeof(AI_Movement))]
    public class Net_AICore : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnStateChanged))]
        private AIState _networkState = AIState.Idle;

        private AI_Core aiCore;
        private AI_Movement aiMovement;
        private AI_VisualController visualController;

        void Awake()
        {
            aiCore = GetComponent<AI_Core>();
            aiMovement = GetComponent<AI_Movement>();
            visualController = GetComponent<AI_VisualController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (aiMovement != null) aiMovement.SetAgentActive(true);
            if (aiCore != null) aiCore.SetBehaviorGraphActive(true);
            if (visualController != null) visualController.UpdateAnimatorLocally = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                if (aiMovement != null) aiMovement.SetAgentActive(false);
                if (aiCore != null) aiCore.SetBehaviorGraphActive(false);
                if (visualController != null) visualController.UpdateAnimatorLocally = false;

                if (aiCore != null) aiCore.ApplyNetworkState(_networkState);
            }
        }

        [ServerCallback]
        void Update()
        {
            if (aiCore != null && _networkState != aiCore.CurrentState)
            {
                _networkState = aiCore.CurrentState;
            }
        }

        private void OnStateChanged(AIState oldVal, AIState newVal)
        {
            if (!isServer && aiCore != null)
            {
                aiCore.ApplyNetworkState(newVal);
            }
        }

        [ClientRpc]
        public void RpcTriggerEffect(string triggerName)
        {
            if (visualController != null)
            {
                visualController.TriggerActionEffect(triggerName);
            }
        }
    }
}
