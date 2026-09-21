using Unity.Behavior;

namespace ProjectShaman.AI.Defines
{
    [BlackboardEnum]
    public enum AIState
    {
        Idle,
        Moving,
        Working,
        Sleeping,
        Interacting,
        Panicking
    }
}
