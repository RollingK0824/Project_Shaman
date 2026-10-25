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

        // Server NetworkTime of the current channel; end 0 = not channeling
        [SyncVar] private double _channelStartTime;
        [SyncVar] private double _channelEndTime;

        public bool IsChanneling => _channelEndTime > 0d;

        // 0..1 on every peer, -1 when not channeling
        public float ChannelProgress
        {
            get
            {
                if (!IsChanneling)
                {
                    return -1f;
                }

                double length = _channelEndTime - _channelStartTime;
                return length <= 0d ? 1f : Mathf.Clamp01((float)((NetworkTime.time - _channelStartTime) / length));
            }
        }

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _entity.OnChannelStarted += HandleServerChannelStarted;
            _entity.OnChannelEnded += HandleServerChannelEnded;
            _entity.SetSimulationActive(true);
            AILog.Log(AILog.NET, _entity.GhostId, "Ghost entity OnStartServer → Agent/BT enabled");
        }

        public override void OnStopServer()
        {
            _entity.OnChannelStarted -= HandleServerChannelStarted;
            _entity.OnChannelEnded -= HandleServerChannelEnded;
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                _entity.SetSimulationActive(false);
            }
        }

        private void HandleServerChannelStarted(AI_GhostEntity entity, float duration)
        {
            _channelStartTime = NetworkTime.time;
            _channelEndTime = _channelStartTime + duration;
        }

        private void HandleServerChannelEnded(AI_GhostEntity entity)
        {
            _channelStartTime = 0d;
            _channelEndTime = 0d;
        }
    }
}
