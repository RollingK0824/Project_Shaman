using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    // Plans one villager's day in seconds on the base split (blocks 0.4 + 0.4, spare 0.2 of the day).
    // The walk to a place is part of its block (it starts when the villager sets off).
    // The walk home eats nearly all of the spare, so the spread between villagers is cut from the blocks instead:
    // each villager gets its own start delay and its own length for every block but the last (up to
    // BlockLengthVariance shorter), and the last block takes what is left before the walk home (never below
    // MinRoutineSeconds). Where a block ends and the next begins therefore differs from villager to villager.
    public static class DailyBlockPlanner
    {
        public static List<DailyBlock> Build(int seed, AIBehaviourConfig config, float homeReturnTime, out string note)
        {
            int count = config.RoutinesPerDay;
            float dayEnd = TimeManager.DAY_DURATION;
            float end = Mathf.Clamp(homeReturnTime, 0f, dayEnd);
            float length = config.RoutineSeconds;
            float minLength = config.MinRoutineSeconds;
            System.Random random = new System.Random(seed);

            float[] lengths = new float[count];
            float leading = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                lengths[i] = Mathf.Max(minLength, length * (1f - config.BlockLengthVariance * (float)random.NextDouble()));
                leading += lengths[i];
            }

            // The last block must still fit its minimum: shorten the leading ones toward theirs if not
            float over = leading + minLength - end;
            for (int i = 0; i < count - 1 && over > 0f; i++)
            {
                float cut = Mathf.Min(over, lengths[i] - minLength);
                lengths[i] -= cut;
                leading -= cut;
                over -= cut;
            }

            float room = Mathf.Max(0f, end - leading - minLength);
            float delay = Next(random, Mathf.Min(config.MaxStartDelaySeconds, room));
            float rest = end - delay - leading;
            float gap = Next(random, Mathf.Max(0f, rest - length));
            lengths[count - 1] = Mathf.Min(length, Mathf.Max(0f, rest - gap));

            note = $"delay={delay:F0}s lengths=[{Join(lengths)}]s gap={gap:F0}s";

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            float time = delay;

            for (int i = 0; i < count; i++)
            {
                if (i == count - 1)
                {
                    time += gap;
                }

                float startTime = time;
                float endTime = Mathf.Min(time + lengths[i], dayEnd);
                if (startTime >= endTime)
                {
                    break;
                }

                // Hours only label the schedule tab; the villager runs on the seconds
                int startHour = Mathf.RoundToInt(DayClock.ToHour(startTime));
                int endHour = Mathf.Max(startHour + 1, Mathf.RoundToInt(DayClock.ToHour(endTime)));

                blocks.Add(new DailyBlock
                {
                    Index = i,
                    StartHour = startHour,
                    EndHour = Mathf.Min(endHour, DayClock.DAY_END_HOUR),
                    StartTime = startTime,
                    EndTime = endTime,
                    DepartTime = startTime
                });

                time = endTime;
            }

            return blocks;
        }

        private static string Join(float[] seconds)
        {
            return string.Join(",", System.Array.ConvertAll(seconds, s => s.ToString("F0")));
        }

        private static float Next(System.Random random, float max)
        {
            return max > 0f ? (float)random.NextDouble() * max : 0f;
        }
    }
}
