using Unity.Behavior;

namespace ProjectShaman.AI.Defines
{
    // Which ghost possesses a host, fixed at villager creation (each one at most once while there are enough kinds).
    // The order is the order of Temp_GhostEntity's model list (AI_ModelVisual)
    public enum GhostType
    {
        DrownedGhost,
        Dueoksini,
        FacelessGhost,
        GhostGirl
    }

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
