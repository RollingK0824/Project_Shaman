using System.Collections.Generic;

namespace ProjectShaman.AI.Routine
{
    public struct DailyBlock
    {
        public int Index;
        public int StartHour;
        public int EndHour;
        public float StartTime;
        public float EndTime;
        public float DepartTime;

        public float Length => EndTime - StartTime;

        // [start0, end0, start1, end1, ...] in clock hours; the pair's position is the block index (= schedule slot)
        public static int[] ToHourPairs(IReadOnlyList<DailyBlock> blocks)
        {
            int[] hours = new int[blocks.Count * 2];
            foreach (DailyBlock block in blocks)
            {
                hours[block.Index * 2] = block.StartHour;
                hours[block.Index * 2 + 1] = block.EndHour;
            }

            return hours;
        }

        public override string ToString()
        {
            return $"#{Index} {StartHour:00}:00~{EndHour:00}:00 [depart {DepartTime:F1}s, {StartTime:F1}s~{EndTime:F1}s]";
        }
    }
}
