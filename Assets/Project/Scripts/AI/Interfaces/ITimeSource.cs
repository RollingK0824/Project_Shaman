namespace ProjectShaman.AI.Interfaces
{
    public interface ITimeSource
    {
        string SourceName { get; }
        bool IsRunning { get; }
        int DayCount { get; }
        bool IsNight { get; }
        float PhaseElapsed { get; }
        float DayDuration { get; }
        float NightDuration { get; }
        int SlotsPerDay { get; }
        float SlotDuration { get; }

        event System.Action<int> OnNewDay;
        event System.Action OnNightStart;

        void StartClock();
        void StopClock();
    }
}
