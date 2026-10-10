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
                if (player != null && !player.IsDead) count++;
            }
            return count;
        }
    }

    public static event Action Changed;
    public static event Action<int> AliveCountChanged;
    public static event Action AllPlayersDead;
    private static int _lastAliveCount;
    private static bool _allDead;

    public static void Register(NetPlayerStatus player)
    {
        if (player == null || _players.Contains(player)) return;

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
        int alive = AliveCount;
        bool allDead = _players.Count > 0 && alive == 0;
        bool becameAllDead = allDead && !_allDead;
        _allDead = allDead;
        if (alive != _lastAliveCount)
        {
            _lastAliveCount = alive;
            AliveCountChanged?.Invoke(alive);
        }
        Changed?.Invoke();
        if (becameAllDead) AllPlayersDead?.Invoke();
    }

    // Domain Reload를 끈 에디터에서도 이전 플레이의 목록과 구독을 제거합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _players.Clear();
        Changed = null;
        AliveCountChanged = null;
        AllPlayersDead = null;
        _lastAliveCount = 0;
        _allDead = false;
    }
}
