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
    private int _startTextTimeIndex = 4;
    private int _textTimeIndex = -1;
    private bool _hasEvaluated;
    private List<string> _textTime = new List<string> { "자시", "축시", "인시", "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시" };

    public const float DAY_DURATION = 300f;
    public const float NIGHT_DURATION = 180f;
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

    // 테스트 빨리 감기 (F1 패널): RebaseTime부터 Speed배로 흐르고 ElapsedAtRebase에서 이어서 센다
    public float Speed { get; private set; } = 1f;
    private double _rebaseTime;
    private double _elapsedAtRebase;

    public void SetCycleStart(double startTime)
    {
        CycleStartTime = startTime;
        _rebaseTime = startTime;
        _elapsedAtRebase = 0;
        IsRunning = true;
    }

    public double ElapsedAt(double networkTime)
    {
        return _elapsedAtRebase + (networkTime - _rebaseTime) * Speed;
    }

    // 게임 시계와 Time.timeScale을 같이 바꿔 주민 이동·일과·애니메이션이 같은 비율로 빨라짐
    public void SetSpeed(float speed, double rebaseTime, double elapsedAtRebase)
    {
        Speed = Mathf.Max(1f, speed);
        _rebaseTime = rebaseTime;
        _elapsedAtRebase = elapsedAtRebase;
        Time.timeScale = Speed;
        Debug.Log($"[TimeManager] 시간 배속 x{Speed:0.#}");
        // Also in the AI session log: above x3 agents take steps past their arrival radius, so lateness and
        // approach timeouts from then on are not real
        string note = Speed > 3f ? " (above x3 movement and arrival timings are unreliable)" : string.Empty;
        ProjectShaman.AI.Core.AILog.Log(ProjectShaman.AI.Core.AILog.TIME_TO_AI, $"Game speed x{Speed:0.#}{note}");
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    public string GetTextTime() { return _textTime[_textTimeIndex < 0 ? _startTextTimeIndex : _textTimeIndex]; }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }


        Elapsed = ElapsedAt(NetworkTime.time);
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


        int step = Mathf.Min((int)(intoCycle / (CYCLE_DURATION / _textTime.Count)), _textTime.Count - 1);
        int newTextTimeIndex = (_startTextTimeIndex + step) % _textTime.Count;

        if (newTextTimeIndex != _textTimeIndex)
        {
            _textTimeIndex = newTextTimeIndex;

            Debug.Log("[TimeManager] Current Time: " + _textTime[_textTimeIndex]);

            OnTextTimeChange?.Invoke();
        }
    }
}
