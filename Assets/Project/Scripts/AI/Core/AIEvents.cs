namespace ProjectShaman.AI.Core
{
    public static class AIEvents
    {
        public static event System.Action<string, string, int> OnVillagerStress;
        public static event System.Action<string, string, string> OnVillagerRequest;

        public static event System.Action<string, string> OnVillagerDied;

        public static void RaiseVillagerDied(string villagerId, string reason)
        {
            AILog.Log(AILog.EVENT, villagerId, $"Villager died ({reason})");
            OnVillagerDied?.Invoke(villagerId, reason);
        }

        public static void RaiseStress(string villagerId, string reason, int stage)
        {
            AILog.Log(AILog.EVENT, villagerId, $"[Stub] Stress stage {stage} ({reason})");
            OnVillagerStress?.Invoke(villagerId, reason, stage);
        }

        public static void RaiseRequest(string villagerId, string requestType, string detail)
        {
            AILog.Log(AILog.EVENT, villagerId, $"[Stub] Request '{requestType}' ({detail})");
            OnVillagerRequest?.Invoke(villagerId, requestType, detail);
        }
    }
}
