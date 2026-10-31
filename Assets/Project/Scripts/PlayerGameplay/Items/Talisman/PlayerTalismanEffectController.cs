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

public struct TalismanEffectDisplay
{
    public string Name;
    public float RemainingDuration;
}

// Small timed modifier layer. Base movement/stress settings are never overwritten.
[DisallowMultipleComponent]
public class PlayerTalismanEffectController : MonoBehaviour
{
    private struct ActiveEffect { public TalismanEffectType Type; public string Name; public float Magnitude, Expires; }
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
        if (!CanExecute || definition == null || !definition.IsValid || definition.EffectType == TalismanEffectType.VillagerTracking) return false;
        if (definition.EffectType == TalismanEffectType.StressRelief)
            return TryGetComponent<PlayerStressController>(out var stress) && stress.CurrentStress > 0f;
        if (definition.EffectType == TalismanEffectType.HealthRestore)
            return _health != null && !_health.IsDead && _health.CurrentHealth < _health.MaxHealth;
        return definition.StackPolicy != TalismanStackPolicy.RejectWhileActive || !_active.ContainsKey(definition.EffectId);
    }
    public bool TryApply(TalismanEffectDefinition definition)
    {
        if (!CanApply(definition)) return false;
        if (definition.EffectType == TalismanEffectType.StressRelief)
        {
            var stress = GetComponent<PlayerStressController>();
            float before = stress.CurrentStress;
            try { stress.ReduceStress(definition.Magnitude); }
            catch (Exception e) { Debug.LogException(e, this); } // A failing UI listener must not grant a free effect.
            return stress.CurrentStress < before;
        }
        if (definition.EffectType == TalismanEffectType.HealthRestore)
        {
            float before = _health.CurrentHealth;
            try { _health.RestoreHealth(definition.Magnitude); }
            catch (Exception e) { Debug.LogException(e, this); }
            return _health.CurrentHealth > before;
        }
        _active[definition.EffectId] = new ActiveEffect { Type = definition.EffectType, Name = definition.DisplayName, Magnitude = definition.Magnitude, Expires = Time.time + definition.Duration };
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
    // Local presentation, including replicated timed effects; wire payload stays unchanged.
    public void CopyActiveDisplay(List<TalismanEffectDisplay> output)
    {
        output.Clear();
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject)) return;
        foreach (var effect in _active.Values)
        {
            float remaining = effect.Expires - Time.time;
            if (remaining > 0f) output.Add(new TalismanEffectDisplay { Name = effect.Name, RemainingDuration = remaining });
        }
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
            if (definition == null || !definition.IsValid || (definition.IsInstant || definition.EffectType == TalismanEffectType.VillagerTracking) || definition.EffectId != state.EffectId || next.ContainsKey(state.EffectId) ||
                float.IsNaN(state.RemainingDuration) || float.IsInfinity(state.RemainingDuration) || state.RemainingDuration <= 0f || state.RemainingDuration > definition.Duration) return false;
            next.Add(state.EffectId, new ActiveEffect { Type = definition.EffectType, Name = definition.DisplayName, Magnitude = definition.Magnitude, Expires = Time.time + state.RemainingDuration });
        }
        _active.Clear(); foreach (var pair in next) _active.Add(pair.Key, pair.Value);
        _snapshotVersion = version; PublishChanged(); return true;
    }
    public void ResetEffects() { _active.Clear(); PublishChanged(); }
}
