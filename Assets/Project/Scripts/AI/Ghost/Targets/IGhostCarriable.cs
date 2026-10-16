using UnityEngine;

namespace ProjectShaman.AI.Ghost
{
    public interface IGhostCarriable : IGhostTarget
    {
        bool PickUpByGhost(in GhostActionContext context);
        void DropByGhost(in GhostActionContext context, Vector3 position);
    }
}
