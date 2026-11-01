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

        private const string MOVE_SPEED_PARAM = "Speed";
        private static readonly int ATTACK_TRIGGER = Animator.StringToHash("Attack");
        // Speed changes are smoothed over about this many seconds, so a jittery synced position does not flicker the walk
        private const float SPEED_SMOOTHING = 0.15f;

        private Animator _animator;
        private Vector3 _lastPosition;
        private float _speed;

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

            _lastPosition = transform.position;
            ClientAttackStarted += PlayAttack;
        }

        public override void OnStopClient()
        {
            ClientAttackStarted -= PlayAttack;
            base.OnStopClient();
        }

        private void PlayAttack()
        {
            if (FindAnimator() && HasParameter(_animator, ATTACK_TRIGGER))
            {
                _animator.SetTrigger(ATTACK_TRIGGER);
            }
        }

        // The model is put under Visual after spawn (AI_ModelVisual), so the Animator is looked up again while it is missing
        private bool FindAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            return _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;
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

        // Walk speed from how far the minion moved: the same on the server (NavMesh or flying) and on clients (synced transform)
        private void UpdateLocomotion()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            _speed = Mathf.Lerp(_speed, delta.magnitude / Time.deltaTime, 1f - Mathf.Exp(-Time.deltaTime / SPEED_SMOOTHING));

            if (FindAnimator())
            {
                _animator.SetFloat(MOVE_SPEED_PARAM, _speed);
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
            UpdateLocomotion();

            if (_fadeStartTime < 0f)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.time - _fadeStartTime) / _fadeSeconds);
            transform.localScale = _fadeStartScale * (1f - t);
        }
    }
}
