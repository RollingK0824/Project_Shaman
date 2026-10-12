namespace ProjectShaman.AI.Ghost
{
    public interface IGhostAbsorbable : IGhostTarget
    {
        void ReceiveAbsorb(in GhostActionContext context);
    }
}
