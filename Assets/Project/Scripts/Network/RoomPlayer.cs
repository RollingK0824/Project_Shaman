using UnityEngine;
using Mirror;
using System;
using System.Linq;

public class RoomPlayer : NetworkRoomPlayer
{
    [SyncVar(hook = nameof(OnNicknameChanged))] public string nickname;
    // 인게임에서 쓰는 이름 (한글 2~4자). 서버가 검사한 값만 들어감
    [SyncVar(hook = nameof(OnGameNameChanged))] public string gameName;
    
    [SyncVar(hook = nameof(OnRoomMaxPlayersChanged))] public int roomMaxPlayers;
    public const int CHARACTER_COUNT = 4;
    // 로비엣 ㅓ고른 캐릭터 (0 ~ 3 == NetPlayer1 ~ 4 - 1) = 서버가 아직 배정 전
    [SyncVar(hook = nameof(OnCharacterChanged))] public int characterId = -1;
    [SyncVar(hook = nameof(OnColorChanged))] public int colorId = -1;

    public static event Action NotEnoughPlayers;
    public static event Action LobbyChanged;
    public bool HasGameName => PlayerNameRule.IsValid(gameName);

    public override void Start()
    {
        base.Start();   // 여기서 roomSlots에 등록됨

        // roomSlots 등록 뒤에 배정해야 같은 프레임에 들어온 사람끼리 색이 겹치지 않음
        // 로비 <-> 게임 씬을 오가도 RoomPlayer는 유지되므로 다시 배정되지 않음
        if (isServer) colorId = FindFreeColor();
    }
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

            string savedName = PlayerPrefs.GetString(PlayerNameRule.PrefsKey, "");
            if (PlayerNameRule.IsValid(savedName)) CmdSetGameName(savedName);
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

    private int FindFreeColor()
    {
        if (NetworkManager.singleton is not NetworkRoomManager room) return -1;

        for (int id = 0; id < PlayerColors.Count; id++)
        {
            if (!room.roomSlots.OfType<RoomPlayer>().Any(rp => rp != this && rp.colorId == id)) return id;
        }

        return -1;
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

        characterId = id;
    }
    private void OnColorChanged(int oldValue, int newValue)
    {
        LobbyChanged?.Invoke();
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

    // [서버] 준비 중엔 변경 불가, 규칙에 맞는 이름만 받음
    [Command]
    public void CmdSetGameName(string name)
    {
        if (readyToBegin) return;

        name = name != null ? name.Trim() : null;
        if (!PlayerNameRule.IsValid(name)) return;

        gameName = name;
    }

    private void OnGameNameChanged(string oldValue, string newValue)
    {
        LobbyChanged?.Invoke();
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
