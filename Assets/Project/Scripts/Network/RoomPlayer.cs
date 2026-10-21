using UnityEngine;
using Mirror;
using System;
public class RoomPlayer : NetworkRoomPlayer
{
    [SyncVar(hook = nameof(OnNicknameChanged))] public string nickname;
    [SyncVar(hook = nameof(OnRoomMaxPlayersChanged))] public int roomMaxPlayers;
    public const int CHARACTER_COUNT = 4;
    // 로비엣 ㅓ고른 캐릭터 (0 ~ 3 == NetPlayer1 ~ 4 - 1) = 서버가 아직 배정 전
    [SyncVar(hook = nameof(OnCharacterChanged))] public int characterId = -1;

    public static event Action NotEnoughPlayers;
    public static event Action LobbyChanged;
    public override void OnStartServer()
    {
        base.OnStartServer();
        var rm = NetworkManager.singleton as NetworkRoomManager;
        roomMaxPlayers = rm.maxConnections;
        characterId = FindFreeCharacter();
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

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        LobbyChanged?.Invoke();
    }

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        LobbyChanged?.Invoke();
    }

    public override void OnClientExitRoom()
    {
        base.OnClientExitRoom();
        LobbyChanged?.Invoke();
    }

    public override void OnDisable()
    {
        // Mirror가 roomSlots에서 제거한 뒤 알립니다. 마지막 참가자 퇴장도 포함합니다.
        base.OnDisable();
        LobbyChanged?.Invoke();
    }

    public override void ReadyStateChanged(bool oldReadyState, bool newReadyState)
    {
        base.ReadyStateChanged(oldReadyState, newReadyState);
        LobbyChanged?.Invoke();
    }

    public override void IndexChanged(int oldIndex, int newIndex)
    {
        base.IndexChanged(oldIndex, newIndex);
        LobbyChanged?.Invoke();
    }

    private void OnNicknameChanged(string oldValue, string newValue)
    {
        LobbyChanged?.Invoke();
    }

    private void OnRoomMaxPlayersChanged(int oldValue, int newValue)
    {
        LobbyChanged?.Invoke();
    }

    public bool IsTakenByOther(int id)
    {
        if (NetworkManager.singleton is not NetworkRoomManager room) return false;

        foreach(var slot in room.roomSlots)
        {
            if (slot != this && slot is RoomPlayer rp && rp.characterId == id) return true;
        }

        return false;
    }

    private int FindFreeCharacter()
    {
        for (int id = 0; id< CHARACTER_COUNT; id++)
        {
            if (!IsTakenByOther(id)) return id;
        }

        return 0;
    }

    [Command]
    public void CmdSelectCharacter(int id)
    {
        if (id < 0 || id >= CHARACTER_COUNT) return;
        if (readyToBegin) return;
        if (IsTakenByOther(id)) return;

        characterId = id;
    }

    private void OnCharacterChanged(int oldValue, int newValue)
    {
        LobbyChanged?.Invoke();
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEventsOnPlay()
    {
        LobbyChanged = null;
        NotEnoughPlayers = null;
    }

    public override void OnGUI()
    {
        
    }

    


    [Command]
    void CmdSetNickname(string name)
    {
        nickname = name;
    }

    [ClientRpc]
    public void RpcShowNotEnoughPlayers()
    {
        NotEnoughPlayers?.Invoke();
    }

    
}

public static class UserData
{
    public static string Nickname = "User";
}
