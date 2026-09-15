using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OnlineUI : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField _nicknameInputField;

    [SerializeField]
    private GameObject _createRoomUI;

    [SerializeField]
    private GameObject _joinFailedPanel;

    private Coroutine _joinFailedCoroutine;

    public void Start()
    {
        _joinFailedPanel.SetActive(false);
    }

    public void ShowJoinFailed()
    {
        _joinFailedPanel.SetActive(true);

        gameObject.SetActive(true);     // 만약 CreateRoomUI등으로 넘어가 있을 경우에 복귀시키는 코드

        if (_joinFailedCoroutine != null)
        {
            StopCoroutine(_joinFailedCoroutine);
        }
        _joinFailedCoroutine = StartCoroutine(HideJoinFailedAfterDelay(3f));
    }

    private IEnumerator HideJoinFailedAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        _joinFailedPanel.SetActive(false);
        _joinFailedCoroutine = null;
    }

    public void OnClickCreateRoomButton()
    {
        if (_nicknameInputField.text != "")
        {
            //PlayerPrefs.SetString("nickname", _nicknameInputField.text);
            UserData.Nickname = _nicknameInputField.text;
            _createRoomUI.SetActive(true);
            gameObject.SetActive(false);
            
        }
        else
        {
            //_nicknameInputField.GetComponent<Animator>().SetTrigger("On");
        }
    }

    public void OnClickEnterGameRoomButton()
    {
        UserData.Nickname = _nicknameInputField.text;
        var manager = RoomManager.singleton as RoomManager;
        manager.BeginJoinAttempt();


        manager.StartClient();
    }
}
