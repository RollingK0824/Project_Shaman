using System.Collections;
using TMPro;
using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _noticePanel;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private float _noticeDuration = 3f;

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
