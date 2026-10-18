using Unity.Behavior;

namespace ProjectShaman.AI.Defines
{
    public enum InteractionKind
    {
        None,
        Conversation,
        GhostTrace,
        VillagerSymptom,
        MisplacedTool
    }

    [BlackboardEnum]
    public enum InteractionResponse
    {
        None,
        Glance,
        Stop,
        Approach,
        Converse
    }

    public enum VillagerActivity
    {
        Rest,
        Conversation,
        Moving,
        ToolSearch,
        Work,
        ReturningHome
    }

    public enum InteractionEndReason
    {
        Completed,
        Preempted,
        TargetLost,
        RoutineChanged,
        MustGoHome,
        Died
    }
}
