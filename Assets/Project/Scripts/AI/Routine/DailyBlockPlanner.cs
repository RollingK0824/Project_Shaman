using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    public static class DailyBlockPlanner
    {
        public static List<DailyBlock> Build(int seed, AIBehaviourConfig config)
        {
            int count = config.RoutinesPerDay;
            int length = config.RoutineHours;
            int slack = Mathf.Max(0, DayClock.DAY_HOURS - length * count);
            int maxDelay = Mathf.Min(config.MaxStartDelayHours, slack);

            System.Random random = new System.Random(seed);
            int startHour = DayClock.DAY_START_HOUR + random.Next(maxDelay + 1);

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            for (int i = 0; i < count; i++)
            {
                int blockStart = startHour + i * length;
                int blockEnd = Mathf.Min(blockStart + length, DayClock.DAY_END_HOUR);

                if (blockStart >= blockEnd)
                {
                    break;
                }

                blocks.Add(new DailyBlock
                {
                    Index = i,
                    StartHour = blockStart,
                    EndHour = blockEnd,
                    StartTime = DayClock.ToDayElapsed(blockStart),
                    EndTime = DayClock.ToDayElapsed(blockEnd)
                });
            }

            return blocks;
        }
    }
}
