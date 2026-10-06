using System;
using System.Collections.Generic;
using UnityEngine;

// 서버 전용 목록. NetPlayerStatus의 서버 생명주기에서만 등록 및 해제합니다.
public static class PlayerRoster
{
    private static readonly List<NetPlayerStatus> _players = new List<NetPlayerStatus>();
    private static readonly IReadOnlyList<NetPlayerStatus> _readOnlyPlayers = _players.AsReadOnly();

    public static IReadOnlyList<NetPlayerStatus> Players => _readOnlyPlayers;

    public static int AliveCount
    {
        get
        {
            int count = 0;
            foreach (NetPlayerStatus player in _players)
            {
                if (!player.IsDead) count++;
            }
            return count;
        }
    }

    public static event Action Changed;

    public static void Register(NetPlayerStatus player)
    {
        if (_players.Contains(player)) return;

        _players.Add(player);
        NotifyChanged();
    }

    public static void Unregister(NetPlayerStatus player)
    {
        if (_players.Remove(player))
        {
            NotifyChanged();
        }
    }

    public static void NotifyChanged()
    {
        Debug.Log($"[PlayerRoster] 참가 {_players.Count}명, 생존 {AliveCount}명");
        Changed?.Invoke();
    }

    // Domain Reload를 끈 에디터에서도 이전 플레이의 목록과 구독을 제거합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _players.Clear();
        Changed = null;
    }
}
