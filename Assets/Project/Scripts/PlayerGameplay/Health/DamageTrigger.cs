using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(DamageSource))]
public class DamageTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private bool _oncePerEntry = true;

    private DamageSource _damageSource;

    private bool _hasTriggered;

    private void Awake()
    {
        _damageSource =
            GetComponent<DamageSource>();
    }

    private void Reset()
    {
        Collider triggerCollider =
            GetComponent<Collider>();

        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_oncePerEntry &&
            _hasTriggered)
        {
            return;
        }

        IDamageable damageable =
            other.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            return;
        }

        if (damageable.IsDead)
        {
            return;
        }

        _damageSource.ApplyDamage(
            other.gameObject
        );

        _hasTriggered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        IDamageable damageable =
            other.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            return;
        }

        if (_oncePerEntry)
        {
            _hasTriggered = false;
        }
    }
}