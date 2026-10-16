using UnityEngine;

// [판정 전용] 조준한 대상에게 엽전 건네기. 판정자(오프라인 LocalGameplayJudge / 서버 NetPlayerItemUse)만 Execute 호출
// 대상 찾기는 PlayerInteractor와 같은 기준(카메라 레이, 거리)을 판정자가 받은 조준값으로 다시 계산
[CreateAssetMenu(fileName = "CoinUseAction", menuName = "Project Shaman/Item Use/Coin")]
public class CoinUseAction : ItemUseAction
{
    [SerializeField, Min(.5f)] private float _reach = 3f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField] private bool _consumeOnSuccessfulUse;

    private static readonly RaycastHit[] _hits = new RaycastHit[16];

    public override bool Execute(in ItemUseContext context)
    {
        if (context.Owner == null || !PlayerActionGuard.CanAct(context.Owner)) return false;

        ICoinReactable target = FindTarget(context);
        if (target == null)
        {
            Debug.Log("[Coin] 엽전을 건넬 대상이 없습니다.", context.Owner);
            return false;
        }

        if (!target.ReactToCoin(context.Owner))
        {
            Debug.Log("[Coin] 대상이 엽전에 반응하지 않았습니다.", context.Owner);
            return false;
        }

        if (_consumeOnSuccessfulUse) context.Consume?.Invoke(1);
        Debug.Log($"[Coin] 사용 성공: {context.Data.DisplayName}", context.Owner);
        return true;
    }

    // 자기 콜라이더는 건너뛰고 가장 가까운 콜라이더 하나만 대상 후보 (벽 너머 대상 방지)
    private ICoinReactable FindTarget(in ItemUseContext context)
    {
        var ray = new Ray(context.AimOrigin, context.AimDirection);
        int count = Physics.RaycastNonAlloc(ray, _hits, _reach, _targetMask, QueryTriggerInteraction.Ignore);

        Collider nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (_hits[i].collider.transform.IsChildOf(context.Owner.transform)) continue;
            if (_hits[i].distance >= nearestDistance) continue;

            nearest = _hits[i].collider;
            nearestDistance = _hits[i].distance;
        }

        return nearest != null ? nearest.GetComponentInParent<ICoinReactable>() : null;
    }

}
