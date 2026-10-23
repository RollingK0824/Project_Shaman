using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    public static class DailyBlockPlanner
    {
        public static List<DailyBlock> Build(int seed, AIBehaviourConfig config, IReadOnlyList<float> travelSeconds, float latestEndHour, out string note)
        {
            int count = config.RoutinesPerDay;
            int endLimit = Mathf.Clamp(Mathf.FloorToInt(latestEndHour), DayClock.DAY_START_HOUR, DayClock.DAY_END_HOUR);

            int[] gaps = new int[count];
            int gapTotal = 0;
            for (int i = 0; i < count; i++)
            {
                gaps[i] = Mathf.CeilToInt(TravelAt(travelSeconds, i) / DayClock.SecondsPerHour);
                gapTotal += gaps[i];
            }

            int available = endLimit - DayClock.DAY_START_HOUR - gapTotal;
            int length = config.RoutineHours;
            if (available < length * count)
            {
                length = Mathf.Max(config.MinRoutineHours, available / Mathf.Max(1, count));
            }

            int free = available - length * count;
            note = $"length={length}h gaps=[{string.Join(",", gaps)}]h free={free}h end≤{endLimit}:00";

            for (int i = count - 1; i >= 0 && free < 0; i--)
            {
                int cut = Mathf.Min(gaps[i], -free);
                gaps[i] -= cut;
                free += cut;
            }

            if (free < 0)
            {
                note += " (short, last block trimmed by home return)";
                free = 0;
            }

            System.Random random = new System.Random(seed);
            int[] spare = SplitSpare(free, count + 1, config.MaxStartDelayHours, random);

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            int hour = DayClock.DAY_START_HOUR;

            for (int i = 0; i < count; i++)
            {
                int leave = hour + spare[i];
                int blockStart = leave + gaps[i];
                int blockEnd = Mathf.Min(blockStart + length, DayClock.DAY_END_HOUR);
                if (blockStart >= blockEnd)
                {
                    break;
                }

                float startTime = DayClock.ToDayElapsed(blockStart);

                blocks.Add(new DailyBlock
                {
                    Index = i,
                    StartHour = blockStart,
                    EndHour = blockEnd,
                    StartTime = startTime,
                    EndTime = DayClock.ToDayElapsed(blockEnd),
                    DepartTime = Mathf.Max(DayClock.ToDayElapsed(leave), startTime - TravelAt(travelSeconds, i))
                });

                hour = blockEnd;
            }

            return blocks;
        }

        private static int[] SplitSpare(int free, int parts, int firstCap, System.Random random)
        {
            int[] spare = new int[parts];

            for (int unit = 0; unit < free; unit++)
            {
                int slot = random.Next(parts);
                if (slot == 0 && spare[0] >= firstCap)
                {
                    slot = 1 + random.Next(parts - 1);
                }

                spare[slot]++;
            }

            return spare;
        }

        private static float TravelAt(IReadOnlyList<float> travelSeconds, int index)
        {
            return travelSeconds != null && index < travelSeconds.Count ? travelSeconds[index] : 0f;
        }
    }
}
