using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomUI : MonoBehaviour
{
    [Header("선택 표시 색")]
    [SerializeField] private Color _selectedColor = new Color(0.45f, 0.30f, 0.18f, 0.8f);
    [SerializeField] private Color _unselectedColor = new Color(1f, 1f, 1f, 0f);

    [SerializeField]
    private List<Button> ghostCountButtons;

    [SerializeField]
    private List<Button> maxPlayerCountButtons;

    [SerializeField] private bool _allowSoloInEditor = false;

    private CreateGameRoomData roomData;
    private const int MinPlayersToStart = 2;

    private readonly List<Color> _maxPlayerTextColors = new List<Color>();


    void Start()
    {
        roomData = new CreateGameRoomData() { ghostCount = 4, maxPlayerCount = 4 };
        //UpdateCrewImage();

        foreach (Button button in maxPlayerCountButtons)
        {
            _maxPlayerTextColors.Add(button.GetComponentInChildren<TMP_Text>().color);
        }

        UpdateGhostCount(roomData.ghostCount);
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

    public void CreateRoom()
    {
        var manager = RoomManager.singleton as RoomManager;

        // 방 설정 작업 처리
        manager.maxConnections = roomData.maxPlayerCount;
        int minPlayers = Mathf.Min(MinPlayersToStart, roomData.maxPlayerCount);

        if (Application.isEditor && _allowSoloInEditor) minPlayers = 1;

        manager.minPlayers = minPlayers;

        manager.SetGhostCount(roomData.ghostCount);

        manager.StartHost();
    }

    private void ApplySelection(List<Button> buttons, int selectedIndex)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].image.color = i == selectedIndex ? _selectedColor : _unselectedColor;
        }
    }
}

public class CreateGameRoomData
{
    public int ghostCount;
    public int maxPlayerCount;
}
