namespace ProjectShaman.AI.Core
{
    public static class DayClock
    {
        public const int DAY_START_HOUR = 5;
        public const int DAY_END_HOUR = 20;
        public const int DAY_HOURS = DAY_END_HOUR - DAY_START_HOUR;

        public static float SecondsPerHour => TimeManager.DAY_DURATION / DAY_HOURS;

        public static float ToHour(float dayElapsedSeconds)
        {
            return DAY_START_HOUR + dayElapsedSeconds / SecondsPerHour;
        }

        public static float ToDayElapsed(float hour)
        {
            return (hour - DAY_START_HOUR) * SecondsPerHour;
        }

        public static float HoursToSeconds(float hours)
        {
            return hours * SecondsPerHour;
        }
    }
}
