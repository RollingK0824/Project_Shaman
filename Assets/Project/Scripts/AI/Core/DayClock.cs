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

        // Night runs from DAY_END_HOUR to the next DAY_START_HOUR; past midnight the hour keeps counting (01 = 25)
        public const int NIGHT_HOURS = 24 - DAY_HOURS;

        public static float NightToHour(float nightElapsedSeconds)
        {
            return DAY_END_HOUR + nightElapsedSeconds / (TimeManager.NIGHT_DURATION / NIGHT_HOURS);
        }

        public static float HoursToSeconds(float hours)
        {
            return hours * SecondsPerHour;
        }
    }
}
