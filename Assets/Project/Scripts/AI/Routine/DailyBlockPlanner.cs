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
            int length = config.RoutineHours;
            int endLimit = Mathf.Clamp(Mathf.FloorToInt(latestEndHour), DayClock.DAY_START_HOUR, DayClock.DAY_END_HOUR);

            int[] gaps = new int[count];
            int gapTotal = 0;
            for (int i = 0; i < count; i++)
            {
                float seconds = travelSeconds != null && i < travelSeconds.Count ? travelSeconds[i] : 0f;
                gaps[i] = Mathf.CeilToInt(seconds / DayClock.SecondsPerHour);
                gapTotal += gaps[i];
            }

            int free = endLimit - DayClock.DAY_START_HOUR - length * count - gapTotal;
            note = $"gaps=[{string.Join(",", gaps)}]h free={free}h end≤{endLimit}:00";

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
            int startDelay = random.Next(Mathf.Min(config.MaxStartDelayHours, free) + 1);
            free -= startDelay;

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            int hour = DayClock.DAY_START_HOUR + startDelay;

            for (int i = 0; i < count; i++)
            {
                int extra = i > 0 ? random.Next(free + 1) : 0;
                free -= extra;

                int blockStart = hour + gaps[i] + extra;
                int blockEnd = Mathf.Min(blockStart + length, DayClock.DAY_END_HOUR);
                if (blockStart >= blockEnd)
                {
                    break;
                }

                float startTime = DayClock.ToDayElapsed(blockStart);
                float travel = travelSeconds != null && i < travelSeconds.Count ? travelSeconds[i] : 0f;

                blocks.Add(new DailyBlock
                {
                    Index = i,
                    StartHour = blockStart,
                    EndHour = blockEnd,
                    StartTime = startTime,
                    EndTime = DayClock.ToDayElapsed(blockEnd),
                    DepartTime = Mathf.Max(DayClock.ToDayElapsed(hour), startTime - travel)
                });

                hour = blockEnd;
            }

            return blocks;
        }
    }
}
