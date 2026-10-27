namespace ProjectShaman.AI.Core
{
    public static class AIEvents
    {
        public static event System.Action<string, string, float> OnVillagerStress;
        public static event System.Action<string, string, string> OnVillagerRequest;
        public static event System.Action<string, string> OnVillagerToolRecovered;

        public static event System.Action<string, string> OnVillagerDied;
        public static event System.Action<string> OnVillagerDeathKnown;
        public static event System.Action<string> OnVillagerExorcised;
        // (villagerId, day, block hour pairs, home hour, bed hour): the day's hours shown on the schedule tab
        public static event System.Action<string, int, int[], int, int> OnVillagerDayPlanned;

        // (villagerId, day, slot, rested, rest place id or null): a work block given up for a missing tool (shown as rest
        // on the schedule tab, with where the villager went), or taken up again once the tool is back
        public static event System.Action<string, int, int, bool, string> OnVillagerSlotRested;

        // (villagerId, day, free-time hour pairs, what is done, place id or null): today's free time on the schedule tab
        public static event System.Action<string, int, int[], string[], string[]> OnVillagerFreeTimePlanned;

        public static void RaiseFreeTimePlanned(string villagerId, int day, int[] hours, string[] names, string[] placeIds)
        {
            OnVillagerFreeTimePlanned?.Invoke(villagerId, day, hours, names, placeIds);
        }

        public static void RaiseSlotRested(string villagerId, int day, int slot, bool rested, string restPlaceId)
        {
            OnVillagerSlotRested?.Invoke(villagerId, day, slot, rested, restPlaceId);
        }

        public static void RaiseDayPlanned(string villagerId, int day, int[] blockHours, int homeHour, int bedHour)
        {
            OnVillagerDayPlanned?.Invoke(villagerId, day, blockHours, homeHour, bedHour);
        }

        public static void RaiseExorcised(string villagerId)
        {
            AILog.Log(AILog.EVENT, villagerId, "Exorcised");
            OnVillagerExorcised?.Invoke(villagerId);
        }

        public static void RaiseDeathKnown(string villagerId)
        {
            AILog.Log(AILog.EVENT, villagerId, "Death made known");
            OnVillagerDeathKnown?.Invoke(villagerId);
        }

        public static void RaiseVillagerDied(string villagerId, string reason)
        {
            AILog.Log(AILog.EVENT, villagerId, $"Villager died ({reason})");
            OnVillagerDied?.Invoke(villagerId, reason);
        }

        public static void RaiseStress(string villagerId, string reason, float amount)
        {
            AILog.Log(AILog.EVENT, villagerId, $"Stress {amount:+0.#;-0.#} ({reason})");
            OnVillagerStress?.Invoke(villagerId, reason, amount);
        }

        public static void RaiseRequest(string villagerId, string requestType, string detail)
        {
            AILog.Log(AILog.EVENT, villagerId, $"[Stub] Request '{requestType}' ({detail})");
            OnVillagerRequest?.Invoke(villagerId, requestType, detail);
        }

        // The villager found a tool of this kind by its own search (not handed over by a player)
        public static void RaiseToolRecovered(string villagerId, string toolTypeId)
        {
            OnVillagerToolRecovered?.Invoke(villagerId, toolTypeId);
        }
    }
}
