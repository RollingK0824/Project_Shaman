using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI
{
    public class AI_Status : MonoBehaviour
    {
        [SerializeField] private AIStatusData _data;
        [SerializeField, Min(1f)] private float _maxHealth = 1f;
        [SerializeField, Min(0f)] private float _deathDespawnDelay;

        [Header("Runtime (server only)")]
        [SerializeField] private float _health;
        [SerializeField] private bool _isDead;
        [SerializeField] private bool _isActive;

        public float MaxHealth => _data != null ? _data.MaxHealth : Mathf.Max(1f, _maxHealth);
        public float DeathDespawnDelay => _data != null ? _data.DeathDespawnDelay : Mathf.Max(0f, _deathDespawnDelay);
        public float Health => _health;
        public float NormalizedHealth => _health / MaxHealth;
        public bool IsDead => _isDead;
        public bool IsActive => _isActive;

        public event Action<AI_Status, float, GameObject> OnDamaged;
        public event Action<AI_Status, GameObject> OnDied;

        private void OnEnable()
        {
            ResetStatus();
        }

        public void SetSimulationActive(bool active)
        {
            _isActive = active;
        }

        public void ResetStatus()
        {
            _health = MaxHealth;
            _isDead = false;
        }

        public bool ApplyDamage(float amount, GameObject source)
        {
            if (!_isActive || _isDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return false;
            }

            _health = Mathf.Max(0f, _health - amount);
            string sourceName = source != null ? source.name : "unknown";
            AILog.Log(AILog.CORE, name, $"Damaged -{amount:F1} by {sourceName} → {_health:F1}/{MaxHealth:F1}");
            OnDamaged?.Invoke(this, amount, source);

            if (_health <= 0f)
            {
                _isDead = true;
                AILog.Log(AILog.CORE, name, $"Died by {sourceName}");
                OnDied?.Invoke(this, source);
            }

            return true;
        }
    }
}
