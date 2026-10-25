using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_GhostEntity))]
    public class Net_GhostEntity : NetworkBehaviour
    {
        [Header("Channel gauge")]
        [SerializeField] private Vector3 _gaugeLocalPosition = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float _gaugeRadius = 0.35f;
        [SerializeField] private float _gaugeWidth = 0.08f;
        [SerializeField] private float _gaugeShowDistance = 15f;
        [SerializeField] private Color _gaugeBackColor = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField] private Color _gaugeFillColor = new Color(0.75f, 0.25f, 0.95f);

        private AI_GhostEntity _entity;
        private AI_ChannelGauge _gauge;

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

            _gauge = AI_ChannelGauge.Create(transform, _gaugeLocalPosition, _gaugeRadius, _gaugeWidth, _gaugeBackColor, _gaugeFillColor);
        }

        // Only players near the ghost see how far its action has come
        private void Update()
        {
            if (_gauge == null)
            {
                return;
            }

            float progress = ChannelProgress;
            if (progress >= 0f && !IsViewerWithin(_gaugeShowDistance))
            {
                progress = -1f;
            }

            _gauge.Show(progress);
        }

        private bool IsViewerWithin(float distance)
        {
            Camera viewer = AI_ViewerCamera.Current;
            return viewer != null && (viewer.transform.position - transform.position).sqrMagnitude <= distance * distance;
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
