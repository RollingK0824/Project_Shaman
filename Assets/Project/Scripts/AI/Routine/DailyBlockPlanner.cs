using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    // Plans one villager's day in seconds on the base split (blocks 0.4 + 0.4, spare 0.2 of the day).
    // The walk to a place is part of its block (it starts when the villager sets off).
    // The spare first holds this villager's walk home; what is left is split at random into a start delay
    // and a break between blocks, so villagers set off and switch at their own moments.
    // A villager with (almost) no spare instead leaves up to StartStaggerSeconds into the first block.
    // Only when the walk home does not fit the spare are blocks cut: the last one first, never below MinRoutineSeconds.
    public static class DailyBlockPlanner
    {
        public static List<DailyBlock> Build(int seed, AIBehaviourConfig config, float homeReturnTime, out string note)
        {
            int count = config.RoutinesPerDay;
            float dayEnd = TimeManager.DAY_DURATION;
            float end = Mathf.Clamp(homeReturnTime, 0f, dayEnd);
            float length = config.RoutineSeconds;
            float minLength = config.MinRoutineSeconds;

            float[] lengths = new float[count];
            float[] breaks = new float[count];
            for (int i = 0; i < count; i++)
            {
                lengths[i] = length;
            }

            System.Random random = new System.Random(seed);
            float free = end - length * count;
            float delay = 0f;
            float stagger = 0f;

            if (free > 0f)
            {
                delay = Next(random, Mathf.Min(free, config.MaxStartDelaySeconds));
                float rest = free - delay;
                for (int i = 0; i < count - 1; i++)
                {
                    breaks[i] = Next(random, rest);
                    rest -= breaks[i];
                }
            }
            else if (free < 0f)
            {
                Cut(lengths, -free, minLength);
            }

            if (free < config.StartStaggerSeconds)
            {
                stagger = Next(random, config.StartStaggerSeconds);
            }

            note = $"free={free:F0}s delay={delay:F0}s stagger={stagger:F0}s breaks=[{string.Join(",", System.Array.ConvertAll(breaks, b => b.ToString("F0")))}]s";

            List<DailyBlock> blocks = new List<DailyBlock>(count);
            float time = delay;

            for (int i = 0; i < count; i++)
            {
                float startTime = time + (i == 0 ? stagger : 0f);
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

                time = endTime + breaks[i];
            }

            return blocks;
        }

        // The walk home trims the last block down to its minimum; past that the earlier blocks share the rest
        private static void Cut(float[] lengths, float amount, float minLength)
        {
            int last = lengths.Length - 1;
            float lastCut = Mathf.Min(amount, lengths[last] - minLength);
            lengths[last] -= lastCut;
            amount -= lastCut;

            for (int i = 0; i < last && amount > 0f; i++)
            {
                float share = Mathf.Min(amount / (last - i), lengths[i] - minLength);
                lengths[i] -= share;
                amount -= share;
            }
        }

        private static float Next(System.Random random, float max)
        {
            return max > 0f ? (float)random.NextDouble() * max : 0f;
        }
    }
}
