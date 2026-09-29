using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.World;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(WorkStation), typeof(NetworkAnimator))]
    public class Net_WorkStation : NetworkBehaviour
    {
        [SerializeField] private string _hitTrigger = "Hit";

        private WorkStation _station;
        private NetworkAnimator _networkAnimator;

        private void Awake()
        {
            _station = GetComponent<WorkStation>();
            _networkAnimator = GetComponent<NetworkAnimator>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _station.OnWorkAccepted += HandleWorkAccepted;
        }

        public override void OnStopServer()
        {
            _station.OnWorkAccepted -= HandleWorkAccepted;
            base.OnStopServer();
        }

        [Server]
        private void HandleWorkAccepted(string actorId, string actionId)
        {
            _networkAnimator.SetTrigger(_hitTrigger);
            AILog.Log(AILog.NET, actorId, $"Station {_station.StationId} NetworkAnimator trigger '{_hitTrigger}'");
        }
    }
}
