using System;
using System.Collections.Generic;
using ProjectShaman.AI.Ghost;
using UnityEngine;

// Server/offline simulation only. The network adapter owns persistent replication/despawn.
// Local +Z points away from the surface; +Y is the upright paper direction.
[DisallowMultipleComponent, RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class BarrierWard : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _barrierRadius = 2.5f;
    [SerializeField, Min(1)] private int _maxCharges = 3;
    [SerializeField, Min(.01f)] private float _staggerDuration = 1.25f;
    [SerializeField, Min(.01f)] private float _retriggerCooldown = 1.5f;
    [SerializeField, Min(0f)] private float _maxLifetime = 60f; // 0: charges only
    private sealed class Entry
    {
        public readonly HashSet<Collider> Colliders = new();
        public float NextAllowed;
    }
    private readonly Dictionary<AI_GhostEntity, Entry> _entries = new();
    private readonly List<AI_GhostEntity> _stale = new();
    private SphereCollider _trigger;
    private GameObject _owner;
    private Collider _surface;
    private Vector3 _localPosition, _localNormal, _localUp;
    private Action<BarrierWard> _despawn;
    private float _expiresAt;
    private bool _armed, _expired, _resolving;
    public int RemainingCharges { get; private set; }
    public int MaxCharges => _maxCharges;
    public bool IsActive => _armed && !_expired;
    public event Action<BarrierWard> Changed;
    public event Action<BarrierWard> Expired;
    public bool IsConfigurationValid => _maxCharges > 0 && float.IsFinite(_barrierRadius) && _barrierRadius > 0f &&
        float.IsFinite(_staggerDuration) && _staggerDuration > 0f && float.IsFinite(_retriggerCooldown) &&
        _retriggerCooldown > 0f && float.IsFinite(_maxLifetime) && _maxLifetime >= 0f;

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.enabled = false; _trigger.isTrigger = true; _trigger.center = Vector3.zero;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = false;
    }
    internal void Prepare(GameObject owner, Collider surface, Action<BarrierWard> despawn)
    {
        _owner = owner; _surface = surface; _despawn = despawn;
        var t = surface.transform;
        _localPosition = t.InverseTransformPoint(transform.position);
        _localNormal = t.localToWorldMatrix.transpose.MultiplyVector(transform.forward);
        _localUp = t.InverseTransformDirection(transform.up);
        RemainingCharges = _maxCharges;
        _trigger.radius = _barrierRadius;
    }
    internal void Arm()
    {
        _expiresAt = _maxLifetime > 0f ? Time.time + _maxLifetime : float.PositiveInfinity;
        _armed = true; _trigger.enabled = true;
        Notify(Changed);
    }
    private void Update()
    {
        if (!IsActive) return;
        if (_surface == null || !_surface.enabled || !_surface.gameObject.activeInHierarchy ||
            !ItemAttackAuthority.CanResolve(_owner) || Time.time >= _expiresAt) { Expire(); return; }
        // Follow doors/moving props without inheriting their scale into the trigger radius.
        var t = _surface.transform;
        Vector3 normal = t.worldToLocalMatrix.transpose.MultiplyVector(_localNormal).normalized;
        Vector3 up = Vector3.ProjectOnPlane(t.TransformDirection(_localUp), normal).normalized;
        transform.SetPositionAndRotation(t.TransformPoint(_localPosition), Quaternion.LookRotation(normal, up));
        _stale.Clear();
        foreach (var pair in _entries)
        {
            pair.Value.Colliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (pair.Key == null || (pair.Value.Colliders.Count == 0 && Time.time >= pair.Value.NextAllowed)) _stale.Add(pair.Key);
        }
        foreach (var key in _stale) _entries.Remove(key);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!IsActive || _resolving || !ItemAttackAuthority.CanResolve(_owner)) return;
        var ghost = GhostOf(other);
        if (ghost == null) return;
        if (!_entries.TryGetValue(ghost, out var entry)) _entries.Add(ghost, entry = new Entry());
        bool entering = entry.Colliders.Count == 0;
        entry.Colliders.Add(other);
        if (!entering || Time.time < entry.NextAllowed) return;
        // Reserve before invoking external code, including rejected/immune contacts.
        entry.NextAllowed = Time.time + _retriggerCooldown;
        _resolving = true;
        try
        {
            var effect = new ItemControlEffect { Kind = ItemControlKind.Stagger, Duration = _staggerDuration };
            var attack = new ItemAttackSession(_owner, 0f, effect, ItemReactionFallback.None, ~0, 1f,
                collider => GhostOf(collider) == ghost);
            if (!attack.TryHit(other)) return;
            RemainingCharges = Mathf.Max(0, RemainingCharges - 1);
            Notify(Changed);
            if (RemainingCharges == 0) Expire();
        }
        finally { _resolving = false; }
    }
    private void OnTriggerExit(Collider other)
    {
        // Components may be disabled before Exit. Remove the collider from its recorded entry.
        foreach (var entry in _entries.Values) entry.Colliders.Remove(other);
    }
    private static AI_GhostEntity GhostOf(Collider other)
    {
        if (other == null || other.GetComponentInParent<PlayerHealth>() != null) return null;
        var ghost = other.GetComponentInParent<AI_GhostEntity>();
        return ghost != null && ghost.isActiveAndEnabled ? ghost : null;
    }
    public void Expire()
    {
        if (_expired) return;
        _expired = true; _armed = false;
        if (_trigger != null) _trigger.enabled = false;
        _entries.Clear();
        Notify(Expired); // Optional VFX/replication hook; cannot keep the collider alive.
        gameObject.SetActive(false);
        try { if (_despawn != null) _despawn(this); else Destroy(gameObject); }
        catch (Exception e) { Debug.LogException(e, this); Destroy(gameObject); }
    }
    private void Notify(Action<BarrierWard> listeners)
    {
        if (listeners == null) return;
        foreach (Action<BarrierWard> listener in listeners.GetInvocationList())
            try { listener(this); } catch (Exception e) { Debug.LogException(e, this); }
    }
    private void OnDisable() { if (_armed && !_expired) Expire(); }
    private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, _barrierRadius); }
}
