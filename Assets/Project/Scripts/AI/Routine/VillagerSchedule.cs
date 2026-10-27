using System.Text;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Routine
{
    public class VillagerSchedule
    {
        private readonly RoutineEntry[][] _slots;

        public int CycleDays { get; }
        public int SlotsPerDay { get; }

        public VillagerSchedule(int cycleDays, int slotsPerDay)
        {
            CycleDays = cycleDays;
            SlotsPerDay = slotsPerDay;
            _slots = new RoutineEntry[cycleDays][];

            for (int d = 0; d < cycleDays; d++)
            {
                _slots[d] = new RoutineEntry[slotsPerDay];
            }
        }

        public RoutineEntry Get(int dayCount, int slot)
        {
            if (slot < 0 || slot >= SlotsPerDay || dayCount < 1)
            {
                return null;
            }

            return _slots[(dayCount - 1) % CycleDays][slot];
        }

        // The next slot after (dayCount, slot) whose routine uses this tool, at most one cycle ahead
        public bool TryFindNextToolUse(int dayCount, int slot, string toolId, out int daysAhead, out RoutineEntry routine)
        {
            for (int step = 1; step <= CycleDays * SlotsPerDay; step++)
            {
                int index = slot + step;
                int day = dayCount + index / SlotsPerDay;
                RoutineEntry entry = Get(day, index % SlotsPerDay);

                if (entry != null && entry.ToolId == toolId)
                {
                    daysAhead = day - dayCount;
                    routine = entry;
                    return true;
                }
            }

            daysAhead = 0;
            routine = null;
            return false;
        }

        public void Set(int cycleDay, int slot, RoutineEntry routine)
        {
            _slots[cycleDay][slot] = routine;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            for (int d = 0; d < CycleDays; d++)
            {
                builder.Append($"[D{d}:");

                for (int s = 0; s < SlotsPerDay; s++)
                {
                    RoutineEntry routine = _slots[d][s];
                    builder.Append(s == 0 ? " " : ", ");
                    builder.Append(routine != null ? routine.RoutineId : "-");
                }

                builder.Append("]");
            }

            return builder.ToString();
        }
    }
}
