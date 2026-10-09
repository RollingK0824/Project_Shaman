using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 방 목록 한 줄. 데이터 표시와 클릭 전달만 하고, 선택 관리는 SteamLobbyUI가 담당
public class RoomRowUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _ownerText;
    [SerializeField] private TMP_Text _playersText;
    [SerializeField] private TMP_Text _stateText;

    [Header("배경")]
    [SerializeField] private Sprite _normalSprite;      // SteamBrowser_RoomRow_Normal
    [SerializeField] private Sprite _selectedSprite;    // SteamBrowser_RoomRow_Selected (붉은 붓 터치)

    [Header("글자색")]
    [SerializeField] private Color _normalTextColor = new Color(0.15f, 0.12f, 0.10f, 1f);   // 먹색
    [SerializeField] private Color _selectedTextColor = Color.white;

    [Header("좌물쇠")]
    [SerializeField] private Image _lockIcon;
    [SerializeField] private Sprite _lockedSprite;
    [SerializeField] private Sprite _unlockedSprite;


    public RoomInfo Room { get; private set; }

    public void Bind(RoomInfo room, Action<RoomRowUI> onClick)
    {
        Room = room;

        _nameText.text = room.Name;
        _ownerText.text = room.Owner;
        _playersText.text = $"{room.CurrentPlayers} / {room.MaxPlayers}";
        _stateText.text = GetStateLabel(room);

        if (_lockIcon != null)
        {
            _lockIcon.sprite = room.IsLocked ? _lockedSprite : _unlockedSprite;
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick?.Invoke(this));

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        Sprite sprite = selected ? _selectedSprite : _normalSprite;
        if (sprite != null) _background.sprite = sprite;

        Color textColor = selected ? _selectedTextColor : _normalTextColor;
        _nameText.color = textColor;
        _ownerText.color = textColor;
        _playersText.color = textColor;
        _stateText.color = textColor;
    }

    // 상태 칸 문구. 오른쪽 정보 패널에서도 같은 문구를 쓰도록 static으로 공유
    public static string GetStateLabel(RoomInfo room)
    {
        if (!room.IsWaiting) return "게임 중";
        if (room.IsFull) return "가득 참";
        return "대기 중";
    }
}