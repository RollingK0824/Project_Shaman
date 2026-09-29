using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_GhostEntity))]
    public class Net_GhostEntity : NetworkBehaviour
    {
        private AI_GhostEntity _entity;

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _entity.SetSimulationActive(true);
            AILog.Log(AILog.NET, _entity.GhostId, "Ghost entity OnStartServer → Agent/BT enabled");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                _entity.SetSimulationActive(false);
            }
        }
    }
}
