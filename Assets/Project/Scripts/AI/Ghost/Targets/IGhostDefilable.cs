namespace ProjectShaman.AI.Ghost
{
    public interface IGhostDefilable : IGhostTarget
    {
        void ReceiveDefile(in GhostActionContext context);
    }
}
