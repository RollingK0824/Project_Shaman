using System.Collections;
using TMPro;
using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _noticePanel;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private float _noticeDuration = 3f;

    [Header("화면")]
    [SerializeField] private GameObject _lanUI;     // 기존 OnlineUI
    [SerializeField] private GameObject _steamUI;   // 새롭게 생성

    private void Start()
    {
        _noticePanel.SetActive(false);

        if (RoomManager.ConsumeNotice() == DisconnectNotice.HostLost)
        {
            StartCoroutine(ShowNoticeRoutine("Disconnected from the host."));
        }
    }

    private IEnumerator ShowNoticeRoutine(string message)
    {
        _noticeText.text = message;
        _noticePanel.SetActive(true);

        yield return new WaitForSeconds(_noticeDuration);

        _noticePanel.SetActive(false);
    }

    public void OnClickLanButton()
    {
        if (!SelectedMode(RoomManager.NetworkMode.Lan)) return;

        OpenScreen(_lanUI);
    }

    public void OnClickSteamButton()
    {
        var manager = RoomManager.singleton as RoomManager;
        if (manager != null && !manager.IsSteamAvailable)
        {
            StartCoroutine(ShowNoticeRoutine("Steam is not running. Start Steam and restart the game."));
            return;
        }

        if (!SelectedMode(RoomManager.NetworkMode.Steam)) return;

        OpenScreen(_steamUI);
    }

    private bool SelectedMode(RoomManager.NetworkMode mode)
    {
        var manager = RoomManager.singleton as RoomManager;
        return manager != null && manager.TrySelectMode(mode);
    }

    private void OpenScreen(GameObject screen)
    {
        if (screen == null) return;
        screen.SetActive(true);
        gameObject.SetActive(false);
    }



    public void OnClickOnlineButton()
    {
        Debug.Log("Click Online");
    }
    public void OnClickQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
