using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider), typeof(DamageSource))]
public class DamageTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private bool _oncePerEntry = true;
    private DamageSource _damageSource;
    private readonly Dictionary<IDamageable, HashSet<Collider>> _contacts = new();
    private readonly Dictionary<Collider, IDamageable> _targets = new();
    private ItemAttackSession _attack;
    private bool _attackVolume;
    private void Awake() => _damageSource = GetComponent<DamageSource>();
    private void Reset() => GetComponent<Collider>().isTrigger = true;
    public void BeginAttack(ItemAttackSession attack)
    {
        _attack?.End(); _attackVolume = true; _attack = attack;
        _contacts.Clear(); _targets.Clear();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled) return;
        if (_attackVolume) { _attack?.TryHit(other); return; }
        var target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;
        if (!_contacts.TryGetValue(target, out var contacts))
        { contacts = new HashSet<Collider>(); _contacts.Add(target, contacts); }
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        bool firstContact = contacts.Count == 0;
        contacts.Add(other); _targets[other] = target;
        if (!_oncePerEntry || firstContact) _damageSource.ApplyDamage(other.gameObject);
    }
    private void OnTriggerExit(Collider other)
    {
        // Re-entering an attack volume is still the same attack, so its ledger never resets here.
        if (_attackVolume || !_targets.TryGetValue(other, out var target)) return;
        _targets.Remove(other);
        if (_contacts.TryGetValue(target, out var contacts))
        { contacts.Remove(other); if (contacts.Count == 0) _contacts.Remove(target); }
    }
    private void OnDisable()
    {
        _attack?.End(); _attack = null; _contacts.Clear(); _targets.Clear();
    }
    // World-space volume, independent of the first-person visual scale and equipped item lifetime.
    public static DamageTrigger CreateSphere(ItemAttackSession attack, Vector3 position, float radius, float duration)
    {
        if (attack == null || float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f ||
            float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f) return null;
        var go = new GameObject("ItemAttackVolume");
        go.layer = 2; // Ignore Raycast: attacks must not obscure interaction or aiming.
        go.transform.position = position;
        var collider = go.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = radius;
        var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        var trigger = go.AddComponent<DamageTrigger>(); trigger.BeginAttack(attack);
        Destroy(go, duration);
        // Initial overlap preserves instant Salt/Bell response. Trigger callbacks share its ledger.
        foreach (var hit in Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Collide)) attack.TryHit(hit);
        return trigger;
    }

    public static DamageTrigger CreateBox(ItemAttackSession attack, Vector3 position, Vector3 halfExtents, Quaternion rotation, float duration)
    {
        if (attack == null || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f ||
            !ValidExtent(halfExtents.x) || !ValidExtent(halfExtents.y) || !ValidExtent(halfExtents.z)) return null;
        var go = new GameObject("ItemAttackVolume"); go.layer = 2;
        go.transform.SetPositionAndRotation(position, rotation);
        var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = halfExtents * 2f;
        var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        var trigger = go.AddComponent<DamageTrigger>(); trigger.BeginAttack(attack);
        Destroy(go, duration);
        foreach (var hit in Physics.OverlapBox(position, halfExtents, rotation, ~0, QueryTriggerInteraction.Collide)) attack.TryHit(hit);
        return trigger;
    }

    private static bool ValidExtent(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
