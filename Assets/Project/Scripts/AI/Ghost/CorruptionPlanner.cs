using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Ghost
{
    public static class CorruptionPlanner
    {
        public static System.Random CreateRandom(int seed, int dayCount, int salt)
        {
            return new System.Random(unchecked(seed * 486187739 + dayCount * 16777619 + salt * 31));
        }

        public static float GetYinFactor(float yin, AIBehaviourConfig config)
        {
            return Mathf.Clamp01(yin / Mathf.Max(1f, config.YinForMaxFrequency));
        }

        public static CorruptionPlan Build(AI_Possession possession, VillagerSchedule schedule, int dayCount, int slotsPerDay, AIBehaviourConfig config)
        {
            CorruptionPlan plan = new CorruptionPlan(dayCount, possession.Yin);
            System.Random random = CreateRandom(possession.Seed, dayCount, 0);
            float yinFactor = GetYinFactor(possession.Yin, config);
            float chance = Mathf.Lerp(config.CorruptionChanceAtZeroYin, config.CorruptionChanceAtMaxYin, yinFactor);

            List<int> workSlots = new List<int>();

            for (int slot = 0; slot < slotsPerDay; slot++)
            {
                RoutineEntry routine = schedule != null ? schedule.Get(dayCount, slot) : null;
                if (routine == null)
                {
                    continue;
                }

                if (routine.Category == RoutineCategory.Work)
                {
                    workSlots.Add(slot);
                    float slotChance = slot == slotsPerDay - 1 ? chance * config.LastSlotChanceMultiplier : chance;
                    if (random.NextDouble() < slotChance)
                    {
                        plan.SetSlotAxis(slot, PickAxis(routine, random));
                    }
                }
            }

            EnsureMinimum(plan, schedule, dayCount, workSlots, slotsPerDay, config.MinCorruptionsPerDay, random);
            return plan;
        }

        public static CorruptionAxis PickAxis(RoutineEntry routine, System.Random random)
        {
            List<CorruptionAxis> axes = GetAvailableAxes(routine);
            return axes[random.Next(axes.Count)];
        }

        public static List<CorruptionAxis> GetAvailableAxes(RoutineEntry routine)
        {
            // Wrong tool, action and place are open to every routine: an empty wrong list in the data means any other one
            // (RoutineResolver picks it from the scene), a filled list limits it. An axis with nothing to swap to at
            // resolve time falls through to the next one, time always works
            return new List<CorruptionAxis> { CorruptionAxis.Tool, CorruptionAxis.Action, CorruptionAxis.Place, CorruptionAxis.Time };
        }

        private static void EnsureMinimum(CorruptionPlan plan, VillagerSchedule schedule, int dayCount, List<int> workSlots, int slotsPerDay, int minimum, System.Random random)
        {
            List<int> clean = new List<int>();

            foreach (int slot in workSlots)
            {
                if (plan.GetSlotAxis(slot) == CorruptionAxis.None && slot != slotsPerDay - 1)
                {
                    clean.Add(slot);
                }
            }

            while (plan.CorruptedSlotCount < minimum && clean.Count > 0)
            {
                int index = random.Next(clean.Count);
                int slot = clean[index];
                clean.RemoveAt(index);
                plan.SetSlotAxis(slot, PickAxis(schedule.Get(dayCount, slot), random));
            }
        }
    }
}
