using UnityEngine;
using Mirror;

public class RoomManager : NetworkRoomManager
{
    public override void OnGUI()
    {
        
    }

    // 전원 Ready 누르면 Mirror가 자동으로 호출 -> 기본 구현이 이미 GameplayScene으로 전환해줌
    // "정확히 몇명이어야 시작" 같은 조건을 추가하고 싶을 때만 override를 사용하자
    public override void OnRoomServerPlayersReady()
    {
        base.OnRoomServerPlayersReady();
    }

    // 로비 -> 게임 전환 시 실제 GamePlayer 스폰하는 지점
    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        // 1. 스폰 위치 가져오기 (NetworkStartPosition이 없으면 Vector3.zero 사용)
        Transform startPos = GetStartPosition();
        Vector3 spawnPos = startPos != null ? startPos.position : Vector3.zero;
        Quaternion spawnRot = startPos != null ? startPos.rotation : Quaternion.identity;

        // 2. GamePlayer 생성
        GameObject gamePlayerObj = Instantiate(playerPrefab, spawnPos, spawnRot);

        // 3. 컴포넌트 가져오기 (gameObject가 아니라 gamePlayerObj에서 가져와야 함!)
        var roomPlayerComp = roomPlayer.GetComponent<RoomPlayer>();
        var gamePlayerComp = gamePlayerObj.GetComponent<GamePlayer>();

        // 4. 데이터 복사 (Null 안전성 확인)
        if (roomPlayerComp != null && gamePlayerComp != null)
        {
            gamePlayerComp.playerName = roomPlayerComp.nickname;
        }

        return gamePlayerObj;
    }
}
