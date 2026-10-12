using UnityEngine;

namespace ProjectShaman.AI.Ghost
{
    public interface IGhostCarriable : IGhostTarget
    {
        void PickUpByGhost(in GhostActionContext context);
        void DropByGhost(in GhostActionContext context, Vector3 position);
    }
}
