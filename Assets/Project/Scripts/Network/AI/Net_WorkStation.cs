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

        [SyncVar(hook = nameof(OnResetVisualShownChanged))] private bool _resetVisualShown = true;

        private void Awake()
        {
            _station = GetComponent<WorkStation>();
            _networkAnimator = GetComponent<NetworkAnimator>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _station.OnWorkAccepted += HandleWorkAccepted;
            _station.OnResetVisualChanged += HandleResetVisualChanged;
        }

        public override void OnStopServer()
        {
            _station.OnWorkAccepted -= HandleWorkAccepted;
            _station.OnResetVisualChanged -= HandleResetVisualChanged;
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _station.ApplyResetVisual(_resetVisualShown);
        }

        [Server]
        private void HandleResetVisualChanged(bool shown)
        {
            _resetVisualShown = shown;
        }

        private void OnResetVisualShownChanged(bool oldValue, bool newValue)
        {
            _station.ApplyResetVisual(newValue);
        }

        [Server]
        private void HandleWorkAccepted(string actorId, string actionId)
        {
            // The hit animation is optional (an anvil or a washing stone has an empty Animator): the work counts without it
            if (_networkAnimator.animator == null || _networkAnimator.animator.runtimeAnimatorController == null)
            {
                return;
            }

            _networkAnimator.SetTrigger(_hitTrigger);
            AILog.Log(AILog.NET, actorId, $"Station {_station.StationId} NetworkAnimator trigger '{_hitTrigger}'");
        }
    }
}
