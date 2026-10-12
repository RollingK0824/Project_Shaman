namespace ProjectShaman.AI.Ghost
{
    public interface IGhostHauntable : IGhostTarget
    {
        void ReceiveHaunt(in GhostActionContext context);
    }
}
