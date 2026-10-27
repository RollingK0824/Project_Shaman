using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    // Plans one villager's day in seconds: a start delay, then each routine block followed by free time, up to the walk home.
    // The walk to a place is part of its block (it starts when the villager sets off). Per villager and day the free share
    // (FreeTimeRatioMin..Max of the working day) and the block lengths (up to BlockLengthVariance shorter) are rolled,
    // and the free time is split at random after the blocks, so villagers switch, rest and head home at different moments.
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

            float delay = Next(random, Mathf.Min(config.MaxStartDelaySeconds, Mathf.Max(0f, end - count * minLength)));
            float window = Mathf.Max(0f, end - delay);
            float freeRatio = Mathf.Lerp(config.FreeTimeRatioMin, config.FreeTimeRatioMax, (float)random.NextDouble());
            float work = Mathf.Max(Mathf.Min(window, count * minLength), window * (1f - freeRatio));

            // Block lengths share the work time by random weights, each kept within [minLength, length];
            // what a cap leaves over becomes free time too
            float[] lengths = new float[count];
            float weightSum = 0f;
            for (int i = 0; i < count; i++)
            {
                lengths[i] = 1f - config.BlockLengthVariance * (float)random.NextDouble();
                weightSum += lengths[i];
            }

            float used = 0f;
            for (int i = 0; i < count; i++)
            {
                lengths[i] = Mathf.Clamp(work * lengths[i] / weightSum, Mathf.Min(minLength, work / count), length);
                used += lengths[i];
            }

            // Free time after each block (the last one's runs up to the walk home)
            float free = Mathf.Max(0f, window - used);
            float[] frees = new float[count];
            float freeWeightSum = 0f;
            for (int i = 0; i < count; i++)
            {
                frees[i] = (float)random.NextDouble();
                freeWeightSum += frees[i];
            }

            for (int i = 0; i < count; i++)
            {
                frees[i] = freeWeightSum > 0f ? free * frees[i] / freeWeightSum : free / count;
            }

            note = $"delay={delay:F0}s lengths=[{Join(lengths)}]s free=[{Join(frees)}]s";

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            float time = delay;

            for (int i = 0; i < count; i++)
            {
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

                time = endTime + frees[i];
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
