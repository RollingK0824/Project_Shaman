using System;
using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Status))]
    public class Net_AIStatus : NetworkBehaviour
    {
        [SerializeField] private bool _acceptRequestedDamage;

        [SyncVar(hook = nameof(OnHealthSynced))] private float _health;
        [SyncVar] private bool _isDead;

        private AI_Status _status;
        private AI_DamageReceiver _receiver;
        private bool _isRelaying;

        public float Health => _health;
        public float MaxHealth => _status.MaxHealth;
        public bool IsDead => _isDead;

        public event Action<float, float> ClientHealthChanged;
        public event Action<float> ClientDamaged;
        public event Action ClientDied;

        private void Awake()
        {
            _status = GetComponent<AI_Status>();
            _receiver = GetComponent<AI_DamageReceiver>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _status.SetSimulationActive(true);
            _health = _status.Health;
            _isDead = _status.IsDead;
            _status.OnDamaged += HandleServerDamaged;
            _status.OnDied += HandleServerDied;
            AILog.Log(AILog.NET, name, "Status OnStartServer → judgement enabled");

            if (_acceptRequestedDamage && _receiver != null)
            {
                PlayerEvents.DamageRequested += HandleDamageRequested;
                _isRelaying = true;
            }
        }

        public override void OnStopServer()
        {
            if (_isRelaying)
            {
                PlayerEvents.DamageRequested -= HandleDamageRequested;
                _isRelaying = false;
            }

            _status.OnDamaged -= HandleServerDamaged;
            _status.OnDied -= HandleServerDied;
            _status.SetSimulationActive(false);
            base.OnStopServer();
        }

        private void HandleDamageRequested(IDamageable target, float amount, GameObject source)
        {
            if (!ReferenceEquals(target, _receiver))
            {
                return;
            }

            if (_receiver.IsDead || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentGameState != GameState.Ongoing)
            {
                return;
            }

            _receiver.ReceiveDamage(amount, source);
        }

        private void HandleServerDamaged(AI_Status status, float amount, GameObject source)
        {
            _health = status.Health;
            RpcDamaged(amount);
        }

        private void HandleServerDied(AI_Status status, GameObject source)
        {
            _health = 0f;
            _isDead = true;
            RpcDied();
        }

        [ClientRpc]
        private void RpcDamaged(float amount)
        {
            ClientDamaged?.Invoke(amount);
        }

        private void OnHealthSynced(float oldValue, float newValue)
        {
            ClientHealthChanged?.Invoke(newValue, MaxHealth);
        }

        [ClientRpc]
        private void RpcDied()
        {
            ClientDied?.Invoke();
        }
    }
}
