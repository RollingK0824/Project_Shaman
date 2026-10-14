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

        RequestDamage(damageable, _damageAmount, gameObject);
    }

    // True means a valid request was dispatched, not that a remote target acknowledged damage.
    public static bool RequestDamage(IDamageable target, float amount, GameObject source)
    {
        if (target == null || target.IsDead || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return false;
        PlayerEvents.RaiseDamageRequested(target, amount, source);
        return true;
    }

    private void OnValidate()
    {
        if (_damageAmount < 0f)
        {
            _damageAmount = 0f;
        }
    }
}
