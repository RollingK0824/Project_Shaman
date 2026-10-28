using UnityEngine;

// 인게임 이름 규칙: 한글 2~4자. 로비 입력 안내와 서버 검사에 같이 사용
public static class PlayerNameRule
{
    public const int MinLength = 2;
    public const int MaxLength = 4;
    public const string PrefsKey = "GameName";

    public static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < MinLength || name.Length > MaxLength) return false;

        foreach (char c in name)
        {
            if (c < '가' || c > '힣') return false; // 완성형 한글만 (자모 단독, 영어, 공백, 이모지 X)
        }

        return true;
    }
}
