using UnityEngine;

public struct PlayerMoveProposal
{
    public uint sequence;       // 이동 요청 순번, 서버가 오래되거나 중복된 요청을 구분
    public uint revision;       // 서버 보정 전후 구분. 보정 전에 보낸 요청이 뒤늦게 적용되는 것을 방지
    public Vector3 position;    // 클라이언트가 먼저 이동한 뒤 서버에 제출하는 위치
    public float yaw;           // 캐릭터의 y축 회전 각도
}