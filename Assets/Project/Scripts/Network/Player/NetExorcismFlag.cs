using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkIdentity), typeof(ExorcismFlagInteractable))]
public class NetExorcismFlag : NetworkBehaviour
{
    [SerializeField] private float _maxActivateDistance = 4.5f;

    // [서버 -> 모든 클라] 깃발 상태 전달
    [SyncVar(hook = nameof(OnStateChanged))] private bool _ritualActive;
    [SyncVar(hook = nameof(OnStateChanged))] private bool _isActivated;

    private ExorcismFlagInteractable _flag;

    private void Awake()
    {
        _flag = GetComponent<ExorcismFlagInteractable>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        CopyFromFlag(_flag);
        _flag.StateChanged += CopyFromFlag;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        PlayerEvents.FlagActivateRequested += HandleActivateRequested;

        if (!isServer) _flag.ApplyState(_ritualActive, _isActivated);
    }

    public override void OnStopServer()
    {
        _flag.StateChanged -= CopyFromFlag;
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        PlayerEvents.FlagActivateRequested -= HandleActivateRequested;
        base.OnStopClient();
    }

    // [로컬 클라] 버스로 들어온 요청 중 이 깃발 + 로컬 플레이어 것만 서버로 전달
    private void HandleActivateRequested(GameObject interactor, ExorcismFlagInteractable flag)
    {
        if (flag != _flag) return;

        if (!interactor.TryGetComponent(out NetworkIdentity identity) || !identity.isLocalPlayer) return;

        CmdRequestActivate();
    }

    // [서버] 활성화 판정, 깃발은 플레이어 소유가 아니기에 requiresAuthority = false, 요청자는 sender로 확인
    [Command(requiresAuthority = false)]
    private void CmdRequestActivate(NetworkConnectionToClient sender = null)
    {
        GameObject player = sender != null && sender.identity != null ? sender.identity.gameObject : null;
        if (player == null) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return;
        if (Vector3.Distance(player.transform.position, transform.position) > _maxActivateDistance) return;

        // 생존 · 의식 중 · 미활성은 CanInteract가 확인. 성공하면 StateChanged → CopyFromFlag로 동기화
        if (_flag.TryActivate(player))
        {
            Debug.Log($"[NetExorcismFlag] 활성화 판정 : {name} <- {player.name}", this);
        }
    }

    // [서버] 깃발 원본 상태를 SyncVar로 복사
    private void CopyFromFlag(ExorcismFlagInteractable flag)
    {
        _ritualActive = flag.IsRitualActive;
        _isActivated = flag.IsActivated;
    }

    // [클라] 서버가 확정한 상태를 복사본에 반영, 호스트는 원본을 갖고 있으므로 건너뜀
    private void OnStateChanged(bool oldValue, bool newValue)
    {
        if (isServer) return;

        _flag.ApplyState(_ritualActive, _isActivated);
    } 
}
