using System;
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

        [Header("Salt teleport")]
        [Tooltip("Played where the ghost vanishes and where it appears. Empty = no effect yet")]
        [SerializeField] private GameObject _teleportEffectPrefab;
        [SerializeField, Min(0.1f)] private float _teleportEffectLifetime = 2f;

        private AI_GhostEntity _entity;
        private AI_ChannelGauge _gauge;
        private AI_RevealOutline _outline;
        private NetworkTransformBase _networkTransform;
        private AI_Movement _movement;
        private Animator _animator;

        private const string MOVE_SPEED_PARAM = "Speed";
        private static readonly int ATTACK_TRIGGER = Animator.StringToHash("Attack");
        // Smaller changes are not worth a sync
        private const float SPEED_SYNC_STEP = 0.1f;

        // Server's NavMeshAgent speed: clients have no agent running, so they play locomotion from this
        [SyncVar] private float _moveSpeed;

        // Channel snapshot at the last server change: elapsed seconds then, total seconds, pause end and slow window
        // (NetworkTime). Duration 0 = not channeling
        [SyncVar] private float _channelDuration;
        [SyncVar] private float _channelElapsedAtSync;
        [SyncVar] private double _channelSyncTime;
        [SyncVar] private double _channelPausedUntil;
        [SyncVar] private float _channelSlowRate = 1f;
        [SyncVar] private double _channelSlowUntil;
        // Bell: until this NetworkTime the ghost is revealed (glow, gauge at any distance)
        [SyncVar] private double _revealedUntil;

        // Full yin when it came out (warning scream); a late joiner gets it with the spawn
        [SyncVar] private bool _isAwakened;

        public bool IsAwakened => _isAwakened;

        // Every peer, when the ghost swings at a player (sound, animation)
        public event Action ClientAttackSwung;

        // (from, to) on every client when salt flings the ghost elsewhere
        public event Action<Vector3, Vector3> ClientTeleported;

        public bool IsChanneling => _channelDuration > 0f;
        public bool IsRevealed => NetworkTime.time < _revealedUntil;

        // 0..1 on every peer, -1 when not channeling
        public float ChannelProgress
        {
            get
            {
                if (!IsChanneling)
                {
                    return -1f;
                }

                double elapsed = GhostActionSession.ElapsedSince(_channelElapsedAtSync, _channelSyncTime, _channelPausedUntil, _channelSlowRate, _channelSlowUntil, NetworkTime.time);
                return Mathf.Clamp01((float)(elapsed / _channelDuration));
            }
        }

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
            _networkTransform = GetComponent<NetworkTransformBase>();
            _movement = GetComponent<AI_Movement>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _entity.OnChannelChanged += HandleServerChannelChanged;
            _entity.OnChannelEnded += HandleServerChannelEnded;
            _entity.OnRevealed += HandleServerRevealed;
            _entity.OnTeleported += HandleServerTeleported;
            _entity.OnAttackSwing += HandleServerAttackSwing;
            _isAwakened = _entity.IsAwakened;
            _entity.SetSimulationActive(true);
            AILog.Log(AILog.NET, _entity.GhostId, "Ghost entity OnStartServer → Agent/BT enabled");
        }

        public override void OnStopServer()
        {
            _entity.OnChannelChanged -= HandleServerChannelChanged;
            _entity.OnChannelEnded -= HandleServerChannelEnded;
            _entity.OnRevealed -= HandleServerRevealed;
            _entity.OnTeleported -= HandleServerTeleported;
            _entity.OnAttackSwing -= HandleServerAttackSwing;
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
            _outline = gameObject.AddComponent<AI_RevealOutline>();
        }

        // Only players near the ghost see how far its action has come, unless a bell revealed it.
        // Revealed: every peer turns the glow outline on and off from the same synced end time (late joiners included)
        private void Update()
        {
            UpdateLocomotion();

            if (_gauge == null)
            {
                return;
            }

            bool revealed = IsRevealed;
            _outline.Show(revealed);

            float progress = ChannelProgress;
            if (progress >= 0f && !revealed && !IsViewerWithin(_gaugeShowDistance))
            {
                progress = -1f;
            }

            _gauge.Show(progress);
        }

        // The model is built after spawn and may be swapped, so the Animator is looked up again whenever it is gone
        private void UpdateLocomotion()
        {
            if (isServer && _movement != null && Mathf.Abs(_movement.CurrentSpeed - _moveSpeed) > SPEED_SYNC_STEP)
            {
                _moveSpeed = _movement.CurrentSpeed;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetFloat(MOVE_SPEED_PARAM, isServer && _movement != null ? _movement.CurrentSpeed : _moveSpeed);
            }
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
            _channelSlowRate = session.ChannelSlowRate;
            _channelSlowUntil = networkNow + Mathf.Max(0f, session.ChannelSlowUntil - now);
            _channelDuration = session.ChannelDuration;
        }

        private void HandleServerChannelEnded(AI_GhostEntity entity)
        {
            _channelDuration = 0f;
        }

        private void HandleServerAttackSwing(AI_GhostEntity entity)
        {
            RpcAttackSwing();
        }

        // A one-off swing, so an RPC; a model without the Attack trigger (no animator set up yet) just skips it
        [ClientRpc]
        private void RpcAttackSwing()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null && _animator.isActiveAndEnabled && HasParameter(_animator, ATTACK_TRIGGER))
            {
                _animator.SetTrigger(ATTACK_TRIGGER);
            }

            ClientAttackSwung?.Invoke();
        }

        private static bool HasParameter(Animator animator, int hash)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleServerRevealed(AI_GhostEntity entity, float seconds)
        {
            _revealedUntil = Math.Max(_revealedUntil, NetworkTime.time + seconds);
        }

        // The position is state (NetworkTransform), so it is teleported, not interpolated across the map; the puff of smoke
        // at both ends happens once, so it is an RPC
        private void HandleServerTeleported(AI_GhostEntity entity, Vector3 from, Vector3 to)
        {
            if (_networkTransform != null)
            {
                _networkTransform.ServerTeleport(to, transform.rotation);
            }
            else
            {
                AILog.Warn(AILog.NET, $"{_entity.GhostId} has no NetworkTransform, clients will see the ghost slide to {to}");
            }

            RpcTeleported(from, to);
        }

        [ClientRpc]
        private void RpcTeleported(Vector3 from, Vector3 to)
        {
            if (_teleportEffectPrefab != null)
            {
                Destroy(Instantiate(_teleportEffectPrefab, from, Quaternion.identity), _teleportEffectLifetime);
                Destroy(Instantiate(_teleportEffectPrefab, to, Quaternion.identity), _teleportEffectLifetime);
            }

            ClientTeleported?.Invoke(from, to);
        }
    }
}
