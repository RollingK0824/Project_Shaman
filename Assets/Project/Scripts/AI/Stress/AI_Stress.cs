using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Stress
{
    [RequireComponent(typeof(AI_Core))]
    public class AI_Stress : MonoBehaviour, IAIConfigurable
    {
        private const int HISTORY_SIZE = 10;

        [Header("Runtime (server only)")]
        [SerializeField] private float _value;
        [SerializeField] private VillagerStressState _state = VillagerStressState.Calm;
        [SerializeField] private List<string> _historyDebug = new List<string>();

        private AI_Core _core;
        private AI_Status _status;
        private AI_GhostReactionReceiver _ghostTarget;
        private AIBehaviourConfig _config;
        private bool _isSubscribed;

        public float Value => _value;
        public float Ratio => _config != null ? _value / _config.VillagerStressMax : 0f;
        public VillagerStressState State => _state;

        public event Action<AI_Stress, float, string> OnStressChanged;
        public event Action<AI_Stress, VillagerStressState, VillagerStressState> OnStateChanged;

        private string VillagerId => _core.LogId;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _status = GetComponent<AI_Status>();
            _ghostTarget = GetComponent<AI_GhostReactionReceiver>();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Configure(AIRuntimeContext context)
        {
            _config = context.Config;

            if (!_isSubscribed)
            {
                AIEvents.OnVillagerStress += HandleStress;
                _isSubscribed = true;
            }
        }

        public void ApplyMorningDecay()
        {
            if (_config == null || _value <= 0f)
            {
                return;
            }

            Apply(-_config.VillagerStressMorningDecay, "morning decay");
        }

        private void HandleStress(string villagerId, string reason, float amount)
        {
            if (villagerId != VillagerId)
            {
                return;
            }

            Apply(amount, reason);
        }

        private void Apply(float amount, string reason)
        {
            if (_config == null || Mathf.Approximately(amount, 0f))
            {
                return;
            }

            if (_status != null && _status.IsDead)
            {
                AILog.Log(AILog.STRESS, VillagerId, $"Ignored {amount:+0.#;-0.#} ({reason}), villager is dead");
                return;
            }

            float previous = _value;
            _value = Mathf.Clamp(_value + amount, 0f, _config.VillagerStressMax);

            if (Mathf.Approximately(previous, _value))
            {
                return;
            }

            AILog.Log(AILog.STRESS, VillagerId, $"{previous:F1} → {_value:F1} ({amount:+0.#;-0.#}, {reason})");
            AddHistory($"{_value:F0} {amount:+0.#;-0.#} {reason}");
            OnStressChanged?.Invoke(this, _value - previous, reason);
            UpdateState();
        }

        private void UpdateState()
        {
            VillagerStressState next = EvaluateState();
            if (next == _state)
            {
                return;
            }

            VillagerStressState previous = _state;
            _state = next;
            AILog.Log(AILog.STRESS, VillagerId, $"State {previous} → {next} ({_value:F1})");

            // Eased below the limit (a gut done): the death mark goes with it
            if (previous == VillagerStressState.Breaking && _ghostTarget != null && _ghostTarget.IsMarked)
            {
                _ghostTarget.SetMarked(false);
            }
            OnStateChanged?.Invoke(this, previous, next);
        }

        // Stress limit = death mark, judged in the morning (C129): the ghost hunts this villager first tonight
        // unless a gut brings the stress down today
        public void JudgeMorningMark()
        {
            if (_state == VillagerStressState.Breaking && _ghostTarget != null && !_ghostTarget.IsMarked)
            {
                _ghostTarget.SetMarked(true);
            }
        }

        public bool IsMarked => _ghostTarget != null && _ghostTarget.IsMarked;

        private VillagerStressState EvaluateState()
        {
            if (_value >= _config.VillagerStressLimit)
            {
                return VillagerStressState.Breaking;
            }

            if (_value >= _config.VillagerStressAnxious)
            {
                return VillagerStressState.Anxious;
            }

            return VillagerStressState.Calm;
        }

        private void AddHistory(string line)
        {
            _historyDebug.Insert(0, line);

            while (_historyDebug.Count > HISTORY_SIZE)
            {
                _historyDebug.RemoveAt(_historyDebug.Count - 1);
            }
        }

        private void Unsubscribe()
        {
            if (_isSubscribed)
            {
                AIEvents.OnVillagerStress -= HandleStress;
                _isSubscribed = false;
            }
        }
    }
}
