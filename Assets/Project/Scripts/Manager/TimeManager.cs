using System;
using Mirror;
using UnityEngine;

public enum TimeOfDay
{
    Day,
    Night
}

public class TimeManager : SceneSingleton<TimeManager>
{
    public const float DAY_DURATION = 480f;
    public const float NIGHT_DURATION = 120f;
    public const float CYCLE_DURATION = DAY_DURATION + NIGHT_DURATION;

    public double CycleStartTime {  get; private set; }
    public int DayCount{ get; private set; }
    public TimeOfDay TimePhase { get; private set; }

    public event Action OnDayStart;
    public event Action OnNightStart;
    public event Action<int> OnNewDay;

    public void SetCycleStart(double startTime) => CycleStartTime = startTime;

    private void Update()
    {
        double elapsed = NetworkTime
    }
}
