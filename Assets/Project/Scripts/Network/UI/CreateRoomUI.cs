using Mirror;
using System.Collections.Generic;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomUI : MonoBehaviour
{
    private const int MinPlayersToStart = 2;
    private const int MaxRoomNameLength = 20;
    private const int MaxPasswordLength = 16;

    [Header("선택 표시 색")]
    [SerializeField] private Color _selectedColor = new Color(0.45f, 0.30f, 0.18f, 0.8f);
    [SerializeField] private Color _unselectedColor = new Color(1f, 1f, 1f, 0f);

    [SerializeField] private List<Button> ghostCountButtons;

    [SerializeField] private List<Button> maxPlayerCountButtons;

    [Header("Steam 전용 (LAN 패널은 비워두기)")]
    [SerializeField] private TMP_InputField _roomNameInput;
    [SerializeField] private Toggle _passwordToggle;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private TMP_Text _messageText;


    [SerializeField] private bool _allowSoloInEditor = false;


    private CreateGameRoomData roomData;
    private readonly List<Color> _maxPlayerTextColors = new List<Color>();


    void Start()
    {
        roomData = new CreateGameRoomData() { ghostCount = 4, maxPlayerCount = 4 };
        //UpdateCrewImage();

        foreach (Button button in maxPlayerCountButtons)
        {
            _maxPlayerTextColors.Add(button.GetComponentInChildren<TMP_Text>().color);
        }

        if (_roomNameInput != null) _roomNameInput.characterLimit = MaxRoomNameLength;

        if (_passwordInput != null)
        {
            _passwordInput.characterLimit = MaxPasswordLength;
            _passwordInput.contentType = TMP_InputField.ContentType.Password; // **** 표시
        }

        if (_passwordToggle != null)
        {
            _passwordToggle.onValueChanged.AddListener(OnPasswordToggleChanged);
            OnPasswordToggleChanged(_passwordToggle.isOn);
        }

        UpdateGhostCount(roomData.ghostCount);
    }

    private void OnEnable()
    {
        ShowMessage(string.Empty);
    }

    //private void UpdateCrewImage()
    //{
    //    int imposterCount = roomData.imposterCount;
    //
    //    int idx = 0;
    //
    //    while(imposterCount != 0)
    //    {
    //        if (idx >= roomData.maxPlayerCount)
    //        {
    //            idx = 0;
    //        }
    //
    //        if (crewImages[idx].material.GetColor("_PlayerColor") != Color.red && Random.Range(0, 5) == 0)
    //        {
    //            crewImages[idx].material.SetColor("_PlayerColor", Color.red);
    //            imposterCount++;
    //        }
    //        idx++;
    //    }
    //
    //    for (int i =0; i < crewImages.Count; i++)
    //    {
    //        if (i < roomData.maxPlayerCount)
    //        {
    //            CrewImages[i].gameObject.SetActive(true);
    //        }
    //        else
    //        {
    //            CrewImages[i].gameObject.SetActive(false);
    //        }
    //    }
    //}

    public void UpdateGhostCount(int count)
    {
        roomData.ghostCount = count;

        ApplySelection(ghostCountButtons, count - 2);

        int limitMaxPlayer = count == 2 ? 2 : count == 3 ? 3 : 4;
        if (roomData.maxPlayerCount < limitMaxPlayer)
        {
            UpdateMaxPlayerCount(limitMaxPlayer);
        }
        else
        {
            UpdateMaxPlayerCount(roomData.maxPlayerCount);
        }

        for (int i = 0; i < maxPlayerCountButtons.Count; i++)
        {
            var text = maxPlayerCountButtons[i].GetComponentInChildren<TMP_Text>();
            Color baseColor = _maxPlayerTextColors[i];
            bool selectable = i >= limitMaxPlayer - 2;

            maxPlayerCountButtons[i].interactable = selectable;

            text.color = selectable ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
        }
    }

    public void UpdateMaxPlayerCount(int count)
    {
        roomData.maxPlayerCount = count;

        ApplySelection(maxPlayerCountButtons, count - 2);
    }

    public void OnPasswordToggleChanged(bool isOn)
    {
        if (_passwordInput == null) return;

        _passwordInput.interactable = isOn;
        if (!isOn) _passwordInput.text = string.Empty;
    }

    public void CreateRoom()
    {
        var manager = RoomManager.singleton as RoomManager;
        if (manager == null) return;

        if (!TryReadPassword(out string password)) return;

        // 방 설정 작업 처리
        manager.maxConnections = roomData.maxPlayerCount;
        int minPlayers = Mathf.Min(MinPlayersToStart, roomData.maxPlayerCount);

        if (Application.isEditor && _allowSoloInEditor) minPlayers = 1;

        manager.minPlayers = minPlayers;

        manager.SetGhostCount(roomData.ghostCount);

        // LAN 패널은 입력칸이 없기에 이전 Steam 방의 값이 남지않도록 항상 덮어씀
        manager.RoomName = _roomNameInput != null ? _roomNameInput.text.Trim() : string.Empty;
        manager.RoomPassword = password;

        manager.StartHost();
    }

    private bool TryReadPassword(out string password)
    {
        password = string.Empty;

        if (_passwordToggle == null || !_passwordToggle.isOn) return true;

        password = _passwordInput != null ? _passwordInput.text : string.Empty;
        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("비밀번호를 입력해주세요");
            _passwordInput?.ActivateInputField();
            return false;
        }

        return true;
    }


    private void ApplySelection(List<Button> buttons, int selectedIndex)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].image.color = i == selectedIndex ? _selectedColor : _unselectedColor;
        }
    }

    private void ShowMessage(string message)
    {
        if (_messageText != null) _messageText.text = message;
    }
}

public class CreateGameRoomData
{
    public int ghostCount;
    public int maxPlayerCount;
}
