namespace ProjectShaman.AI.Routine
{
    public struct DailyBlock
    {
        public int Index;
        public int StartHour;
        public int EndHour;
        public float StartTime;
        public float EndTime;

        public float Length => EndTime - StartTime;

        public bool Contains(float time)
        {
            return time >= StartTime && time < EndTime;
        }

        public override string ToString()
        {
            return $"#{Index} {StartHour:00}:00~{EndHour:00}:00 [{StartTime:F1}s~{EndTime:F1}s]";
        }
    }
}
