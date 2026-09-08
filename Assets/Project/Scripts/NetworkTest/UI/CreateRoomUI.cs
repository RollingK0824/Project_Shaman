using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomUI : MonoBehaviour
{
    [SerializeField]
    private List<Button> ghostCountButtons;

    [SerializeField]
    private List<Button> maxPlayerCountButtons;

    private CreateGameRoomData roomData;



    void Start()
    {


        roomData = new CreateGameRoomData() { ghostCount = 4, maxPlayerCount = 10 };
        //UpdateCrewImage();
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

        for (int i = 0; i < ghostCountButtons.Count; i++)
        {
            if (i == count - 2)
            {
                ghostCountButtons[i].image.color = new Color(1f, 1f, 1f, 0.4f);
            }
            else
            {
                ghostCountButtons[i].image.color = new Color(1f, 1f, 1f, 0f);
            }
        }

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
            if (i < limitMaxPlayer - 2)
            {
                maxPlayerCountButtons[i].interactable = false;
                text.color = Color.gray;
            }
            else
            {
                maxPlayerCountButtons[i].interactable = true;
                text.color = Color.white;
            }
        }
    }

    public void UpdateMaxPlayerCount(int count)
    {
        roomData.maxPlayerCount = count;

        for (int i = 0; i < maxPlayerCountButtons.Count; i++)
        {
            if (i == count - 2)
            {
                maxPlayerCountButtons[i].image.color = new Color(1f, 1f, 1f, 0.4f);
            }
            else
            {
                maxPlayerCountButtons[i].image.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    public void CreateRoom()
    {
        var manager = RoomManager.singleton;

        // 방 설정 작업 처리
        //

        manager.StartHost();
    }
}

public class CreateGameRoomData
{
    public int ghostCount;
    public int maxPlayerCount;
}
