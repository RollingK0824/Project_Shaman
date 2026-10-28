using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    // A villager's day runs in order (C131): it gets up, goes to routine A, works it for its fixed length, tidies up,
    // goes to B, works it, then has free time until nightfall sends it home. Only the order and the lengths are fixed;
    // a block starts when the villager is really there. The times here are estimates for the schedule tab, laid one
    // after another from the wake-up (or from a block that has really started)
    public static class DailyBlockPlanner
    {
        // Up to MaxStartDelaySeconds into the day, so villagers do not all set off at once
        public static float PickWakeTime(int seed, AIBehaviourConfig config)
        {
            return (float)new System.Random(seed).NextDouble() * config.MaxStartDelaySeconds;
        }

        // Blocks from `index` on, each after the previous one's end (or `time`) plus leadSeconds(i): the tidy-up and the walk
        // to its place. A block that would not begin before nightfall gets no hours (the tab shows nothing for it)
        public static void Lay(List<DailyBlock> blocks, int index, float time, float length, System.Func<int, float> leadSeconds)
        {
            float dayEnd = TimeManager.DAY_DURATION;
            for (int i = index; i < blocks.Count; i++)
            {
                float start = Mathf.Min(time + leadSeconds(i), dayEnd);
                blocks[i] = Make(i, start, Mathf.Min(start + length, dayEnd));
                time = blocks[i].EndTime;
            }
        }

        public static DailyBlock Make(int index, float startTime, float endTime)
        {
            // Hours only label the schedule tab; the villager runs on the seconds
            int startHour = Mathf.RoundToInt(DayClock.ToHour(startTime));
            int endHour = endTime > startTime
                ? Mathf.Min(Mathf.Max(startHour + 1, Mathf.RoundToInt(DayClock.ToHour(endTime))), DayClock.DAY_END_HOUR)
                : startHour;

            return new DailyBlock
            {
                Index = index,
                StartHour = startHour,
                EndHour = Mathf.Max(startHour, endHour),
                StartTime = startTime,
                EndTime = endTime,
                DepartTime = startTime
            };
        }
    }
}
