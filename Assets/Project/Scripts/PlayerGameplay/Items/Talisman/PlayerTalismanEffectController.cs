using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[Serializable]
public struct TalismanEffectState
{
    public string EffectId;
    public float RemainingDuration;
}

// Small timed modifier layer. Base movement/stress settings are never overwritten.
[DisallowMultipleComponent]
public class PlayerTalismanEffectController : MonoBehaviour
{
    private struct ActiveEffect { public TalismanEffectType Type; public float Magnitude, Expires; }
    private readonly Dictionary<string, ActiveEffect> _active = new();
    private readonly List<string> _expired = new();
    private PlayerHealth _health;
    private Func<bool> _authorityCheck;
    private long _snapshotVersion = -1;
    public event Action EffectsChanged;
    public int ActiveCount { get { Expire(); return _active.Count; } }
    public void SetAuthorityCheck(Func<bool> check) => _authorityCheck = check;
    public bool CanExecute => isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) &&
        (!NetworkClient.active && !NetworkServer.active ? _authorityCheck == null || _authorityCheck() :
        NetworkServer.active && _authorityCheck != null && _authorityCheck());
    private void Awake() => _health = GetComponent<PlayerHealth>();
    private void OnEnable() { if (_health != null) _health.Died += OnDied; }
    private void OnDisable() { if (_health != null) _health.Died -= OnDied; ResetEffects(); }
    private void OnDied(GameObject source) => ResetEffects();
    private void Update() => Expire();
    public float MovementMultiplier => Multiplier(TalismanEffectType.MovementSpeed);
    public float StressGainMultiplier => Multiplier(TalismanEffectType.StressGain);
    public float StressRecoveryMultiplier => Multiplier(TalismanEffectType.StressRecovery);
    private float Multiplier(TalismanEffectType type)
    {
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject)) return 1f;
        Expire();
        // Same type uses strongest effect, never an unbounded product of effect IDs.
        float value = 1f;
        foreach (var effect in _active.Values)
            if (effect.Type == type) value = type == TalismanEffectType.StressGain ? Mathf.Min(value, effect.Magnitude) : Mathf.Max(value, effect.Magnitude);
        return value;
    }
    private void Expire()
    {
        _expired.Clear();
        foreach (var pair in _active) if (Time.time >= pair.Value.Expires) _expired.Add(pair.Key);
        foreach (var id in _expired) _active.Remove(id);
        if (_expired.Count > 0) PublishChanged();
    }
    public bool CanApply(TalismanEffectDefinition definition)
    {
        Expire();
        return CanExecute && definition != null && definition.IsValid &&
            (definition.StackPolicy != TalismanStackPolicy.RejectWhileActive || !_active.ContainsKey(definition.EffectId));
    }
    public bool TryApply(TalismanEffectDefinition definition)
    {
        if (!CanApply(definition)) return false;
        _active[definition.EffectId] = new ActiveEffect { Type = definition.EffectType, Magnitude = definition.Magnitude, Expires = Time.time + definition.Duration };
        return true;
    }
    // Collection publishes after effect + consumption commit, preventing reentrant double use.
    internal void PublishChanged()
    {
        try { EffectsChanged?.Invoke(); } catch (Exception e) { Debug.LogException(e, this); }
    }
    public TalismanEffectState[] ExportState()
    {
        Expire(); var result = new List<TalismanEffectState>();
        foreach (var pair in _active) result.Add(new TalismanEffectState { EffectId = pair.Key, RemainingDuration = pair.Value.Expires - Time.time });
        return result.ToArray();
    }
    // Adapter resolves IDs using the trusted local catalogue, never accepts client-authored magnitudes.
    public bool ApplyReplicatedState(long version, IReadOnlyList<TalismanEffectState> states, Func<string, TalismanEffectDefinition> resolve)
    {
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject) || !NetworkClient.active || NetworkServer.active ||
            version <= _snapshotVersion || states == null || states.Count > 32 || resolve == null) return false;
        var next = new Dictionary<string, ActiveEffect>();
        foreach (var state in states)
        {
            var definition = resolve(state.EffectId);
            if (definition == null || !definition.IsValid || definition.EffectId != state.EffectId || next.ContainsKey(state.EffectId) ||
                float.IsNaN(state.RemainingDuration) || float.IsInfinity(state.RemainingDuration) || state.RemainingDuration <= 0f || state.RemainingDuration > definition.Duration) return false;
            next.Add(state.EffectId, new ActiveEffect { Type = definition.EffectType, Magnitude = definition.Magnitude, Expires = Time.time + state.RemainingDuration });
        }
        _active.Clear(); foreach (var pair in next) _active.Add(pair.Key, pair.Value);
        _snapshotVersion = version; PublishChanged(); return true;
    }
    public void ResetEffects() { _active.Clear(); PublishChanged(); }
}
