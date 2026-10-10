using System;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

public enum TimeOfDay
{
    Day,
    Night
}

public class TimeManager : SceneSingleton<TimeManager>
{
    [SerializeField]
    private int _textTimeIndex = 4;
    private int _tempCurrent;
    private bool _hasEvaluated;
    private List<string> _textTime = new List<string> { "자시", "축시", "인시", "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시" };

    public const float DAY_DURATION = 40f;
    public const float NIGHT_DURATION = 20f;
    public const float CYCLE_DURATION = DAY_DURATION + NIGHT_DURATION;

    public double Elapsed {  get; private set; }
    public double CycleStartTime {  get; private set; }
    public int DayCount{ get; private set; }
    public TimeOfDay CurrentTimePhase { get; private set; }

    public bool IsRunning { get; private set; }

    public event Action OnTextTimeChange;
    public event Action OnDayStart;
    public event Action OnNightStart;
    public event Action<int> OnNewDay;

    public void SetCycleStart(double startTime)
    {
        CycleStartTime = startTime;
        IsRunning = true;
    }

    public string GetTextTime() { return _textTime[_textTimeIndex]; }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }


        Elapsed = NetworkTime.time - CycleStartTime;
        if (Elapsed < 0)
        {
            return;
        }

        double intoCycle = Elapsed % CYCLE_DURATION;
        var newPhase = intoCycle < DAY_DURATION ? TimeOfDay.Day : TimeOfDay.Night;
        int newDay = (int)(Elapsed / CYCLE_DURATION) + 1;

       
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


        if (((int)Elapsed % (int)(CYCLE_DURATION/12)) == 0)
        {
            if (_tempCurrent == (int)Elapsed)
            {
                return;
            }
            _tempCurrent = (int)Elapsed;

            _textTimeIndex++;
            if (_textTimeIndex >= _textTime.Count)
            {
                _textTimeIndex = 0;
            }

            Debug.Log("[TimeManager] Current Time: " + _textTime[_textTimeIndex]);

            OnTextTimeChange?.Invoke();
        }
    }
}
