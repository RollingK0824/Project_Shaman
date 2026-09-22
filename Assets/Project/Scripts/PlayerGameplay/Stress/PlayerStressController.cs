using System;
using UnityEngine;

public class PlayerStressController : MonoBehaviour, IStressReceiver
{
    public enum StressLevel
    {
        Calm,
        Uneasy,
        Danger,
        Critical
    }

    [Header("Stress")]
    [SerializeField]
    [Range(0f, 100f)]
    private float _currentStress = 0f;

    [SerializeField] private float _maxStress = 100f;

    public float CurrentStress => _currentStress;
    public float MaxStress => _maxStress;

    public float NormalizedStress =>
        _maxStress <= 0f
            ? 0f
            : _currentStress / _maxStress;

    public StressLevel CurrentLevel { get; private set; }

    public event Action<float> StressChanged;
    public event Action<StressLevel> StressLevelChanged;

    public event Action<
        float,
        StressCause,
        GameObject
    > StressReceived;

    private void Awake()
    {
        _currentStress = Mathf.Clamp(
            _currentStress,
            0f,
            _maxStress
        );

        CurrentLevel =
            CalculateStressLevel(_currentStress);
    }

    public void ReceiveStress(
        float amount,
        StressCause cause,
        GameObject source = null)
    {
        if (amount <= 0f)
        {
            return;
        }

        AddStress(amount);

        StressReceived?.Invoke(
            amount,
            cause,
            source
        );

        string sourceName =
            source != null
                ? source.name
                : "None";

        Debug.Log(
            $"[Stress] 이벤트 수신 / " +
            $"Cause = {cause} / " +
            $"+{amount:0.##} / " +
            $"Source = {sourceName} / " +
            $"Current = {_currentStress:0.##}"
        );
    }

    public void AddStress(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetStress(
            _currentStress + amount
        );
    }

    public void ReduceStress(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetStress(
            _currentStress - amount
        );
    }

    public void SetStress(float value)
    {
        float newStress = Mathf.Clamp(
            value,
            0f,
            _maxStress
        );

        if (Mathf.Approximately(
                _currentStress,
                newStress))
        {
            return;
        }

        _currentStress = newStress;

        StressChanged?.Invoke(
            _currentStress
        );

        StressLevel newLevel =
            CalculateStressLevel(
                _currentStress
            );

        if (newLevel == CurrentLevel)
        {
            return;
        }

        CurrentLevel = newLevel;

        StressLevelChanged?.Invoke(
            CurrentLevel
        );

        Debug.Log(
            $"[Stress] 단계 변경: " +
            $"{CurrentLevel} " +
            $"({_currentStress:0.##}/" +
            $"{_maxStress:0.##})"
        );
    }

    private StressLevel CalculateStressLevel(
        float stress)
    {
        float normalized =
            _maxStress <= 0f
                ? 0f
                : stress / _maxStress;

        if (normalized >= 0.75f)
        {
            return StressLevel.Critical;
        }

        if (normalized >= 0.5f)
        {
            return StressLevel.Danger;
        }

        if (normalized >= 0.25f)
        {
            return StressLevel.Uneasy;
        }

        return StressLevel.Calm;
    }
}