using System;
using System.Collections.Generic;
using UnityEngine;

// One player's position for the notebook map, read once when the Map tab opens.
public struct MapPlayerSnapshot
{
    public uint NetId;
    public int ColorId;     // colour: PlayerColors.Get(CharacterId)
    public string Name;
    public Vector3 Position;    // the body, also while spectating
    public Vector3 Forward;     // Vector3.zero = no arrow
    public bool IsDead;
}

// Implemented by the Network side. Answer now or after a server round-trip, both are fine.
public interface IMapPlayerSource
{
    // Every player except the local one
    void RequestOtherPlayers(Action<IReadOnlyList<MapPlayerSnapshot>> onResult);
}

// The Network side sets Current on client start and clears it on stop. null = only the local player is shown.
public static class MapPlayerSource
{
    public static IMapPlayerSource Current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Current = null;
    }
}
