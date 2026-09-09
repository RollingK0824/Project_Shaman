using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OnlineUI : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField _nicknameInputField;

    [SerializeField]
    private GameObject _createRoomUI;


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
        var manager = RoomManager.singleton;
        manager.StartClient();
    }
}
