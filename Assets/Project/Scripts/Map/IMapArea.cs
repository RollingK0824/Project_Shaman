using UnityEngine;

namespace ProjectShaman.Map
{
    // A named region of the map that positions resolve to (quest text, dialogue, memory, map labels).
    // Areas are scene objects, identical on every peer, so only AreaId ever crosses the network.
    public interface IMapArea
    {
        string AreaId { get; }
        string DisplayName { get; }
        int Depth { get; }
        float Footprint { get; }
        Vector3 Center { get; }
        bool Contains(Vector3 position);
    }
}
