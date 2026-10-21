namespace ProjectShaman.AI.Defines
{
    public enum GhostTraceKind
    {
        LivestockCorpse,
        VillagerCorpse,
        DefiledWell,
        DefiledStorage,
        DefiledGuardian,
        DefiledOffering,
        ShadowSpot,
        Anomaly
    }

    public enum TraceResolveReason
    {
        ClearedByPlayer,
        Item,
        Request,
        Capped,
        GameEnd
    }
}
