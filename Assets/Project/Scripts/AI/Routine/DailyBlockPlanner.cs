using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    // The day keeps its base split (blocks 0.4 + 0.4, spare 0.2): the walk to a place is part of its block,
    // the spare holds the start delay and the walk home. Only when this villager has to set off home before
    // the last block ends (far place, long look-in on the way) are the blocks cut, never below MinRoutineHours.
    public static class DailyBlockPlanner
    {
        public static List<DailyBlock> Build(int seed, AIBehaviourConfig config, float latestEndHour, out string note)
        {
            int count = config.RoutinesPerDay;
            int endLimit = Mathf.Clamp(Mathf.FloorToInt(latestEndHour), DayClock.DAY_START_HOUR, DayClock.DAY_END_HOUR);
            int length = config.RoutineHours;
            int lengths = length * count;

            // Room left before going home decides how much start delay fits; no room means the blocks are cut
            int room = endLimit - DayClock.DAY_START_HOUR - lengths;
            int delay = 0;
            int lastCut = 0;
            int shrunk = length;

            if (room > 0)
            {
                System.Random random = new System.Random(seed);
                delay = random.Next(Mathf.Min(room, config.MaxStartDelayHours) + 1);
            }
            else if (room < 0)
            {
                // The walk home first trims the last block, down to its minimum; past that every block shares the cut
                lastCut = Mathf.Min(-room, length - config.MinRoutineHours);
                int rest = -room - lastCut;
                if (rest > 0)
                {
                    shrunk = Mathf.Max(config.MinRoutineHours, length - Mathf.CeilToInt((float)rest / Mathf.Max(1, count - 1)));
                }
            }

            note = $"length={length}h room={room}h delay={delay}h{(room < 0 ? $" cut last -{lastCut}h, others {shrunk}h (home by {endLimit}:00)" : "")}";

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            int hour = DayClock.DAY_START_HOUR + delay;

            for (int i = 0; i < count; i++)
            {
                int blockLength = i == count - 1 ? Mathf.Max(config.MinRoutineHours, Mathf.Min(shrunk, length - lastCut)) : shrunk;
                int blockStart = hour;
                int blockEnd = Mathf.Min(blockStart + blockLength, DayClock.DAY_END_HOUR);
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
                    DepartTime = startTime
                });

                hour = blockEnd;
            }

            return blocks;
        }
    }
}
