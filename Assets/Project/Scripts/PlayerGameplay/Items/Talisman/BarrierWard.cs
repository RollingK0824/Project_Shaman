using System;
using System.Collections.Generic;
using UnityEngine;

// Server/offline personal protection. Not an HP shield or a Ghost attack volume.
[DisallowMultipleComponent]
public sealed class BarrierWard : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _barrierRadius = 1.4f;
    [SerializeField, Min(.1f)] private float _maxLifetime = 60f;
    [SerializeField] private Vector3 _centerOffset = new Vector3(0f, 1.1f, 0f);
    private static readonly Dictionary<GameObject, BarrierWard> Active = new();
    private GameObject _owner;
    private CharacterController _capsule;
    private Action<BarrierWard> _despawn;
    private float _expiresAt;
    private bool _armed, _expired;
    public GameObject Owner => _owner;
    public float Radius => _barrierRadius;
    public float Duration => _maxLifetime;
    public float RemainingDuration => IsActive ? Mathf.Max(0f, _expiresAt - Time.time) : 0f;
    public bool IsActive => _armed && !_expired && isActiveAndEnabled && Time.time < _expiresAt &&
        ItemAttackAuthority.CanResolve(_owner);
    public event Action<BarrierWard> Changed;
    public event Action<BarrierWard> Expired;
    public bool IsConfigurationValid => float.IsFinite(_barrierRadius) && _barrierRadius > 0f &&
        float.IsFinite(_maxLifetime) && _maxLifetime > 0f &&
        float.IsFinite(_centerOffset.x) && float.IsFinite(_centerOffset.y) && float.IsFinite(_centerOffset.z);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();

    public static bool IsProtecting(GameObject owner) => owner != null &&
        Active.TryGetValue(owner, out var ward) && ward != null && ward.IsActive;

    public static float GetRemainingDuration(GameObject owner) => owner != null &&
        Active.TryGetValue(owner, out var ward) && ward != null ? ward.RemainingDuration : 0f;

    internal void Prepare(GameObject owner, Action<BarrierWard> despawn)
    {
        _owner = owner;
        _capsule = owner.GetComponent<CharacterController>();
        _despawn = despawn;
        FollowOwner();
    }
    internal void Arm()
    {
        _expiresAt = Time.time + _maxLifetime;
        _armed = true;
        Active[_owner] = this;
        Notify(Changed);
    }
    private void LateUpdate()
    {
        if (!_armed || _expired) return;
        if (!IsActive) { Expire(); return; }
        FollowOwner();
    }
    private void FollowOwner()
    {
        if (_owner == null) return;
        // World-space radius is independent of model/root scale. Follow body, not camera.
        Vector3 center = _capsule != null ? _capsule.transform.TransformPoint(_capsule.center) :
            _owner.transform.position + _centerOffset;
        transform.SetPositionAndRotation(center, Quaternion.identity);
    }
    public void Expire()
    {
        if (_expired) return;
        _expired = true;
        _armed = false;
        if (!ReferenceEquals(_owner, null) && Active.TryGetValue(_owner, out var current) && current == this)
            Active.Remove(_owner);
        Notify(Expired);
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
