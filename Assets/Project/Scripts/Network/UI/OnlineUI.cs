using System.Globalization;
using Mirror;
using Steamworks;
using ProjectShaman.Steam;
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

    [SerializeField] private TMP_InputField _hostSteamIdInputField;
    private Coroutine _joinFailedCoroutine;
    private TMP_Text _joinFailedText;
    private string _defaultJoinFailedMessage;

    private void Awake()
    {
        _joinFailedText = _joinFailedPanel.GetComponentInChildren<TMP_Text>(true);
        if (_joinFailedText != null)
            _defaultJoinFailedMessage = _joinFailedText.text;
        _joinFailedPanel.SetActive(false);

        RoomManager.JoinFailed += ShowJoinFailed;
    }

    public void Start()
    {
        _joinFailedPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        RoomManager.JoinFailed -= ShowJoinFailed;
    }

    public void ShowJoinFailed() => ShowJoinMessage(_defaultJoinFailedMessage);

    private void ShowJoinMessage(string message)
    {
        if (_joinFailedText != null) _joinFailedText.text = message;
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
        if (NetworkClient.active || NetworkServer.active) return;

        var manager = RoomManager.singleton as RoomManager;
        if (manager == null)
        {
            ShowJoinMessage("Room manager is not ready.");
            return;
        }

        string nickname = _nicknameInputField.text.Trim();
        if (string.IsNullOrWhiteSpace(nickname))
        {
            ShowJoinMessage("Enter your nickname.");
            return;
        }

        string input = _hostSteamIdInputField != null ?
            _hostSteamIdInputField.text.Trim() : string.Empty;

        string address;
        if ( manager.IsUsingSteam)
        {
            if (!TryGetSteamHostAddress(manager, input, out address)) return;
        }
        else
        {
            address = string.IsNullOrEmpty(input) ? "localhost" : input;
        }


        if (_joinFailedCoroutine != null) StopCoroutine(_joinFailedCoroutine);
        _joinFailedPanel.SetActive(false);
        UserData.Nickname = nickname;
        manager.networkAddress = address;
        manager.BeginJoinAttempt();
        manager.StartClient();
    }

    private bool TryGetSteamHostAddress(RoomManager manager, string input, out string address)
    {
        address = null;

        if (!ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
            || !new CSteamID(value).IsValid() || !new CSteamID(value).BIndividualAccount())
        {
            ShowJoinMessage("Enter a valid host SteamID64.");
            _hostSteamIdInputField?.ActivateInputField();
            return false;
        }

        if (!manager.transport.Available())
        {
            ShowJoinMessage("Sign in to Steam and restart the game.");
            return false;
        }

        if (value == SteamUser.GetSteamID().m_SteamID)
        {
            ShowJoinMessage("Enter the host's SteamID64, not your own.");
            return false;
        }

        address = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
