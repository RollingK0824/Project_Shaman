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

        private AI_Status _status;
        private AI_DamageReceiver _receiver;
        private bool _isRelaying;

        private void Awake()
        {
            _status = GetComponent<AI_Status>();
            _receiver = GetComponent<AI_DamageReceiver>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _status.SetSimulationActive(true);
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
    }
}
