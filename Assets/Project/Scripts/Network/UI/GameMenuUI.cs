using Mirror;
using UnityEngine;


// 게임 씬 ESC 메뉴. 계속하기 / 나가기.
// 호스트가 나가면 모든 플레이어의 게임이 끝나므로 확인창을 띄운다 (LobbyUI와 같은 규칙).
public class GameMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _leaveConfirmPanel;

    private bool _lookBeforeMenu;

    private void Start()
    {
        _menuPanel.SetActive(false);
        _leaveConfirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetMenuOpen(!_menuPanel.activeSelf);
        }
    }

    // 메뉴를 열면 커서를 풀고 시점 회전만 멈춤. 닫으면 원래대로.
    private void SetMenuOpen(bool open)
    {
        if (_menuPanel.activeSelf == open) return;

        _menuPanel.SetActive(open);
        if (!open) _leaveConfirmPanel.SetActive(false);

        GameObject player = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
        PlayerCameraController look = player != null ? player.GetComponent<PlayerCameraController>() : null;
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;

        if (open)
        {
            if (look != null)
            {
                _lookBeforeMenu = look.CanLook;
                look.CanLook = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 메뉴를 연 사이에 죽었으면 시점 회전을 다시 켜지 않음
            if (look != null) look.CanLook = _lookBeforeMenu && (health == null || !health.IsDead);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // [계속하기] 버튼
    public void OnClickResume()
    {
        SetMenuOpen(false);
    }

    // [나가기] 버튼. 호스트는 확인창, 클라는 바로 나감.
    public void OnClickLeave()
    {
        if (NetworkServer.active)
        {
            _leaveConfirmPanel.SetActive(true);
            return;
        }

        Leave();
    }

    // 호스트 확인창 [확인]
    public void OnConfirmLeave()
    {
        Leave();
    }

    // 호스트 확인창 [취소]
    public void OnCancelLeave()
    {
        _leaveConfirmPanel.SetActive(false);
    }

    private void Leave()
    {
        if (NetworkManager.singleton is RoomManager roomManager)
        {
            roomManager.LeaveSession();
        }
    }
}
