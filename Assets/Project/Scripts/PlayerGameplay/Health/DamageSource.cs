using UnityEngine;

public class DamageSource : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float _damageAmount = 25f;

    public float DamageAmount =>
        _damageAmount;

    public void ApplyDamage(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        IDamageable damageable =
            target.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogWarning(
                $"[Damage] {target.name}에 " +
                "IDamageable이 없습니다.",
                target
            );

            return;
        }

        if (damageable.IsDead)
        {
            return;
        }

        damageable.ReceiveDamage(
            _damageAmount,
            gameObject
        );
    }

    private void OnValidate()
    {
        if (_damageAmount < 0f)
        {
            _damageAmount = 0f;
        }
    }
}