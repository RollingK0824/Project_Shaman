using UnityEngine;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "AIStatusData", menuName = "ProjectShaman/AI/Status")]
    public class AIStatusData : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _maxHealth = 1f;
        [SerializeField, Min(0f)] private float _deathDespawnDelay;

        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float DeathDespawnDelay => Mathf.Max(0f, _deathDespawnDelay);
    }
}
