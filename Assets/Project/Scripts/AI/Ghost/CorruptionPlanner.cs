using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Ghost
{
    public static class CorruptionPlanner
    {
        private static readonly SymptomType[] REST_SYMPTOMS =
        {
            SymptomType.Tremble,
            SymptomType.StareIntoVoid,
            SymptomType.Nonsense,
            SymptomType.Grotesque
        };

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
                else if (routine.Category == RoutineCategory.Rest)
                {
                    PlanSymptoms(plan, slot, yinFactor, config, random);
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
            List<CorruptionAxis> axes = new List<CorruptionAxis>();

            if (routine.WrongToolIds.Count > 0)
            {
                axes.Add(CorruptionAxis.Tool);
            }

            if (routine.WrongActionIds.Count > 0)
            {
                axes.Add(CorruptionAxis.Action);
            }

            if (routine.WrongPlaceIds.Count > 0)
            {
                axes.Add(CorruptionAxis.Place);
            }

            axes.Add(CorruptionAxis.Time);
            return axes;
        }

        private static void PlanSymptoms(CorruptionPlan plan, int slot, float yinFactor, AIBehaviourConfig config, System.Random random)
        {
            int count = Mathf.RoundToInt(yinFactor * config.MaxSymptomsPerRestSlot);

            for (int i = 0; i < count; i++)
            {
                plan.Symptoms.Add(new PlannedSymptom
                {
                    Slot = slot,
                    SlotFraction = Mathf.Lerp(0.2f, 0.9f, (float)random.NextDouble()),
                    Type = REST_SYMPTOMS[random.Next(REST_SYMPTOMS.Length)]
                });
            }
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
