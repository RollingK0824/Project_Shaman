using System.Collections.Generic;
using UnityEngine;

public class BellItem : ItemBase
{
    [Header("Bell Effect")]
    [SerializeField] private float _effectRadius = 5f;

    [SerializeField] private LayerMask _targetMask = ~0;

    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Bell] 장착: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUnequipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Bell] 장착 해제: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseStarted()
    {
        if (Owner == null)
        {
            return;
        }

        UseBell();
    }

    public override void OnUseCanceled()
    {
        // 방울은 클릭 순간 한 번 사용하는 아이템이므로
        // 현재 프로토타입에서는 해제 시 별도 처리를 하지 않는다.
    }

    private void UseBell()
    {
        Debug.Log(
            $"[Bell] 사용: {Data.DisplayName}",
            gameObject
        );

        Collider[] hits =
            Physics.OverlapSphere(
                Owner.transform.position,
                _effectRadius,
                _targetMask,
                QueryTriggerInteraction.Collide
            );

        HashSet<IBellReactable> reactedTargets =
            new HashSet<IBellReactable>();

        foreach (Collider hit in hits)
        {
            IBellReactable reactable =
                hit.GetComponentInParent<IBellReactable>();

            if (reactable == null)
            {
                continue;
            }

            // NPC에 Collider가 여러 개 있어도
            // 같은 대상에게 여러 번 전달하지 않는다.
            if (!reactedTargets.Add(reactable))
            {
                continue;
            }

            reactable.ReactToBell(Owner);
        }

        Debug.Log(
            $"[Bell] 반응 대상 수: {reactedTargets.Count}",
            gameObject
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            _effectRadius
        );
    }
}