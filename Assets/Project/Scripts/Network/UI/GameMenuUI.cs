using Mirror;
using UnityEngine;


// 게임 씬 ESC 메뉴. 계속하기 / 나가기.
// 호스트가 나가면 모든 플레이어의 게임이 끝나므로 확인창을 띄운다 (LobbyUI와 같은 규칙).
public class GameMenuUI : MonoBehaviour
{
    

    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _leaveConfirmPanel;

    private bool _lookBeforeMenu;

    // 메뉴가 열려 있는지 (로비 게시판이 메뉴 중에는 상호작용하지 않도록)
    public static bool IsOpen { get; private set; }

    private void Start()
    {
        _menuPanel.SetActive(false);
        _leaveConfirmPanel.SetActive(false);
    }

    private void Update()
    {
        //if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (!InputManager.Instance.Actions.UI.Cancel.WasPerformedThisFrame()) return;

        // 로딩 화면 뒤에서 메뉴가 열리지 않도록
        if (NetworkManager.singleton is RoomManager roomManager && roomManager.IsLoadingGame) return;

        // 로비: 게시판을 보고 있으면 ESC는 게시판에서 빠져나오기만 함
        if (LobbyBoardFocus.Current != null)
        {
            LobbyBoardFocus.Current.Unfocus();
            return;
        }

        // 설정 패널이 열려 있으면 ESC는 설정(키바인딩 → 설정 순)만 닫음
        if (SettingsPanelUI.Current != null)
        {
            SettingsPanelUI.Current.Back();
            return;
        }

        // 게임 중에는 관전상태에서만 열 수 있음 (닫기는 항상 허용)
        if (!_menuPanel.activeSelf && !CanOpenMenu()) return;

        SetMenuOpen(!_menuPanel.activeSelf);
    }

    // 메뉴를 열면 커서를 풀고 시점 회전만 멈춤. 닫으면 원래대로.
    private void SetMenuOpen(bool open)
    {
        if (_menuPanel.activeSelf == open) return;

        _menuPanel.SetActive(open);
        IsOpen = open;
        if (!open) _leaveConfirmPanel.SetActive(false);

        PlayerCameraController look = FindLocalLook();
        PlayerHealth health = look != null ? look.GetComponent<PlayerHealth>() : null;
        PlayerInputReader input = look != null ? look.GetComponent<PlayerInputReader>() : null;

        if (open)
        {
            if (look != null)
            {
                _lookBeforeMenu = look.CanLook;
                look.CanLook = false;
            }

            if (input != null) input.SetGameplayInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 메뉴를 연 사이에 죽었으면 시점 회전을 다시 켜지 않음
            if (look != null) look.CanLook = _lookBeforeMenu && (health == null || !health.IsDead);

            if (input != null) input.SetGameplayInputBlocked(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // 게임: 로컬 플레이어(NetPlayer) / 로비: 씬의 LobbyWalker (네트워크 오브젝트가 아님)
    private static PlayerCameraController FindLocalLook()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local != null && local.TryGetComponent(out PlayerCameraController look)) return look;

        LobbyWalker walker = FindFirstObjectByType<LobbyWalker>();
        return walker != null ? walker.GetComponent<PlayerCameraController>() : null;
    }

    // 게임 : 관전 중일 때만 메뉴 허옹 (살아 있을 땐 ESC가 수첩,UI 닫기 담당)
    // 로비 : 로컬 플레이어가 RoomPlayer라 관전 컨포넌트가 없음 --> 항상 허용
    private static bool CanOpenMenu()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local == null || !local.TryGetComponent(out PlayerSpectatorController spectator)) return true;

        return spectator.IsSpectating;
    }

    // 메뉴를 연 채로 씬이 바뀌어도 상태가 남지 않도록
    private void OnDestroy() => IsOpen = false;

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
