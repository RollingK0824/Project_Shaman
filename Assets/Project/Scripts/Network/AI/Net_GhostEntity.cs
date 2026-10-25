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

        // Channel snapshot at the last server change: elapsed seconds then, total seconds, pause end (NetworkTime).
        // Duration 0 = not channeling
        [SyncVar] private float _channelDuration;
        [SyncVar] private float _channelElapsedAtSync;
        [SyncVar] private double _channelSyncTime;
        [SyncVar] private double _channelPausedUntil;
        // Bell: until this NetworkTime every player sees the gauge, near or far
        [SyncVar] private double _revealedUntil;

        public bool IsChanneling => _channelDuration > 0f;

        // 0..1 on every peer, -1 when not channeling
        public float ChannelProgress
        {
            get
            {
                if (!IsChanneling)
                {
                    return -1f;
                }

                double since = NetworkTime.time - System.Math.Max(_channelSyncTime, _channelPausedUntil);
                float elapsed = _channelElapsedAtSync + (float)System.Math.Max(0d, since);
                return Mathf.Clamp01(elapsed / _channelDuration);
            }
        }

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _entity.OnChannelChanged += HandleServerChannelChanged;
            _entity.OnChannelEnded += HandleServerChannelEnded;
            _entity.OnRevealed += HandleServerRevealed;
            _entity.SetSimulationActive(true);
            AILog.Log(AILog.NET, _entity.GhostId, "Ghost entity OnStartServer → Agent/BT enabled");
        }

        public override void OnStopServer()
        {
            _entity.OnChannelChanged -= HandleServerChannelChanged;
            _entity.OnChannelEnded -= HandleServerChannelEnded;
            _entity.OnRevealed -= HandleServerRevealed;
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

        // Only players near the ghost see how far its action has come, unless a bell revealed it
        private void Update()
        {
            if (_gauge == null)
            {
                return;
            }

            float progress = ChannelProgress;
            bool revealed = NetworkTime.time < _revealedUntil;
            if (progress >= 0f && !revealed && !IsViewerWithin(_gaugeShowDistance))
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

        private void HandleServerChannelChanged(AI_GhostEntity entity)
        {
            GhostActionSession session = entity.Session;
            if (session == null || !session.IsChanneling)
            {
                return;
            }

            float now = Time.time;
            double networkNow = NetworkTime.time;
            _channelSyncTime = networkNow;
            _channelElapsedAtSync = session.ChannelElapsed(now);
            _channelPausedUntil = networkNow + Mathf.Max(0f, session.ChannelPausedUntil - now);
            _channelDuration = session.ChannelDuration;
        }

        private void HandleServerChannelEnded(AI_GhostEntity entity)
        {
            _channelDuration = 0f;
        }

        private void HandleServerRevealed(AI_GhostEntity entity, float seconds)
        {
            _revealedUntil = System.Math.Max(_revealedUntil, NetworkTime.time + seconds);
        }
    }
}
