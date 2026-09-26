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

    // 서버 기준 사이클 시작 시각을 받았는지, 받기 전에는 시간 계산 x
    public bool IsRunning { get; private set; }
    // 시작 후 첫 계산을 했는지. 첫 계산에서는 현재 페이즈 이벤트를 무조건 한 번 발생
    private bool _hasEvaluated;

    public event Action OnDayStart;
    public event Action OnNightStart;
    public event Action<int> OnNewDay;

    public void SetCycleStart(double startTime)
    {
        CycleStartTime = startTime;
        IsRunning = true;
    }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }


        double elapsed = NetworkTime.time - CycleStartTime;
        if (elapsed < 0)
        {
            return;
        }

        double intoCycle = elapsed % CYCLE_DURATION;
        var newPhase = intoCycle < DAY_DURATION ? TimeOfDay.Day : TimeOfDay.Night;
        int newDay = (int)(elapsed / CYCLE_DURATION) + 1;

        // 날짜를 먼저 갱신 (OnDayStart 구독자가 DayCount를 읽을 때 새 날짜가 보이도록)
        if (newDay != DayCount)
        {
            DayCount = newDay;
            OnNewDay?.Invoke(DayCount);
        }

        if (!_hasEvaluated || newPhase != CurrentTimePhase)
        {
            _hasEvaluated = true;

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

        
    }
}
