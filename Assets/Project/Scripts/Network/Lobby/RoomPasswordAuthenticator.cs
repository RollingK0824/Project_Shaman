using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.UI;


// 비밀번호 방 입장 확인
// 클라가 보낸 비밀번호를 서버가 RoomManager.RoomPassword와 비교

// 비밀번호가 없는 방은 항상 통과
// RoomManager와 같은 오브젝트에 붙이고, RoomManager의 Authenticator 칸에 연결

public class RoomPasswordAuthenticator : NetworkAuthenticator
{
    public struct AuthRequestMessage : NetworkMessage
    {
        public string Password;
    }

    public struct AuthResponseMessage : NetworkMessage
    {
        public bool Success;
        public string Reason;
    }

    [SerializeField] private float _rejectDelay = 0.5f;

    // 클라 - 참가 직전에 UI가 설정
    public static string PendingPassword { get; set; } = string.Empty;

    // 클라 - 마지막 거절 사유
    private static string _lastRejectReason;

    public static string ConsumeRejectReason()
    {
        string reason = _lastRejectReason;
        _lastRejectReason = null;

        return reason;
    }


    // 서버 부분
    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<AuthRequestMessage>();
    }

    public override void OnServerAuthenticate(NetworkConnectionToClient conn)
    {
    }

    private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        var manager = NetworkManager.singleton as RoomManager;
        if (manager == null) return;

        bool accepted = conn is LocalConnectionToClient || manager == null || !manager.HasPassword || msg.Password == manager.RoomPassword;

        if (accepted)
        {
            conn.Send(new AuthResponseMessage { Success = true });
            ServerAccept(conn);
            return;
        }


        Debug.Log($"[RoomPasswordAuthenticator] 비밀번호 불일치 -> 거절 connId = {conn.connectionId}");
        conn.Send(new AuthResponseMessage { Success = false, Reason = "Wrong password" });
        conn.isAuthenticated = false;
        StartCoroutine(RejectAfterDelay(conn));
    }

    private IEnumerator RejectAfterDelay(NetworkConnectionToClient conn)
    {
        yield return new WaitForSeconds(_rejectDelay);

        ServerReject(conn);
    }

    // 클라 부분
    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<AuthResponseMessage>();
    }

    public override void OnClientAuthenticate()
    {
        NetworkClient.Send(new AuthRequestMessage { Password = PendingPassword ?? string.Empty });
        PendingPassword = string.Empty;
    }

    private void OnAuthResponse(AuthResponseMessage msg)
    {
        if (msg.Success)
        {
            ClientAccept();
            return;
        }

        _lastRejectReason = msg.Reason;
        ClientReject();
    }

}
