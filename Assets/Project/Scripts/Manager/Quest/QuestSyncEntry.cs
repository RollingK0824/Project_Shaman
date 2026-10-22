// One quest as it crosses the network: ids and numbers only; clients rebuild text from their own data
public struct QuestSyncEntry
{
    public int InstanceId;
    public string QuestId;
    public string GiverId;
    public string TargetId;
    public string ContextId;
    public int Progress;
    public int State;
    public int DayIssued;
    public uint AcceptedBy;
}
