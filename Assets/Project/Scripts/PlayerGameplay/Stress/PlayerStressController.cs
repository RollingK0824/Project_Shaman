using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStressController : MonoBehaviour, IStressReceiver
{
    public enum StressLevel { Low, Mid, High }
    public const float StressLimit = 100f;

    [Header("Stress")]
    [SerializeField, Range(0f, StressLimit)] private float _currentStress;
    // Preserve the existing serialized field while enforcing the 0-100 range.
    [SerializeField, HideInInspector] private float _maxStress = StressLimit;
    [Header("Stage Thresholds")]
    [SerializeField, Range(1f, 98f)] private float _midThreshold = 35f;
    [SerializeField, Range(2f, 99f)] private float _highThreshold = 70f;
    [SerializeField] private bool _logStressEvents;

    public float CurrentStress => _currentStress;
    public float MaxStress => StressLimit;
    public float NormalizedStress => _currentStress / StressLimit;
    public float MidThreshold => _midThreshold;
    public float HighThreshold => _highThreshold;
    public StressLevel CurrentLevel { get; private set; }
    public float FeedbackIntensity => CurrentLevel == StressLevel.Low ? 0f :
        Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(_midThreshold, StressLimit, _currentStress));
    public float HighIntensity => Mathf.InverseLerp(_highThreshold, StressLimit, _currentStress);

    public event Action<float> StressChanged;
    public event Action<StressLevel> StressLevelChanged;
    public event Action<float, StressCause, GameObject> StressReceived;
    private readonly Dictionary<UnityEngine.Object, float> _sourceRates = new();
    private readonly List<UnityEngine.Object> _expiredSources = new();

    private void Awake()
    {
        ValidateSettings();
        CurrentLevel = CalculateStressLevel(_currentStress);
    }

    private void Update()
    {
        float increase = 0f;
        float recovery = 0f;
        _expiredSources.Clear();
        foreach (var source in _sourceRates)
        {
            if (source.Key == null) { _expiredSources.Add(source.Key); continue; }
            if (source.Value < 0f) recovery += source.Value;
            else increase += source.Value;
        }
        foreach (var source in _expiredSources) _sourceRates.Remove(source);
        // Safe areas take priority over continuous exposure. One-shot scares
        // still use IStressReceiver and do not damage health.
        float rate = recovery < 0f ? recovery : increase;
        if (rate != 0f) SetStress(_currentStress + rate * Time.deltaTime);
    }

    public void SetSourceRate(UnityEngine.Object source, float stressPerSecond)
    {
        if (source == null || !IsFinite(stressPerSecond)) return;
        _sourceRates[source] = stressPerSecond;
    }

    public void RemoveSource(UnityEngine.Object source)
    {
        if (!ReferenceEquals(source, null)) _sourceRates.Remove(source);
    }

    public void ReceiveStress(float amount, StressCause cause, GameObject source = null)
    {
        if (!IsFinite(amount) || amount <= 0f) return;
        AddStress(amount);
        StressReceived?.Invoke(amount, cause, source);
        if (_logStressEvents)
            Debug.Log($"[Stress] {cause} +{amount:0.##} / Current = {_currentStress:0.##}", this);
    }

    public void AddStress(float amount)
    {
        if (IsFinite(amount) && amount > 0f) SetStress(_currentStress + amount);
    }

    public void ReduceStress(float amount)
    {
        if (IsFinite(amount) && amount > 0f) SetStress(_currentStress - amount);
    }

    public void SetStress(float value)
    {
        if (!IsFinite(value)) return;
        float next = Mathf.Clamp(value, 0f, StressLimit);
        if (Mathf.Approximately(_currentStress, next)) return;
        _currentStress = next;
        StressLevel previous = CurrentLevel;
        CurrentLevel = CalculateStressLevel(next);
        StressChanged?.Invoke(next);
        if (previous != CurrentLevel)
        {
            StressLevelChanged?.Invoke(CurrentLevel);
            if (_logStressEvents) Debug.Log($"[Stress] 단계: {CurrentLevel} ({next:0.##}/100)", this);
        }
    }

    public void ApplyStressLevel(StressLevel level)
    {
        if (level == CurrentLevel)
            return;


        CurrentLevel = level;

        StressLevelChanged?.Invoke(CurrentLevel);
    }
    private StressLevel CalculateStressLevel(
        float stress)
    private StressLevel CalculateStressLevel(float stress)
    {
        if (stress >= _highThreshold) return StressLevel.High;
        return stress >= _midThreshold ? StressLevel.Mid : StressLevel.Low;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void ValidateSettings()
    {
        _maxStress = StressLimit;
        _midThreshold = Mathf.Clamp(IsFinite(_midThreshold) ? _midThreshold : 35f, 1f, 98f);
        _highThreshold = Mathf.Clamp(IsFinite(_highThreshold) ? _highThreshold : 70f, _midThreshold + 1f, 99f);
        _currentStress = Mathf.Clamp(IsFinite(_currentStress) ? _currentStress : 0f, 0f, StressLimit);
    }

    private void OnValidate()
    {
        ValidateSettings();
        CurrentLevel = CalculateStressLevel(_currentStress);
    }
    private void OnDisable() => _sourceRates.Clear();
}
