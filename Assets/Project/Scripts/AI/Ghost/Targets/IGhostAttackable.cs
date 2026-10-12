namespace ProjectShaman.AI.Ghost
{
    public interface IGhostAttackable : IGhostTarget
    {
        void ReceiveGhostAttack(in GhostActionContext context);
    }
}
