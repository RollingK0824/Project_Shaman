using UnityEngine;

namespace ProjectShaman.AI.Interfaces
{
    public interface ISpawnHandler
    {
        string HandlerName { get; }
        bool CanSpawn { get; }
        void Spawn(GameObject instance);
        void Despawn(GameObject instance);
    }
}
