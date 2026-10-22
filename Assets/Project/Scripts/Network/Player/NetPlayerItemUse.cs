using Mirror;
using UnityEngine;
using System.Collections.Generic;

// [로컬 클라] - 아이템 사용 요청 (아이템ID + 조준값)을 서버로 전달
// [서버]      - 보유, 쿨다운, 조준값을 검증한 뒤 ItemUseAction.Execute로 판정
// [다른 클라] - 판정이 성공하면 RPC로 연출만 재생
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerInventory), typeof(NetPlayerInventory))]
[RequireComponent(typeof(ItemAttackAuthority))]
public class NetPlayerItemUse : NetworkBehaviour
{
    private const double ServerCooldownTolerance = 0.1;
    [SerializeField] private NetItemDataBase _dataBase;
    [SerializeField] private float _maxAimOriginDistance = 3f;  // 카메라 위치가 몸에서 이 거리보다 멀면 거부

    private PlayerInventory _inventory;
    private NetPlayerInventory _netInventory;
    private NetPlayerStatus _status;
    private NetPlayerEquipment _equipment;

    private ItemAttackAuthority _attackAuthority;

    private readonly Dictionary<int, double> _serverNextUseTime = new Dictionary<int, double>();

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _netInventory = GetComponent<NetPlayerInventory>();
        _status = GetComponent<NetPlayerStatus>();
        _attackAuthority = GetComponent<ItemAttackAuthority>();
        _equipment = GetComponent<NetPlayerEquipment>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_attackAuthority != null) _attackAuthority.SetAuthorityCheck(ServerCanResolveAttack);
    }

    public override void OnStopServer()
    {
        if (_attackAuthority != null) _attackAuthority.SetAuthorityCheck(null);
        base.OnStopServer();
    }

    // [서버] 공격이 대상에 닿을 때마다 호출됨. 쿨다운, 소모는 여기서 하지 않고 지금 상태만 확인
    private bool ServerCanResolveAttack()
    {
        if (!isServer) return false;
        if (_status != null && _status.IsDead) return false;

        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentGameState == GameState.Ongoing && NetGameStartGate.HasStarted;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        PlayerEvents.ItemUseRequested += HandleItemUseRequested;
    }

    public override void OnStopLocalPlayer()
    {
        PlayerEvents.ItemUseRequested -= HandleItemUseRequested;
        base.OnStopLocalPlayer();
    }

    private void HandleItemUseRequested(ItemUseContext context)
    {
        if (context.Owner != gameObject) return;
        if (!_inventory.Contains(context.Data)) return;

        int id = _dataBase.GetId(context.Data);
        if (id < 0)
        {
            Debug.LogError($"[NetPlayerItemUse] NetItemDataBase에 없는 아이템: {context.Data.DisplayName}", this);
            return;
        }

        CmdUseItem(id, context.AimOrigin, context.AimDirection);
    }

    [Command]
    private void CmdUseItem(int itemId, Vector3 aimOrigin, Vector3 aimDirection)
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (!ServerCanResolveAttack()) return;

        ItemData data = _dataBase.GetItem(itemId);
        if (data == null || data.UseAction == null) return;
        if (!_inventory.Contains(data)) return;

        // 조준값 검증: 비정상 값, 몸에서 너무 먼 카메라 위치(벽 너머 조준 조작 방지)
        if (!IsFinite(aimOrigin) || !IsFinite(aimDirection) || aimDirection.sqrMagnitude < 0.0001f) return;
        if (Vector3.Distance(transform.position, aimOrigin) > _maxAimOriginDistance) return;

        if (_serverNextUseTime.TryGetValue(itemId, out double nextTime) && NetworkTime.time < nextTime) return;

        // 소모는 반드시 NetPlayerInventory를 거쳐야 소유 클라 목록도 같이 줄어듦
        var context = new ItemUseContext(gameObject, data, aimOrigin, aimDirection,
            amount => _netInventory.ServerConsume(data, amount));

        if (!data.UseAction.Execute(context)) return;

        _serverNextUseTime[itemId] = NetworkTime.time + System.Math.Max(0.0, data.UseAction.Cooldown - ServerCooldownTolerance);
        Debug.Log($"[NetPlayerItemUse] 사용 판정 : {name} -> {data.DisplayName}", this);

        // 퀘스트 Use 보고는 판정이 확정된 서버에서 한 번만
        PlayerItemActions.ReportUse(gameObject, data);
        QuestTargets.NotifyItemUsed(gameObject, data, aimOrigin, aimDirection);

        RpcPlayUsePresentation(itemId, aimOrigin, aimDirection);
    }

    // [다른 클라, 호스트] 연출만 재생. 판정, 소모는 하지 않음
    [ClientRpc(includeOwner = false)]
    private void RpcPlayUsePresentation(int itemId, Vector3 aimOrigin, Vector3 aimDirection)
    {
        ItemData data = _dataBase.GetItem(itemId);
        if (data == null || data.UseAction == null) return;

        data.UseAction.PlayRemotePresentation(new ItemUseContext(gameObject, data, aimOrigin, aimDirection));

        // 3인칭 팔 모션 (방울 1, 초혼경 2, 소금·팥쑥 3, 징 4)
        if (_equipment != null) _equipment.PlayRemoteUseMotion(data);
    }

    private static bool IsFinite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

#if UNITY_EDITOR
    // [테스트 전용] PlayerItemController가 아직 버스로 요청을 안 보내므로, 로컬 클라에서 직접 요청을 보냄
    [ContextMenu("Test/Use First Usable Item (Local)")]
    private void TestUseFirstItem()
    {
        if (!isLocalPlayer)
        {
            Debug.LogWarning("[NetPlayerItemUse] 로컬 플레이어에서 실행해 주세요", this);
            return;
        }

        Camera cam = GetComponentInChildren<Camera>();
        foreach (ItemData item in _inventory.Items)
        {
            if (item == null || item.UseAction == null) continue;

            PlayerEvents.RaiseItemUseRequested(new ItemUseContext(gameObject, item, cam.transform.position, cam.transform.forward));
            return;
        }

        Debug.LogWarning("[NetPlayerItemUse] UseAction이 있는 아이템을 먼저 주워 주세요", this);
    }
#endif
}
