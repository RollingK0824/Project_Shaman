using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public interface IGhostBarrier
    {
        bool Blocks(Vector3 position, GhostActionVerb verb);
    }
}
