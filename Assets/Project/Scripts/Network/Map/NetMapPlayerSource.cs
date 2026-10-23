using UnityEngine;
using System;
using System.Collections.Generic;
using Mirror;

// [로컬 클라] 수첩 지도에 다른 플레이어 위치 알려줌
// NetPlayer는 NetworkTransform으로 모든 클라에 위치가 이미 동기화되어 있어 서버에 묻지 않고도 바로 가능
public class NetMapPlayerSource : IMapPlayerSource
{
    public void RequestOtherPlayers(Action<IReadOnlyList<MapPlayerSnapshot>> onResult)
    {
        List<MapPlayerSnapshot> players = new List<MapPlayerSnapshot>();
        NetworkIdentity local = NetworkClient.localPlayer;

        foreach(NetworkIdentity identity in NetworkClient.spawned.Values)
        {
            if (identity == null || identity == local) continue;
            if (!identity.TryGetComponent(out NetPlayerStatus status)) continue;

            players.Add(new MapPlayerSnapshot
            {
                NetId = identity.netId,
                ColorId = status.ColorId,
                Name = status.PlayerName,
                Position = identity.transform.position,
                Forward = status.IsDead ? Vector3.zero : identity.transform.forward,
                IsDead = status.IsDead,
            });
        }

        onResult?.Invoke(players);
    }
}
