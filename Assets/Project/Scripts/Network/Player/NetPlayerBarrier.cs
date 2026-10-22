using UnityEngine;
using Mirror;

// 보호막 부적 (결계) 온라인 연결
// [서버]  NetPlayerItemUse가 검증을 마친 사용 요청으로 진짜 BarrierWard를 서버에서만 만든다
// [모든 클라] _isProtected를 보고 보호막 모습만 몸에 따라 그린다 (보호 효과 x)
// [호스트] 서버의 진짜 BarrierWard가 이미 보이기에 모습을 따로 만들지 않는다.

[RequireComponent(typeof(NetPlayerInventory), typeof (NetPlayerEquipment))]
public class NetPlayerBarrier : NetworkBehaviour
{
    [Tooltip("BarrierTalismanUseAction에 연결된 것과 같은 BarrierWard 프리팹 (클라 모습용)")]
    [SerializeField] private BarrierWard _wardPrefab;

    // [서버 -> 모든 클라] 보호막 활성 여부
    [SyncVar(hook = nameof(OnProtectedChanged))] private bool _isProtected;


    private NetPlayerInventory _netInventory;
    private NetPlayerEquipment _equipment;
    private CharacterController _capsule;

    private BarrierWard _serverWard;            // [서버] 진짜 보호막
    private BarrierWard _remoteWard;            // [클라] 모습만 (Arm 하지 않음)
    private BarrierWardVisual _remoteVisual;

    public bool IsProtected => _isProtected;

    private void Awake()
    {
        _netInventory = GetComponent<NetPlayerInventory>();
        _equipment = GetComponent<NetPlayerEquipment>();
        _capsule = GetComponent<CharacterController>();
    }

    // [서버] NetPlayerItemUse.CmdUseItem에서 보유·쿨다운·조준 검증 뒤 호출. true일 때만 ReportUse
    [Server]
    public bool ServerTryActivate(BarrierTalismanUseAction action, in ItemUseContext context)
    {
        ItemData data = context.Data;

        bool success = action.TryActivateAuthoritative(context,
            () => _equipment.EquippedItem == data,                 // 서버가 확정한 장착 아이템 기준
            () => _netInventory.ServerConsume(data, 1),            // 소유 클라 인벤토리도 같이 줄어듦
            ward => { _serverWard = ward; return true; },          // 서버에만 보관 (네트워크 스폰 없음)
            HandleServerWardDespawn, out _);

        if (success) _isProtected = true;
        return success;
    }

    // [서버] 만료(시간·사망·게임 종료) 또는 사용 실패 롤백 때 BarrierWard가 호출
    private void HandleServerWardDespawn(BarrierWard ward)
    {
        if (_serverWard == ward)
        {
            _serverWard = null;
            _isProtected = false;
        }

        if (ward != null) Destroy(ward.gameObject);
    }

    public override void OnStopServer()
    {
        if (_serverWard != null) _serverWard.Expire();
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_isProtected) ShowRemote(true);   // 처음 받은 값에는 hook이 불리지 않음
    }

    public override void OnStopClient()
    {
        ShowRemote(false);
        base.OnStopClient();
    }

    private void OnProtectedChanged(bool oldValue, bool newValue) => ShowRemote(newValue);

    private void ShowRemote(bool show)
    {
        if (isServer) return;   // 호스트는 서버의 진짜 보호막이 보임

        if (show)
        {
            if (_remoteWard != null || _wardPrefab == null) return;

            _remoteWard = Instantiate(_wardPrefab);
            _remoteVisual = _remoteWard.GetComponentInChildren<BarrierWardVisual>(true);
            FollowOwner();
            if (_remoteVisual != null) _remoteVisual.ApplyReplicatedState(_wardPrefab.Radius, true);
            return;
        }

        if (_remoteWard == null) return;

        // 모습은 부모에서 떨어져 나와 스스로 페이드 후 사라짐 (BarrierWardVisual.Dissolve)
        if (_remoteVisual != null) _remoteVisual.ApplyReplicatedState(_wardPrefab.Radius, false);
        Destroy(_remoteWard.gameObject);
        _remoteWard = null;
        _remoteVisual = null;
    }

    private void LateUpdate()
    {
        if (_remoteWard != null) FollowOwner();
    }

    // BarrierWard.FollowOwner와 같은 기준 (카메라가 아니라 몸통 중심)
    private void FollowOwner()
    {
        Vector3 center = _capsule != null
            ? _capsule.transform.TransformPoint(_capsule.center)
            : transform.position + Vector3.up * 1.1f;
        _remoteWard.transform.SetPositionAndRotation(center, Quaternion.identity);
    }
}
