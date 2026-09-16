using UnityEngine;
using System;
using System.Collections.Generic;
using Steamworks;
using Mirror;

namespace ProjectShaman.Steam
{
    public class SteamTransport : Transport
    {
        // 우리 서비스 내에서 쓸 가상 포트 번호
        private const int _virtualPort = 0;

        // 서버 측 : Mirror의 connectionId(int) <-> Steam의 HSteamNetConnection 매핑
        private readonly Dictionary<int, HSteamNetConnection> _serverConnections = new Dictionary<int, HSteamNetConnection>();
        private int _nextConnectionId = 1;

        // 서버가 리슨 중인 소켓
        private HSteamListenSocket _listenSocket;
        private bool _serverStarted;

        // 클라 측 : 내가 서버에 건 연결 하나
        private HSteamNetConnection _clientConnection;
        private bool _clientConnected;

        // Steam 콜백 (연결 상태 변화를 이걸로 받게 됨.. 비동기 이벤트)
        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChanged;

        private void Awake()
        {
            // 이 트랜스포트가 살아있는 동안 계속 콜백을 받도록 등록하기
            _connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
        }

        public override void ServerStop()
        {
            throw new NotImplementedException();
        }

        // ------------ 공통 ------------
        public override bool Available()
        {
            // Steam이 초기화되어 있어야 트랜스포트를 사용 할 수 있음
            return SteamManager.Initialized;
        }

        public override int GetMaxPacketSize(int channelId = Channels.Reliable)
        {
            // TODO: k_cbMaxSteamNetworkingSocketsMessageSizeSend 값으로 채우기
            return 512 * 1024;
        }

        public override void Shutdown()
        {
            Debug.Log("[SteamTransport] Shutdown");

            // TODO: 서버/클라 연결 전부 정리
        }

        // ------------ 서버 ------------
        public override bool ServerActive() => _serverStarted;

        public override void ServerStart()
        {
            Debug.Log("[SteamTransport] ServerStop (TODO)");
            // TODO: CloseListenSocket + 모든 클라이언트 연결 Close
            _serverStarted = false;
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            // TODO: _serverConnections[connectionId]로 SendMessageToConnection 호출
        }

        public override void ServerDisconnect(int connectionId)
        {
            // TODO: CloseConnection + 딕셔너리 제거
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            // TODO: 해당 connectionId의 SteamID 문자열 변환
            return "";
        }

        public override Uri ServerUri()
        {
            // TODO: steam://<steamID>형태로 반환 (선택사항, 당장 필수는 아님)
            return null;
        }


        // ------------ 클라 ------------
        public override bool ClientConnected() => _clientConnected;
        public override void ClientConnect(string address)
        {
            Debug.Log($"[SteamTransport] ClientConnect(address={address}) (TODO)");
            // TODO: address를 SteamID(ulong)로 파싱 -> SteamNetworkingIdentity 구성 -> ConnectP2P 호출

        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId = 0)
        {
            // TODO: _clientConnection으로 SendMessageToConnection 호출
        }

        public override void ClientDisconnect()
        {
            // TODO: CloseConnection(_clientConnected);
            _clientConnected = false;
        }

        // ------------ Steam 콜백 & 수신 폴링 ------------
        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            Debug.Log($"[SteamTransport] 연결 상태 변화: {data.m_info.m_eState}");
            // TODO: 여기서 신규 연결 수락(서버) / 연결 성공, 끊김 판정(클라) 분기 처리
        }

        private void LateUpdate()
        {
            // TODO: 서버면 각 연결에서 클라이언트면 내 연결에서 ReceiveMessageOnConnection 폴링
            //  + SteamNetworkingSockets.RunCallbacks() (또는 SteamAPI.RunCallbacks가 이미 처리)
        }
    }
}
