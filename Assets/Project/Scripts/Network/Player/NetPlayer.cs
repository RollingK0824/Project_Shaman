using Mirror;
using UnityEngine;

/// <summary>Enables local input and presentation only for this connection's player.</summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetPlayer : NetworkBehaviour
{
    [SerializeField] private Behaviour[] _localOnlyComponents;
    private Camera _playerCamera;
    private AudioListener _audioListener;

    private PlayerInputReader _input;

    private void Awake()
    {
        // 꺼져 있는 컴포넌트도 찾도록 true 전달 (프리팹에서 기본값을 꺼 둬도 동작).
        _playerCamera = GetComponentInChildren<Camera>(true);
        _audioListener = GetComponentInChildren<AudioListener>(true);
        _input = GetComponent<PlayerInputReader>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetLocalComponents(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetLocalComponents(isLocalPlayer);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetLocalComponents(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 로딩 중이면 시작 시각까지 입력을 끔. 이미 시작했거나 테스트 씬이면 바로 허용
        // (게이트는 클라에서 NetPlayer보다 늦게 생성될 수 있으므로 RoomManager 상태를 기준으로 판단)
        if (NetworkManager.singleton is RoomManager roomManager && roomManager.IsLoadingGame)
        {
            SetInputEnabled(false);
            RoomManager.ClientGameStarted += HandleGameStarted;
        }

        CmdReportGameReady();
    }

    public override void OnStopLocalPlayer()
    {
        RoomManager.ClientGameStarted -= HandleGameStarted;

        SetLocalComponents(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        base.OnStopLocalPlayer();
    }

    public override void OnStopClient()
    {
        SetLocalComponents(false);
        base.OnStopClient();
    }

    private void SetLocalComponents(bool active)
    {
        foreach (Behaviour component in _localOnlyComponents ?? System.Array.Empty<Behaviour>())
        {
            if (component != null && component != this)
                component.enabled = active;
        }
        if (_playerCamera != null) _playerCamera.enabled = active;
        if (_audioListener != null) _audioListener.enabled = active;
    }

    // [로컬 → 서버] 이 클라에서 플레이어 사용 준비 완료
    [Command]
    private void CmdReportGameReady()
    {
        if (NetGameStartGate.Instance != null)
        {
            NetGameStartGate.Instance.ServerMarkReady(connectionToClient);
        }
    }

    private void HandleGameStarted()
    {
        RoomManager.ClientGameStarted -= HandleGameStarted;
        SetInputEnabled(true);
    }

    // SetGameplayInputBlocked는 UIManager 등이 메뉴 열고 닫을 때 덮어쓰므로, 로딩 잠금은 입력 컴포넌트 자체를 끔
    private void SetInputEnabled(bool enabled)
    {
        if (_input != null) _input.enabled = enabled;
    }
}
