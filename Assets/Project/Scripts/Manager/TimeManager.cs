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
    public const float DAY_DURATION = 40f;
    public const float NIGHT_DURATION = 20f;
    public const float CYCLE_DURATION = DAY_DURATION + NIGHT_DURATION;

    public double CycleStartTime {  get; private set; }
    public int DayCount{ get; private set; }
    public TimeOfDay CurrentTimePhase { get; private set; }

    public event Action OnDayStart;
    public event Action OnNightStart;
    public event Action<int> OnNewDay;

    public void SetCycleStart(double startTime) => CycleStartTime = startTime;

    private void Update()
    {
        double elapsed = NetworkTime.time - CycleStartTime;
        if (elapsed < 0)
        {
            return;
        }

        double intoCycle = elapsed % CYCLE_DURATION;
        var newPhase = intoCycle < DAY_DURATION ? TimeOfDay.Day : TimeOfDay.Night;
        int newDay = (int)(elapsed / CYCLE_DURATION) + 1;

        if (newPhase != CurrentTimePhase)
        {
            CurrentTimePhase = newPhase;
            if (newPhase == TimeOfDay.Day)
            {
                OnDayStart?.Invoke();
            }
            else
            {
                OnNightStart?.Invoke();
            }
        }

        if (newDay != DayCount)
        {
            DayCount = newDay;
            OnNewDay?.Invoke(DayCount);
        }
    }
}
