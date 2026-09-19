using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }

    void ReceiveDamage(
        float amount,
        GameObject source = null
    );
}