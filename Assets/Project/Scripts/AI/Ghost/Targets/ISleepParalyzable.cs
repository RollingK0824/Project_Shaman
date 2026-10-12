namespace ProjectShaman.AI.Ghost
{
    public interface ISleepParalyzable : IGhostTarget
    {
        void ReceiveSleepParalysis(in GhostActionContext context);
    }
}
