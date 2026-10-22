using System;

namespace ProjectShaman.AI.Data
{
    // RequestKey is "Kind" or "Kind:Detail" (e.g. "FindTool", "FindTool:Hoe", "Trace:DefiledWell", "Gut")
    [Serializable]
    public struct RequestQuestEntry
    {
        public string RequestKey;
        public string QuestId;

        public RequestQuestEntry(string requestKey, string questId)
        {
            RequestKey = requestKey;
            QuestId = questId;
        }
    }
}
