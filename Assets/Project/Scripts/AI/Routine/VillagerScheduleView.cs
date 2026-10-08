using System;
using System.Collections.Generic;

namespace ProjectShaman.AI.Routine
{
    public class VillagerScheduleView
    {
        private readonly RoutineSlotInfo[] _slots;
        private readonly RoutineSlotInfo[][] _days;

        public int CycleDays { get; }
        public int SlotsPerDay { get; }
        public IReadOnlyList<RoutineSlotInfo> FlatSlots => _slots;

        private VillagerScheduleView(int cycleDays, int slotsPerDay, RoutineSlotInfo[] slots)
        {
            CycleDays = cycleDays;
            SlotsPerDay = slotsPerDay;
            _slots = slots;
            _days = new RoutineSlotInfo[cycleDays][];

            for (int d = 0; d < cycleDays; d++)
            {
                _days[d] = new RoutineSlotInfo[slotsPerDay];
                Array.Copy(_slots, d * slotsPerDay, _days[d], 0, slotsPerDay);
            }
        }

        public static VillagerScheduleView FromSchedule(VillagerSchedule schedule)
        {
            if (schedule == null || schedule.CycleDays <= 0 || schedule.SlotsPerDay <= 0)
            {
                return null;
            }

            RoutineSlotInfo[] slots = new RoutineSlotInfo[schedule.CycleDays * schedule.SlotsPerDay];

            for (int d = 0; d < schedule.CycleDays; d++)
            {
                for (int s = 0; s < schedule.SlotsPerDay; s++)
                {
                    slots[d * schedule.SlotsPerDay + s] = RoutineSlotInfo.From(schedule.Get(d + 1, s));
                }
            }

            return new VillagerScheduleView(schedule.CycleDays, schedule.SlotsPerDay, slots);
        }

        public static VillagerScheduleView FromFlat(int cycleDays, int slotsPerDay, IReadOnlyList<RoutineSlotInfo> flatSlots)
        {
            if (cycleDays <= 0 || slotsPerDay <= 0 || flatSlots == null || flatSlots.Count != cycleDays * slotsPerDay)
            {
                return null;
            }

            RoutineSlotInfo[] slots = new RoutineSlotInfo[flatSlots.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = flatSlots[i];
            }

            return new VillagerScheduleView(cycleDays, slotsPerDay, slots);
        }

        public RoutineSlotInfo Get(int dayCount, int slot)
        {
            if (slot < 0 || slot >= SlotsPerDay || dayCount < 1)
            {
                return default;
            }

            return _days[(dayCount - 1) % CycleDays][slot];
        }

        public IReadOnlyList<RoutineSlotInfo> GetDay(int dayCount)
        {
            if (dayCount < 1)
            {
                return Array.Empty<RoutineSlotInfo>();
            }

            return _days[(dayCount - 1) % CycleDays];
        }
    }
}
