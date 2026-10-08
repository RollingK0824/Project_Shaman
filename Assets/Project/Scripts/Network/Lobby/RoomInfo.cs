using UnityEngine;

public readonly struct RoomInfo
{
    public readonly string Name;
    public readonly string Owner;
    public readonly string Address;             // 호스트 SteamId64 
    public readonly int CurrentPlayers;
    public readonly int MaxPlayers;
    public readonly int GhostCount;
    public readonly bool IsWaiting;
    public readonly bool IsJoinable;            // 대기중이고 자리 있을때만 true

    public bool IsFull => MaxPlayers > 0 && CurrentPlayers >= MaxPlayers;

    public RoomInfo(string name, string owner, string address, int currentPlayers, int maxPlayers, int ghostCount, bool isWaiting, bool isJoinable)
    {
        Name = name;
        Owner = owner;
        Address = address;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        GhostCount = ghostCount;
        IsWaiting = isWaiting;
        IsJoinable = isJoinable;
    }
}
