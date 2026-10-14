using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Status))]
    public class Net_AIStatus : NetworkBehaviour
    {
        private AI_Status _status;

        private void Awake()
        {
            _status = GetComponent<AI_Status>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _status.SetSimulationActive(true);
            AILog.Log(AILog.NET, name, "Status OnStartServer → judgement enabled");
        }

        public override void OnStopServer()
        {
            _status.SetSimulationActive(false);
            base.OnStopServer();
        }
    }
}
