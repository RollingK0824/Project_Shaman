using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public interface IGhostTarget
    {
        GhostTargetKind Kind { get; }
        GameObject gameObject { get; }
        bool CanBeTargeted(in GhostActionContext context);
        int GetPriority(in GhostActionContext context);
    }
}
