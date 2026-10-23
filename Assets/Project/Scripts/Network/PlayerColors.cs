using UnityEngine;

// 캐릭터 번호 (RoomPlayer.characterId 0~3) = 플레이어 색
// 로비, 투표 등 플레이어를 구분하는 표시는 모두 여기 색 사용
public static class PlayerColors
{
    private static readonly Color[] Colors =
    {
        new Color(0.80f, 0.15f, 0.15f),   // 1번 빨강
        new Color(0.85f, 0.58f, 0.05f),   // 2번 황금 (노랑은 배경에 묻힘)
        new Color(0.18f, 0.55f, 0.20f),   // 3번 초록
        new Color(0.10f, 0.40f, 0.80f),   // 4번 파랑
    };

    public static readonly Color Unknown = Color.gray;

    public static int Count => Colors.Length;   // 추가

    public static Color Get(int characterId) => characterId >= 0 && characterId < Colors.Length ? Colors[characterId] : Unknown;
}
