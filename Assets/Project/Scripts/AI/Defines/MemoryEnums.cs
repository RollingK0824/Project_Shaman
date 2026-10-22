namespace ProjectShaman.AI.Defines
{
    public enum MemoryKind
    {
        Routine,
        TraceWitness,
        Conversation,
        ToolLost,
        SleepParalysis,
        Witness,
        Absent
    }

    public enum MemorySource
    {
        Direct,
        Heard
    }

    public enum VillagerStressState
    {
        Calm,
        Anxious,
        Breaking
    }
}
