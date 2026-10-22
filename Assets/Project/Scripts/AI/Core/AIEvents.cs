namespace ProjectShaman.AI.Core
{
    public static class AIEvents
    {
        public static event System.Action<string, string, float> OnVillagerStress;
        public static event System.Action<string, string, string> OnVillagerRequest;
        public static event System.Action<string, string> OnVillagerToolRecovered;

        public static event System.Action<string, string> OnVillagerDied;
        public static event System.Action<string> OnVillagerDeathKnown;

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
