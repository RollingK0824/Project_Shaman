using UnityEngine;

namespace ProjectShaman.AI.Interfaces
{
    public interface INetworkMovable
    {
        void MoveTo(Vector3 destination);
        void Stop();
        bool IsStopped { get; }
        bool HasReachedDestination { get; }
        float CurrentSpeed { get; }
    }
}
