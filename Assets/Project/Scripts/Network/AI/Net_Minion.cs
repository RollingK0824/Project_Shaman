using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Minion;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Minion))]
    public class Net_Minion : NetworkBehaviour
    {
        private AI_Minion _minion;

        private void Awake()
        {
            _minion = GetComponent<AI_Minion>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _minion.SetSimulationActive(true);
            AILog.Log(AILog.NET, name, "Minion OnStartServer → FSM enabled");
        }

        public override void OnStopServer()
        {
            _minion.SetSimulationActive(false);
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                _minion.SetSimulationActive(false);
            }
        }
    }
}
