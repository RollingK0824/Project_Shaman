namespace ProjectShaman.AI.Defines
{
    public enum MinionState
    {
        Inactive,
        MoveToObjective,
        ChasePlayer,
        Attack,
        Dead,
        // Pierced a player, presentation playing, despawns when it ends
        Fading
    }

    public enum MinionTargetMode
    {
        ObjectiveOnly,
        ObjectiveFirst,
        PlayerFirst
    }
}
