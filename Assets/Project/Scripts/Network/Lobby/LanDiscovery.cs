using UnityEngine;
using System.Net;
using System.Net.Sockets;
using Mirror;
using Mirror.Discovery;
using System;

// LAN 방 찾기 응답. 같은 네트워크의 참가자가 방 목록에 띄울 정보
public struct LanRoomResponse : NetworkMessage
{
    public long serverId;
    public string roomName;
    public string owner;
    public int players;
    public int maxPlayers;
    public int ghostCount;
    public bool inGame;
}

// [호스트] LAN 방을 같은 네트워크에 알림 / [참가자] LAN 방 찾기
// RoomManager 오브젝트에 붙임 
[DisallowMultipleComponent]
public class LanDiscovery : NetworkDiscoveryBase<ServerRequest, LanRoomResponse>
{
    // [참가자] 방 응답을 받을 때마다(같은 방도 주기적으로 다시 옴). serverId로 같은 방 구분
    public event Action<RoomInfo, long> RoomFound;

    // [호스트] 같은 PC에서 이미 다른 호스트가 7777을 쓰고 있으면 광고만 실패하고 게임은 그대로 진행
    public void StartAdvertising()
    {
        try
        {
            AdvertiseServer();
        }
        catch(SocketException e)
        {
            Debug.LogWarning($"[LanDiscovery] 방 광고 실패 (포트 사용 중?) : {e.Message}", this);
        }
    }

    // [참가자] 소켓을 못 열면 목록만 비어 있고, IP 직접 입력은 그대로 가능
    public bool TryStartDiscovery()
    {
        try
        {
            StartDiscovery();
            return true;
        }
        catch (SocketException e)
        {
            Debug.LogWarning($"[LanDiscovery] 방 찾기 시작 실패: {e.Message}", this);
            return false;
        }
    }

    protected override ServerRequest GetRequest() => new ServerRequest();

    // [호스트] 찾기 요청에 현재 방 정보로 응답
    protected override LanRoomResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)
    {
        var manager = RoomManager.singleton as RoomManager;
        // LAN은 같은 공간에서 테스트하니 PC 이름으로 어느 방인지 구분
        string owner = SystemInfo.deviceName;

        return new LanRoomResponse
        {
            serverId = ServerId,
            roomName = string.IsNullOrWhiteSpace(manager.RoomName) ? $"{owner}'s room" : manager.RoomName,
            owner = owner,
            players = NetworkServer.connections.Count,
            maxPlayers = manager.maxConnections,
            ghostCount = manager.SelectedGhostCount,
            inGame = Mirror.Utils.IsSceneActive(manager.GameplayScene)
        };
    }

    // [참가자] 응답 → RoomInfo. 주소는 호스트가 적어 보낸 값 대신 실제로 응답이 온 IP를 사용
    protected override void ProcessResponse(LanRoomResponse response, IPEndPoint endpoint)
    {
        bool isFull = response.maxPlayers > 0 && response.players >= response.maxPlayers;
        bool isWaiting = !response.inGame;

        var room = new RoomInfo(
            response.roomName,
            response.owner,
            endpoint.Address.ToString(),
            response.players,
            response.maxPlayers,
            response.ghostCount,
            isWaiting,
            false,                   // LAN 방은 비밀번호 없음
            isWaiting && !isFull);

        RoomFound?.Invoke(room, response.serverId);
    }
}
