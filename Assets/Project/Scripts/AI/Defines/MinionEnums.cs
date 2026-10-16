namespace ProjectShaman.AI.Defines
{
    public enum MinionState
    {
        Inactive,
        MoveToObjective,
        ChasePlayer,
        Attack,
        Dead
    }

    public enum MinionTargetMode
    {
        ObjectiveOnly,
        ObjectiveFirst,
        PlayerFirst
    }
}
