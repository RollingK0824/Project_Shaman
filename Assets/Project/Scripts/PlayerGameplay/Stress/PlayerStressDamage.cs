using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerStressController), typeof(PlayerHealth))]
public class PlayerStressDamage : MonoBehaviour
{
    [Header("Prototype tuning")]
    [SerializeField, Range(0f, 100f)] private float _damageThreshold = 85f;
    [SerializeField, Min(0.1f)] private float _holdDuration = 10f;
    [SerializeField, Min(0f)] private float _damagePerTick = 2f;
    [SerializeField, Min(0.1f)] private float _damageInterval = 2f;
    private PlayerStressController _stress;
    private PlayerHealth _health;
    private GameObject _source;
    private float _held;
    private float _tick;
    private bool _started;
    private Func<bool> _serverAuthority;
    public float HeldDuration => _held;
    public bool DamageStarted => _started;
    public void SetServerAuthorityCheck(Func<bool> authority) { _serverAuthority = authority; ResetExposure(); }

    private void Awake()
    {
        _stress = GetComponent<PlayerStressController>();
        _health = GetComponent<PlayerHealth>();
        _source = new GameObject("StressDamage");
        _source.transform.SetParent(transform, false);
        ValidateSettings();
    }
    private void OnEnable() { _stress.StressChanged += OnStressChanged; }
    private void OnDisable() { _stress.StressChanged -= OnStressChanged; ResetExposure(); }
    private void OnDestroy() { if (_source != null) Destroy(_source); }
    private void OnStressChanged(float value) { if (value < _damageThreshold) ResetExposure(); }
    private void Update() => Advance(Time.deltaTime);

    // The network adapter must explicitly authorize server/round state. Never tick on a client.
    public void Advance(float deltaTime)
    {
        bool authority = (!NetworkClient.active && !NetworkServer.active) ||
            (NetworkServer.active && _serverAuthority != null && _serverAuthority());
        if (!isActiveAndEnabled || !authority || _health.IsDead || _stress.CurrentStress < _damageThreshold)
        { ResetExposure(); return; }
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
        if (!_started)
        {
            _held += deltaTime;
            if (_held < _holdDuration) return;
            _held = _holdDuration;
            _started = true;
            _tick = 0f;
            DealDamage();
            return;
        }
        _tick += deltaTime;
        if (_tick < _damageInterval) return;
        // Avoid a burst of catch-up damage after a hitch or suspended editor.
        _tick = 0f;
        DealDamage();
    }
    private void DealDamage()
    {
        if (_damagePerTick <= 0f) return;
        Debug.Log($"[StressDamage] 지속 스트레스 {_stress.CurrentStress:0.#} / 피해 {_damagePerTick:0.#}", this);
        _health.ReceiveDamage(_damagePerTick, _source);
    }
    public void ResetExposure() { _held = 0f; _tick = 0f; _started = false; }
    private static float Valid(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    private void ValidateSettings()
    {
        _damageThreshold = Mathf.Clamp(Valid(_damageThreshold, 85f), 0f, 100f);
        _holdDuration = Mathf.Max(.1f, Valid(_holdDuration, 10f));
        _damagePerTick = Mathf.Max(0f, Valid(_damagePerTick, 2f));
        _damageInterval = Mathf.Max(.1f, Valid(_damageInterval, 2f));
    }
    private void OnValidate() => ValidateSettings();
}
