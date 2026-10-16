using System;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Mock
{
    public class MockDefenseFlag : MonoBehaviour, IDefenseTarget, IDamageable
    {
        [SerializeField, Min(1f)] private float _maxHP = 10f;

        [Header("Runtime")]
        [SerializeField] private float _currentHP;
        [SerializeField] private int _hitCount;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public bool IsDead => _currentHP <= 0f;

        public event Action Destroyed;

        private void Awake()
        {
            _currentHP = _maxHP;
        }

        public void ResetHP()
        {
            _currentHP = _maxHP;
            _hitCount = 0;
            AILog.Log(AILog.MINION, name, $"MockDefenseFlag reset {_currentHP:F0}/{_maxHP:F0}");
        }

        public void ReceiveDamage(float amount, GameObject source = null)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            _hitCount++;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            AILog.Log(AILog.MINION, name, $"MockDefenseFlag hit {amount:F1} by {(source != null ? source.name : "unknown")} → {_currentHP:F0}/{_maxHP:F0}");

            if (IsDead)
            {
                AILog.Log(AILog.MINION, name, "MockDefenseFlag destroyed");
                Destroyed?.Invoke();
            }
        }
    }
}
