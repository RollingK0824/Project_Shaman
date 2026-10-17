using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "MinionData", menuName = "ProjectShaman/AI/Minion Data")]
    public class MinionData : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float _moveSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float _chaseSpeed = 3.2f;
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.3f;

        [Header("Targeting")]
        [SerializeField] private MinionTargetMode _targetMode = MinionTargetMode.ObjectiveFirst;
        [SerializeField, Min(0f)] private float _aggroRadius = 5f;
        [SerializeField, Min(0f)] private float _loseAggroDistance = 8f;
        [SerializeField, Min(0f)] private float _leashDistance = 10f;
        [SerializeField, Min(0f)] private float _reaggroCooldown = 3f;
        [SerializeField, Min(0f)] private float _commitToObjectiveDistance = 4f;
        [SerializeField, Min(0.05f)] private float _aggroCheckInterval = 0.25f;

        [Header("Player Attack")]
        [SerializeField, Min(0.1f)] private float _attackRange = 1.5f;
        [SerializeField, Min(0f)] private float _attackWindup = 0.4f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1.5f;
        [SerializeField, Min(0f)] private float _attackDamage = 10f;

        [Header("Objective")]
        [SerializeField, Min(0.1f)] private float _objectiveContactDistance = 1.2f;
        [SerializeField, Min(0f)] private float _objectiveDamage = 1f;

        public float MoveSpeed => _moveSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float RepathInterval => _repathInterval;
        public MinionTargetMode TargetMode => _targetMode;
        public float AggroRadius => _aggroRadius;
        public float LoseAggroDistance => Mathf.Max(_aggroRadius, _loseAggroDistance);
        public float LeashDistance => _leashDistance;
        public float ReaggroCooldown => _reaggroCooldown;
        public float CommitToObjectiveDistance => _commitToObjectiveDistance;
        public float AggroCheckInterval => _aggroCheckInterval;
        public float AttackRange => _attackRange;
        public float AttackWindup => _attackWindup;
        public float AttackInterval => _attackInterval;
        public float AttackDamage => _attackDamage;
        public float ObjectiveContactDistance => _objectiveContactDistance;
        public float ObjectiveDamage => _objectiveDamage;
    }
}
