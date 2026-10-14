using UnityEngine;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Status))]
    public class AI_DamageReceiver : MonoBehaviour, IDamageable
    {
        private AI_Status _status;

        public bool IsDead => _status.IsDead;

        private void Awake()
        {
            _status = GetComponent<AI_Status>();
        }

        public void ReceiveDamage(float amount, GameObject source = null)
        {
            _status.ApplyDamage(amount, source);
        }
    }
}
