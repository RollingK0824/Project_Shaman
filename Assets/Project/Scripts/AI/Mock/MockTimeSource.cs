using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Mock
{
    public class MockTimeSource : MonoBehaviour, ITimeSource
    {
        [SerializeField] private int _slotsPerDay = 4;
        [SerializeField] private float _slotDuration = 30f;
        [SerializeField] private float _nightDuration = 20f;
        [SerializeField] private float _timeScale = 1f;

        [Header("Runtime")]
        [SerializeField] private bool _isRunning;
        [SerializeField] private int _dayCount;
        [SerializeField] private bool _isNight;
        [SerializeField] private float _phaseElapsed;
        [SerializeField] private int _currentSlot = -1;

        public string SourceName => $"Mock({name})";
        public bool IsRunning => _isRunning;
        public int DayCount => _dayCount;
        public bool IsNight => _isNight;
        public float PhaseElapsed => _phaseElapsed;
        public float DayDuration => _slotsPerDay * _slotDuration;
        public float NightDuration => _nightDuration;
        public int SlotsPerDay => _slotsPerDay;
        public float SlotDuration => _slotDuration;

        public event System.Action<int> OnNewDay;
        public event System.Action OnNightStart;

        private void Awake()
        {
            _isRunning = false;
            _dayCount = 0;
            _isNight = false;
            _phaseElapsed = 0f;
            _currentSlot = -1;
        }

        public void StartClock()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _dayCount = 1;
            _isNight = false;
            _phaseElapsed = 0f;

            AILog.Log(AILog.TIME_TO_AI, $"{SourceName} started (slots={_slotsPerDay}x{_slotDuration}s, night={_nightDuration}s, scale={_timeScale})");
            OnNewDay?.Invoke(_dayCount);
        }

        public void StopClock()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _currentSlot = -1;
            AILog.Log(AILog.TIME_TO_AI, $"{SourceName} stopped at Day {_dayCount}");
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            Advance(Time.deltaTime * _timeScale);
        }

        private void Advance(float deltaSeconds)
        {
            _phaseElapsed += deltaSeconds;
            _currentSlot = _isNight ? -1 : Mathf.Min(Mathf.FloorToInt(_phaseElapsed / _slotDuration), _slotsPerDay - 1);

            if (!_isNight && _phaseElapsed >= DayDuration)
            {
                _isNight = true;
                _phaseElapsed = 0f;
                AILog.Log(AILog.TIME_TO_AI, $"Day {_dayCount} → Night");
                OnNightStart?.Invoke();
            }
            else if (_isNight && _phaseElapsed >= _nightDuration)
            {
                _isNight = false;
                _phaseElapsed = 0f;
                _dayCount++;
                AILog.Log(AILog.TIME_TO_AI, $"Night → Day {_dayCount}");
                OnNewDay?.Invoke(_dayCount);
            }
        }

        [ContextMenu("Debug/Skip To Next Slot")]
        private void SkipToNextSlot()
        {
            if (!CanSkip() || _isNight)
            {
                return;
            }

            float nextBoundary = (Mathf.FloorToInt(_phaseElapsed / _slotDuration) + 1) * _slotDuration;
            AILog.Log(AILog.TIME_TO_AI, $"[Debug] Skip {_phaseElapsed:F1}s → {nextBoundary:F1}s");
            Advance(nextBoundary - _phaseElapsed + 0.01f);
        }

        [ContextMenu("Debug/Skip 10 Seconds")]
        private void SkipTenSeconds()
        {
            if (!CanSkip())
            {
                return;
            }

            AILog.Log(AILog.TIME_TO_AI, "[Debug] Skip +10s");
            Advance(10f);
        }

        [ContextMenu("Debug/Skip To Night")]
        private void SkipToNight()
        {
            if (!CanSkip() || _isNight)
            {
                return;
            }

            AILog.Log(AILog.TIME_TO_AI, "[Debug] Skip to night");
            Advance(DayDuration - _phaseElapsed + 0.01f);
        }

        [ContextMenu("Debug/Skip To Next Day")]
        private void SkipToNextDay()
        {
            if (!CanSkip())
            {
                return;
            }

            if (!_isNight)
            {
                SkipToNight();
            }

            AILog.Log(AILog.TIME_TO_AI, "[Debug] Skip to next day");
            Advance(_nightDuration - _phaseElapsed + 0.01f);
        }

        private bool CanSkip()
        {
            if (!Application.isPlaying || !_isRunning)
            {
                AILog.Warn(AILog.TIME_TO_AI, "Skip works only while clock is running");
                return false;
            }

            return true;
        }
    }
}
