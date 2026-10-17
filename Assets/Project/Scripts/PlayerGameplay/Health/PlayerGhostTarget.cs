using Mirror;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

// Exposes the real Player to the existing Ghost query; health and AI decisions stay with their owners.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerGhostTarget : MonoBehaviour, IGhostTarget
{
    private PlayerHealth _health;
    private bool _registered;
    public GhostTargetKind Kind => GhostTargetKind.Player;
    private bool CanRegister => isActiveAndEnabled && _health != null &&
        _health.isActiveAndEnabled && !_health.IsDead &&
        (NetworkServer.active || !NetworkClient.active);

    private void Awake() => _health = GetComponent<PlayerHealth>();
    private void OnEnable()
    {
        _health.Died += OnDied;
        RefreshRegistration();
    }
    // Network startup/shutdown may happen after OnEnable. No Network component changes required.
    private void LateUpdate() => RefreshRegistration();
    private void OnDisable()
    {
        if (_health != null) _health.Died -= OnDied;
        Unregister();
    }
    private void OnDied(GameObject source) => Unregister();
    private void RefreshRegistration()
    {
        if (!CanRegister) { Unregister(); return; }
        if (_registered) return;
        GhostTargetRegistry.Register(this);
        _registered = true;
    }
    private void Unregister()
    {
        if (!_registered) return;
        GhostTargetRegistry.Unregister(this);
        _registered = false;
    }
    public bool CanBeTargeted(in GhostActionContext context) =>
        CanRegister && context.Verb == GhostActionVerb.Attack;
    public int GetPriority(in GhostActionContext context) => 0;
}
