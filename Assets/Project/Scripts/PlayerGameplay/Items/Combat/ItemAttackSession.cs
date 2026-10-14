using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemReactionFallback { None, Salt, BeanMugwort, Bell }

// One instance is one attack. Share it between an initial overlap and every trigger contact.
public sealed class ItemAttackSession
{
    private readonly HashSet<GameObject> _attempted = new();
    private readonly GameObject _owner;
    private readonly float _damage;
    private readonly ItemControlEffect _control;
    private readonly ItemReactionFallback _fallback;
    private readonly int _targetMask;
    private readonly float _expiresAt;
    private bool _ended;
    public int AppliedTargetCount { get; private set; }
    public event Action<GameObject> HitApplied;

    public ItemAttackSession(GameObject owner, float damage, ItemControlEffect control,
        ItemReactionFallback fallback, int targetMask, float lifetime)
    {
        _owner = owner;
        _damage = float.IsNaN(damage) || float.IsInfinity(damage) ? 0f : Mathf.Max(0f, damage);
        _control = control; _fallback = fallback; _targetMask = targetMask;
        float duration = float.IsNaN(lifetime) || float.IsInfinity(lifetime) ? 0f : Mathf.Max(0f, lifetime);
        _expiresAt = Time.time + duration;
    }
    public void End() => _ended = true;

    public bool TryHit(Collider other)
    {
        if (_ended || Time.time >= _expiresAt || !ItemAttackAuthority.CanResolve(_owner) ||
            other == null || !other.enabled || !other.gameObject.activeInHierarchy ||
            (_targetMask & (1 << other.gameObject.layer)) == 0 || other.transform.IsChildOf(_owner.transform)) return false;
        var health = other.GetComponentInParent<IDamageable>();
        var control = other.GetComponentInParent<IItemControlReceiver>();
        if (health != null && (!Active(health) || health.IsDead)) return false;
        Component legacy = null;
        switch (_fallback)
        {
            case ItemReactionFallback.Salt: legacy = other.GetComponentInParent<ISaltReactable>() as Component; break;
            case ItemReactionFallback.BeanMugwort: legacy = other.GetComponentInParent<IBeanMugwortReactable>() as Component; break;
            case ItemReactionFallback.Bell: legacy = other.GetComponentInParent<IBellReactable>() as Component; break;
        }
        // Receiver identity, not hitbox identity or scene root: child colliders share one entry.
        var receiver = health as Component ?? control as Component ?? legacy;
        if (receiver == null || receiver.transform.IsChildOf(_owner.transform)) return false;
        bool hasDamageContract = _damage > 0f && health != null;
        bool hasControlContract = _control.IsValid && control != null;
        if (!hasDamageContract && !hasControlContract && legacy == null) return false;
        var targetObject = receiver.gameObject;
        if (!_attempted.Add(targetObject)) return false; // Before callbacks, including immune hits.
        bool applied = false;
        if (hasDamageContract) applied = DamageSource.RequestDamage(health, _damage, _owner);
        if (hasControlContract && Active(control) && (health == null || (Active(health) && !health.IsDead)) && ItemAttackAuthority.CanResolve(_owner))
            applied |= control.TryApplyItemControl(_control, _owner);
        // Never call both a common contract and its legacy equivalent, even after rejection.
        if (!hasDamageContract && !hasControlContract && Active(legacy))
        {
            switch (_fallback)
            {
                case ItemReactionFallback.Salt: applied = ((ISaltReactable)legacy).ReactToSalt(_owner); break;
                case ItemReactionFallback.BeanMugwort:
                    if (_control.IsValid) applied = ((IBeanMugwortReactable)legacy).ReactToBeanMugwort(_owner, _control.SpeedMultiplier, _control.Duration);
                    break;
                case ItemReactionFallback.Bell: ((IBellReactable)legacy).ReactToBell(_owner); applied = true; break;
            }
        }
        // A lethal damage callback may destroy the receiver immediately.
        if (applied) { AppliedTargetCount++; HitApplied?.Invoke(targetObject); }
        return applied;
    }
    private static bool Active(object target) => target is Component component && component != null &&
        component.gameObject.activeInHierarchy && (!(component is Behaviour behaviour) || behaviour.isActiveAndEnabled);
}
