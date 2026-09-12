using UnityEngine;
using Mirror;
public class RoomPlayer : NetworkRoomPlayer
{
    [SyncVar] public string nickname;
    [SyncVar] public int roomMaxPlayers;
    public override void OnGUI()
    {
        
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();


        if (isLocalPlayer)
        {
            // 사용자가 입력해둔 커스텀 닉네임을 가져와 서버로 전송
            string myName = string.IsNullOrEmpty(UserData.Nickname)
                ? $"Player{index + 1}"
                : UserData.Nickname;

            CmdSetNickname(myName);
        }
        
    }

    [Command]
    void CmdSetNickname(string name)
    {
        nickname = name;
    }

    [ClientRpc]
    public void RpcShowNotEnoughPlayers()
    {
        // UI 켜기
        var lobbyUI = Object.FindFirstObjectByType<LobbyUI>();
        lobbyUI?.ShowNotification();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        var rm = NetworkManager.singleton as NetworkRoomManager;
        roomMaxPlayers = rm.maxConnections;
    }
}

public static class UserData
{
    public static string Nickname = "User";
}