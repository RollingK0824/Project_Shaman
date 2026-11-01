using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;
using ProjectShaman.Map;
using UnityEngine;

public enum DayBlockKind { Work, Rest, Home, Sleep }

public struct DayBlock
{
    public string Title, Place;
    public DayBlockKind Kind;
    public int StartHour, EndHour;
}

// A villager's day as blocks, by the same rules as the notebook schedule (NotebookScheduleView.ShowDay).
// Keeps no state: schedules change mid-day, so every call reads the registry again. The registry is synced to clients
public static class VillagerDayBlocks
{
    // Rows from DayClock.DAY_START_HOUR: the day plus the night, up to 5 a.m. the next day
    public const int TABLE_ROWS = 24;

    // false: no day planned yet, no schedule, or the death is known (nothing to show)
    public static bool TryBuild(IVillagerView villager, List<DayBlock> into)
    {
        into.Clear();
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (villager == null || registry == null || villager.IsDeathKnown || villager.Schedule == null) return false;
        if (!registry.TryGetDayHours(villager.VillagerId, out int day, out int[] hours, out int leave, out int home, out int bed)
            || hours == null || day <= 0) return false;

        int routinesEnd = 0;
        for (int slot = 0; slot * 2 + 1 < hours.Length; slot++)
        {
            RoutineSlotInfo routine = villager.Schedule.Get(day, slot);
            if (routine.IsEmpty) continue;
            int start = hours[slot * 2], end = hours[slot * 2 + 1];
            routinesEnd = Mathf.Max(routinesEnd, end);

            // Work given up for a missing tool: rests there instead, or spent the block searching
            if (registry.IsSlotRested(villager.VillagerId, day, slot, out string restPlaceId))
            {
                bool rested = !string.IsNullOrEmpty(restPlaceId);
                Add(into, rested ? "휴식" : "도구 찾기", rested ? MapAreaRegistry.NameOf(restPlaceId) : null, DayBlockKind.Rest, start, end);
                continue;
            }

            DayBlockKind kind = routine.Category == RoutineCategory.Rest ? DayBlockKind.Rest : DayBlockKind.Work;
            string place = MapAreaRegistry.NameOf(routine.PlaceId) ?? PlaceRegistry.NameOf(routine.PlaceId);
            Add(into, routine.DisplayName, place, kind, start, end);
        }

        // Walk home (one row at least), rest at home, then asleep until the table ends
        int night = DayClock.DAY_START_HOUR + TABLE_ROWS;
        // Routines running past the leave hour push the walk home after them, so the blocks never overlap
        if (leave > 0) leave = Mathf.Max(leave, routinesEnd);
        if (leave > 0 && bed > leave)
        {
            int arrive = Mathf.Min(Mathf.Max(home, leave + 1), bed);
            Add(into, "귀가", "집", DayBlockKind.Home, leave, arrive);
            Add(into, "휴식", "집", DayBlockKind.Home, arrive, bed);
            Add(into, "취침", "집", DayBlockKind.Sleep, bed, night);
        }
        else if (leave > 0)
        {
            Add(into, "귀가 · 취침", "집", DayBlockKind.Sleep, leave, night);
        }

        return true;
    }

    // Clamped to the table like the notebook; empty blocks are dropped
    private static void Add(List<DayBlock> into, string title, string place, DayBlockKind kind, int start, int end)
    {
        int last = DayClock.DAY_START_HOUR + TABLE_ROWS;
        start = Mathf.Clamp(start, DayClock.DAY_START_HOUR, last);
        end = Mathf.Clamp(end, start, last);
        if (end <= start) return;
        into.Add(new DayBlock { Title = title, Place = place, Kind = kind, StartHour = start, EndHour = end });
    }
}
