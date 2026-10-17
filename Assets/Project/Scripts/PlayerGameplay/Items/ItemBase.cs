using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    public ItemData Data { get; private set; }
    public GameObject Owner { get; private set; }

    public virtual void Initialize(
        ItemData data,
        GameObject owner)
    {
        Data = data;
        Owner = owner;
    }

    // [로컬 클라] 사용 요청을 만든다. 실패하면 연출도 요청도 하지 않음
    // 여기서의 검사는 연출 남발을 막기 위한 용도. 최종 판정은 판정자(서버/오프라인)쪽에서 진행
    protected bool TryBuildUseRequest(out ItemUseContext context)
    {
        context = default;
        if (Owner == null || Data == null) return false;
        if (Data.UseAction == null)
        {
            Owner.GetComponent<PlayerItemFeedback>()?.Unavailable("사용 기능 연결 필요");
            Debug.LogWarning($"[Item] {Data.DisplayName}: ItemData의 UseAction 연결이 필요합니다.", this);
            return false;
        }
        if (!PlayerActionGuard.CanAct(Owner)) return false;

        var camera = Owner.GetComponentInChildren<Camera>();
        if (camera == null) return false;
        // 클라 쿨다운 : 기존 PlayerItemController.TryBeginUseCooldown 재사용
        var controller = Owner.GetComponent<PlayerItemController>();
        if (controller != null && !controller.TryBeginUseCooldown(Data, Data.UseAction.Cooldown))
        {
            Owner.GetComponent<PlayerItemFeedback>()?.Unavailable("재사용 대기");
            return false;
        }

        // 조준값은 로컬 카메라 기준이고 서버는 값을 받아서 검증 후 사용
        Transform aim = camera.transform;
        context = new ItemUseContext(Owner, Data, aim.position, aim.forward);
        controller?.PlayUsePresentation(Data);
        return true;
    }

    public abstract void OnUseStarted();

    public virtual void OnUseCanceled()
    {
    }

    public virtual void OnEquipped()
    {
    }

    public virtual void OnUnequipped()
    {
    }
}
