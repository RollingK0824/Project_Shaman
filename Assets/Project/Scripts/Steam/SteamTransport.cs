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
            if (_clientConnected) ClientDisconnect();

            if (_serverStarted) ServerStop();
        }

        private void OnDestroy()
        {
            _connectionStatusChanged?.Dispose();
        }

        private void SendInternal(HSteamNetConnection conn, ArraySegment<byte> segment, int channelId)
        {
            int sendFlags = channelId == Channels.Reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

            // unsafe 포인터 대신 AllocHGlobal 사용 - "Allow unsafe code" 프로젝트 설정 안해도됨..
            IntPtr dataPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(segment.Count);
            System.Runtime.InteropServices.Marshal.Copy(segment.Array, segment.Offset, dataPtr, segment.Count);

            long msgNumOut;
            EResult res = SteamNetworkingSockets.SendMessageToConnection(conn, dataPtr, (uint)segment.Count, sendFlags, out msgNumOut);

            System.Runtime.InteropServices.Marshal.FreeHGlobal(dataPtr);

            if (res != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[SteamTransport] SendMessageToConnection 실패: {res}");
            }
        }

        // ------------ 서버 ------------
        public override bool ServerActive() => _serverStarted;

        public override void ServerStart()
        {
            /** CreateListenSocketP2P(...)
             * 클라이언트의 P2P 접속 요청을 대기하는 소캣을 생성합니다.
             * _virtualPort : 가상 포트 번호, 한 Stema ID 내에서 여러 네트워크 서비르를 구분할 때 사용한다. (보통 0 사용)
             * 0, new SteamNetworkingConfigValue_t[0] : 추가 소켓 옵션/설정을 전달하는 파라미터, 기본값으로 안전하게 초기화
             * 
             ** _listenSocket.m_HSteamListenSocket
             * 생성된 소캣의 고유 핸들(ID)값, 핸들 값이 정상적으로 출력되면 소켓이 바르게 생성된 것
             */
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(_virtualPort, 0, new SteamNetworkingConfigValue_t[0]);

            // 소켓 유효성 검사
            if (_listenSocket.m_HSteamListenSocket == 0)
            {
                Debug.LogError("[SteamTransport] ServerStart 실패 - ListenSocket을 생성할 수 없습니다.");
                return;
            }

            _serverStarted = true;
            Debug.Log($"[SteamTransport] ServerStart - listenSocket handle = {_listenSocket.m_HSteamListenSocket}");
        }

        public override void ServerStop()
        {
            if (!_serverStarted) return;

            /** CloseConnection (접속자 정리)
             * 현재 접속해 있는 모든 클라이언트와의 연결 핸들 (conn)을 순회하며 안전하게 종료한다.
             * 0 : 종류 사유 코드 (Reason Code)
             * "server stopped" : 클라이언트에 전달될 텍스트 사유
             * false : 즉시 강제 종료방식 (true로 설정해서 미전송 패킷을 전송 후 닫도록 할 수 있음 -> 메시지 패킷이 확실히 전달되기를 원한다면 true값으로)
             * 
             ** CloseListenSocket (자원 해제)
             * 소켓 관리를 완전히 종료하고, 수신 대기 상태를 해제한다.
             */

            foreach(var conn in _serverConnections.Values)
            {
                SteamNetworkingSockets.CloseConnection(conn, 0, "server stopped", false);
            }

            _serverConnections.Clear();
            SteamNetworkingSockets.CloseListenSocket(_listenSocket);
            _listenSocket = HSteamListenSocket.Invalid; // 핸들 초기화
            _serverStarted = false;
            Debug.Log("[SteamTransport] ServerStop");
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (!_serverConnections.TryGetValue(connectionId, out var conn)) return;
            SendInternal(conn, segment, channelId);
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
            if (!ulong.TryParse(address, out ulong steamId64))
            {
                Debug.LogError($"[SteamTransport] 잘못된 SteamID 형식: {address}");
                OnClientError?.Invoke(TransportError.DnsResolve, "Invalied SteamID format");
                return;
            }


            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(steamId64));

            _clientConnection = SteamNetworkingSockets.ConnectP2P(ref identity, _virtualPort, 0,
                new SteamNetworkingConfigValue_t[0]);
            Debug.Log($"[SteamTransport] ClientConnect 시도 - target={steamId64}, connHandle = " +
                $"{_clientConnection.m_HSteamNetConnection}");


        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId = 0)
        {
            SendInternal(_clientConnection, segment, channelId);
        }

        public override void ClientDisconnect()
        {
            SteamNetworkingSockets.CloseConnection(_clientConnection, 0, "client disconnect", false);
            _clientConnected = false;
        }

        /*****************************************************************
         ********************  Steam 콜백 & 수신 폴링  ********************
         *****************************************************************/

        // 서버 및 클라이언트 양쪽에서 연결 요청, 완료, 해제 이벤트를 처리하는 핵심 상태 분기 로직
        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            // 콜백 데이터에서 현재 연결의 변화된 상태(state)와 소켓 연결 핸들(conn) 추출
            var state = data.m_info.m_eState;
            var conn = data.m_hConn;

            switch (state)
            {
                // 1. Connecting : 새 클라가 서버로 접속을 시도하는 시점
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (_serverStarted && data.m_info.m_hListenSocket.m_HSteamListenSocket == 
                        _listenSocket.m_HSteamListenSocket)
                    {
                        EResult res = SteamNetworkingSockets.AcceptConnection(conn);
                        if (res != EResult.k_EResultOK)
                        {
                            Debug.LogWarning($"[SteamTransport] AcceptConnection 실패: {res}");
                            SteamNetworkingSockets.CloseConnection(conn, 0, "accept failed", false);
                        }
                    }

                    // 클라쪽은 Connecting 상태를 그냥 지나감 (아직은 따로 할일 x)
                    break;

                // 2. Connected : P2P 연결 수락및 네트워크 세션이 완전히 확정된 시점
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    // [서버 관점] 내 서버 소켓으로 접속 완료된 클라인 경우
                    if (_serverStarted && data.m_info.m_hListenSocket.m_HSteamListenSocket == 
                        _listenSocket.m_HSteamListenSocket)
                    {
                        // 트랜스포트 내부에서 사용할 고유 커넥션 ID를 순차적으로 부여
                        int connId = _nextConnectionId++;

                        // ID와 Steam 커넥션 핸들을 딕셔너리에 매핑하여 저장
                        _serverConnections[connId] = conn;
                        Debug.Log($"[SteamTransport] OnServerConnected connId = {connId}");
                        
                        // Mirror 등 상위 네트워크 매니저에게 클라 접속 완료 이벤트 전달
                        OnServerConnected?.Invoke(connId);
                    }
                    // [클라 관점] 내 클라 핸들로 원격 서버 접속이 완료된 경우
                    else if (conn.m_HSteamNetConnection == _clientConnection.m_HSteamNetConnection)
                    {
                        // 클라 접속 완료 플래그를 true 전환
                        _clientConnected = true;
                        Debug.Log("[SteamTransport] OnClinetConnected");

                        // 상위 매니저에게 서버 접속 완료 이벤트 전달
                        OnClientConnected?.Invoke();
                    }

                    break;

                // 3. ClosedByPeer/ProblemDetectedLocally : 연결 해제 또는 네트워크 에러 발생
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    int? foundId = null;

                    // [서버 1단계] 끊긴 커넥션 핸들(conn)이 서버에 등록된 클라 목록에 있는지 순회하면서 찾기
                    foreach(var kv in _serverConnections)
                    {
                        if (kv.Value.m_HSteamNetConnection == conn.m_HSteamNetConnection)
                        {
                            foundId = kv.Key; break;
                        }
                    }

                    // [서버 2단계] 해당 클라를 서버 목록에서 제거하고, 이탈 이벤트를 발생
                    if (foundId.HasValue)
                    {
                        // 커넥션 관리 딕셔너리에서 해당 ID 제거
                        _serverConnections.Remove(foundId.Value);

                        // Steam 네트워크 자원 내부에서 해당 연결 핸들을 닫아 메모리 해제
                        SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                        Debug.Log($"[SteamTransport] OnServerDisconnected connId = {foundId.Value}");

                        // Mirror 상위 매니저에 클라 연결 끊김 이벤트 전달
                        OnServerDisconnected?.Invoke(foundId.Value);
                    }
                    // [클라 처리] 내가 접속했던 서버와 연결이 끊긴 경우
                    else if (conn.m_HSteamNetConnection == _clientConnection.m_HSteamNetConnection)
                    {
                        // 내 클라 소캣 핸들 자원을 정리
                        SteamNetworkingSockets.CloseConnection(conn, 0, null, false);

                        // 클라 접속 플래그 해제
                        _clientConnected = false;
                        Debug.Log("[SteamTransport] OnClinetDisconnected");

                        // 상위 매니저에 서버와 연결 해제됨을 알림
                        OnClientDisconnected?.Invoke();
                    }

                    break;
            }

        }


        private const int _maxMessagesPerPoll = 32;
        private readonly IntPtr[] _messageBuffer = new IntPtr[_maxMessagesPerPoll];

        private void LateUpdate()
        {
            if (_serverStarted)
            {
                foreach(var kv in _serverConnections)
                {
                    PollConnection(kv.Value, kv.Key, isServer: true);
                }
            }

            if (_clientConnected)
            {
                PollConnection(_clientConnection, -1, isServer: false);
            }
        }

        private void PollConnection(HSteamNetConnection conn, int connId, bool isServer)
        {
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, _messageBuffer, _maxMessagesPerPoll);

            for (int i = 0; i < count; i++)
            {
                var msg = System.Runtime.InteropServices.Marshal.PtrToStructure<SteamNetworkingMessage_t>(_messageBuffer[i]);

                byte[] data = new byte[msg.m_cbSize];
                System.Runtime.InteropServices.Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                var segment = new ArraySegment<byte>(data);

                // 현재는 Channels.Reliable로 뭉처서 보고
                // Steam 쪽 메시지 차제에 Reliable/Unreliable 구분이 따로 안 붙어 있어서, 정확한 구분은 송신에서 플래그를 어떻게 붙이느냐에 따라 달려있음
                if (isServer)
                {
                    OnServerDataReceived?.Invoke(connId, segment, Channels.Reliable);
                }
                else
                {
                    OnClientDataReceived?.Invoke(segment, Channels.Reliable);
                }

                SteamNetworkingMessage_t.Release(_messageBuffer[i]); // 필수 (안하게 되면 네이티브 메모리 누수)
            }
        }
    }
}
