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
    [SerializeField] private GameObject[] _closeOnInvite;  // 초대를 받게 되면 닫을 화면

    [Header("사운드")]
    [SerializeField] private AudioClip _bgm;

    private SoundManager _sound;

    private void Awake() => SteamLobbyService.InviteReady += OpenSteamScreenForInvite;
    private void OnDestroy()
    {
        SteamLobbyService.InviteReady -= OpenSteamScreenForInvite;

        // 메인메뉴를 떠날 때(로비 등) BGM 끄기
        if (_sound != null) _sound.StopBGM();
    }
    private void Start()
    {
        _noticePanel.SetActive(false);

        if (_bgm != null)
        {
            _sound = SoundManager.Instance;
            _sound.PlayBGM(_bgm);
        }

        DisconnectNotice notice = RoomManager.ConsumeNotice();
        if (notice == DisconnectNotice.HostLost)
        {
            StartCoroutine(ShowNoticeRoutine("Disconnected from the host."));
        }
        else if (notice == DisconnectNotice.LoadFailed)
        {
            StartCoroutine(ShowNoticeRoutine("Failed to get ready for the game in time."));
        }
    }

    private IEnumerator ShowNoticeRoutine(string message)
    {
        _noticeText.text = message;
        _noticePanel.SetActive(true);

        yield return new WaitForSeconds(_noticeDuration);

        _noticePanel.SetActive(false);
    }

    private void OpenSteamScreenForInvite()
    {
        if (_steamUI == null || _steamUI.activeSelf) return;
        if (!SelectedMode(RoomManager.NetworkMode.Steam)) return;

        if (_lanUI != null) _lanUI.SetActive(false);
        foreach (GameObject screen in _closeOnInvite)
        {
            if (screen != null) screen.SetActive(false);
        }

        OpenScreen(_steamUI);
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
