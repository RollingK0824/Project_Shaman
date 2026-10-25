using System;
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

        public event Action ClientAttackStarted;
        public event Action<bool> ClientAttackResolved;
        public event Action<ItemControlKind, float> ClientControlApplied;
        public event Action<float> ClientPierced;

        private float _fadeStartTime = -1f;
        private float _fadeSeconds;
        private Vector3 _fadeStartScale;

        private void Awake()
        {
            _minion = GetComponent<AI_Minion>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _minion.OnAttackStarted += HandleServerAttackStarted;
            _minion.OnAttackResolved += HandleServerAttackResolved;
            _minion.OnControlApplied += HandleServerControlApplied;
            _minion.OnPierced += HandleServerPierced;
            _minion.SetSimulationActive(true);
            AILog.Log(AILog.NET, name, "Minion OnStartServer → FSM enabled");
        }

        public override void OnStopServer()
        {
            _minion.OnAttackStarted -= HandleServerAttackStarted;
            _minion.OnAttackResolved -= HandleServerAttackResolved;
            _minion.OnControlApplied -= HandleServerControlApplied;
            _minion.OnPierced -= HandleServerPierced;
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

        private void HandleServerAttackStarted(AI_Minion minion)
        {
            RpcAttackStarted();
        }

        private void HandleServerAttackResolved(AI_Minion minion, bool hit)
        {
            RpcAttackResolved(hit);
        }

        private void HandleServerControlApplied(AI_Minion minion, ItemControlEffect effect)
        {
            RpcControlApplied(effect.Kind, effect.Duration);
        }

        private void HandleServerPierced(AI_Minion minion, float fadeSeconds)
        {
            RpcPierced(fadeSeconds);
        }

        [ClientRpc]
        private void RpcAttackStarted()
        {
            ClientAttackStarted?.Invoke();
        }

        [ClientRpc]
        private void RpcAttackResolved(bool hit)
        {
            ClientAttackResolved?.Invoke(hit);
        }

        [ClientRpc]
        private void RpcControlApplied(ItemControlKind kind, float duration)
        {
            ClientControlApplied?.Invoke(kind, duration);
        }

        // Placeholder fade until there is an effect: the minion shrinks away where it stopped
        [ClientRpc]
        private void RpcPierced(float fadeSeconds)
        {
            _fadeStartTime = Time.time;
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            _fadeStartScale = transform.localScale;
            ClientPierced?.Invoke(fadeSeconds);
        }

        private void Update()
        {
            if (_fadeStartTime < 0f)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.time - _fadeStartTime) / _fadeSeconds);
            transform.localScale = _fadeStartScale * (1f - t);
        }
    }
}
