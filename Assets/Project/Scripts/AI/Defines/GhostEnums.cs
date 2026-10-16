using Unity.Behavior;

namespace ProjectShaman.AI.Defines
{
    [BlackboardEnum]
    public enum CorruptionAxis
    {
        None,
        Tool,
        Action,
        Place,
        Time
    }

    [BlackboardEnum]
    public enum SymptomType
    {
        Tremble,
        StareIntoVoid,
        Nonsense,
        Grotesque,
        EmptyHandTool
    }

    [BlackboardEnum]
    public enum GhostActionVerb
    {
        None,
        Attack,
        SleepParalysis,
        Defile,
        Absorb,
        HideTool,
        Haunt
    }

    [BlackboardEnum]
    public enum GhostActionShape
    {
        None,
        Instant,
        Carry
    }

    [BlackboardEnum]
    public enum GhostTargetKind
    {
        Villager,
        Player,
        Animal,
        Well,
        Storage,
        Guardian,
        Offering,
        ShadowSpot,
        Tool,
        AnomalySpot
    }

    [BlackboardEnum]
    public enum GhostDayEffect
    {
        Dazed
    }
}
