using UnityEngine;
using Mirror;
using ProjectShaman.AI.World;

// 결계 부적 우클릭 (주민 집에 붙이기) 온라인 연결
// [로컬 클라] PlayerHouseBarrierPlacement.PlacementRequested → 아이템 번호 + 조준값만 서버로
//            NetPlayerItemUse.CmdUseItem으로 보내면 자기 보호막이 켜지므로 별도 Command 사용
// [서버]     검증 → TryPlaceAuthoritative(장착·소모·생성·Arm 한 번에) → 성공 시 NetHouseBarriers에 공개 + 사용 보고 1회
[RequireComponent(typeof(NetworkIdentity), typeof(NetPlayerEquipment), typeof(NetPlayerInventory))]
public class NetPlayerHouseBarrier : NetworkBehaviour
{
    [SerializeField] private NetItemDataBase _dataBase;
    [SerializeField] private float _maxAimOriginDistance = 3f;     // NetPlayerItemUse와 같은 기준

    private PlayerHouseBarrierPlacement _placement;
    private PlayerInventory _inventory;
    private NetPlayerInventory _netInventory;
    private NetPlayerEquipment _equipment;
    private NetPlayerStatus _status;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _netInventory = GetComponent<NetPlayerInventory>();
        _equipment = GetComponent<NetPlayerEquipment>();
        _status = GetComponent<NetPlayerStatus>();
    }

    // PlayerItemController.Awake가 자동으로 붙이는데 Awake 순서는 보장되지 않으므로 쓸 때 찾음
    private PlayerHouseBarrierPlacement Placement
    {
        get
        {
            if (_placement == null) _placement = GetComponent<PlayerHouseBarrierPlacement>();
            if (_placement == null) _placement = gameObject.AddComponent<PlayerHouseBarrierPlacement>();
            return _placement;
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Placement.PlacementRequested += HandlePlacementRequested;
    }

    public override void OnStopLocalPlayer()
    {
        if (_placement != null) _placement.PlacementRequested -= HandlePlacementRequested;
        base.OnStopLocalPlayer();
    }

    // [로컬 클라] 플레이어 쪽이 집 판정까지 통과한 요청만 들어옴
    private void HandlePlacementRequested(ItemUseContext context)
    {
        if (context.Owner != gameObject) return;

        int itemId = _dataBase.GetId(context.Data);
        if (itemId < 0)
        {
            Feedback("붙일 수 없는 부적입니다.");
            return;
        }

        // 클라에는 진짜 ward가 없어 중복 확인이 통과됨 → 공개된 목록으로 대신 막음
        NetHouseBarriers manager = NetHouseBarriers.Instance;
        if (manager != null && Placement.TryFindHome(context, out PlaceArea home, out _, out _) &&
            manager.IsHomeProtected(home.PlaceId))
        {
            Feedback("이미 결계로 보호 중인 집입니다");
            return;
        }

        CmdRequestPlace(itemId, context.AimOrigin, context.AimDirection);
    }

    [Command]
    private void CmdRequestPlace(int itemId, Vector3 aimOrigin, Vector3 aimDirection)
    {
        ItemData data = _dataBase.GetItem(itemId);
        var context = new ItemUseContext(gameObject, data, aimOrigin, aimDirection);

        string reject = ServerValidate(data, context);
        if (reject != null)
        {
            TargetFailed(reject);
            return;
        }

        NetHouseBarriers manager = NetHouseBarriers.Instance;
        bool success = Placement.TryPlaceAuthoritative(context,
            () => _equipment.EquippedItem == data,          // 서버가 확정한 장착 기준
            () => _netInventory.ServerConsume(data, 1),     // 소유 클라 소지품도 같이 줄어듦
            ward => true,                                   // 아직 Arm 전 → 공개는 성공한 뒤에
            manager.ServerDespawn,                          // 롤백·만료 모두 여기로
            out HouseBarrierWard placed);

        if (!success)
        {
            TargetFailed("지금은 결계를 붙일 수 없습니다.");
            return;
        }

        manager.ServerPublish(placed, itemId);

        // 성공 보고는 서버에서 한 번만 (PlacementConfirmed에서 또 하지 않음)
        PlayerItemActions.ReportUse(gameObject, data);
        QuestTargets.NotifyItemUsed(gameObject, data, aimOrigin, aimDirection);
    }

    [Server]
    private string ServerValidate(ItemData data, in ItemUseContext context)
    {
        if (_status != null && _status.IsDead) return "지금은 붙일 수 없습니다.";

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing || !NetGameStartGate.HasStarted) return "지금은 붙일 수 없습니다.";
        if (NetHouseBarriers.Instance == null) return "집 결계를 사용할 수 없습니다.";

        if (!(data is TalismanItemData talisman) || talisman.TalismanType != TalismanType.Barrier) return "결계 부적이 아닙니다.";
        if (_equipment.EquippedItem != data) return "결계 부적을 들고 있어야 합니다.";
        if (!_inventory.Contains(data)) return "결계 부적이 없습니다.";

        // 조준값 검증: 비정상 값, 몸에서 너무 먼 카메라 위치
        if (!IsFinite(context.AimOrigin) || !IsFinite(context.AimDirection)) return "잘못된 요청입니다.";
        if (Vector3.Distance(transform.position, context.AimOrigin) > _maxAimOriginDistance) return "잘못된 요청입니다.";

        // 집 판정은 서버에서 플레이어 코드로 다시 (이미 보호 중이면 그 사유가 그대로 나옴)
        return Placement.TryFindHome(context, out _, out _, out string reason) ? null : reason;
    }

    [TargetRpc]
    private void TargetFailed(string message) => Feedback(message);

    private void Feedback(string message)
    {
        if (TryGetComponent(out PlayerItemFeedback feedback)) feedback.Unavailable(message);
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
}