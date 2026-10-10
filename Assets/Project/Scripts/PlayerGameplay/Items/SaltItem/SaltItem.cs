using UnityEngine;

public class SaltItem : ItemBase
{
    [Header("Salt")]
    [SerializeField]
    private bool _consumeOnSuccessfulUse = false;

    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Salt] 장착: {Data.DisplayName}",
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
            $"[Salt] 장착 해제: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseStarted()
    {
        if (!PlayerActionGuard.CanAct(Owner)) return;
        if (Owner == null)
        {
            return;
        }

        PlayerInteractor interactor =
            Owner.GetComponent<PlayerInteractor>();

        if (interactor == null)
        {
            Debug.LogWarning(
                "[Salt] PlayerInteractor가 없습니다.",
                gameObject
            );

            return;
        }

        if (!interactor.TryGetTargetComponent(
                out ISaltReactable target))
        {
            Debug.Log(
                "[Salt] 소금을 사용할 대상이 없습니다.",
                gameObject
            );

            return;
        }

        bool reacted =
            target.ReactToSalt(Owner);

        if (!reacted)
        {
            Debug.Log(
                "[Salt] 대상이 소금에 반응하지 않았습니다.",
                gameObject
            );

            return;
        }

        Debug.Log(
            $"[Salt] 사용 성공: {Data.DisplayName}",
            gameObject
        );

        if (_consumeOnSuccessfulUse)
        {
            ConsumeOne();
        }
    }

    public override void OnUseCanceled()
    {
        // 소금은 클릭 순간 한 번 사용하는 방식으로 테스트한다.
    }

    private void ConsumeOne()
    {
        if (Data == null ||
            Owner == null)
        {
            return;
        }

        PlayerInventory inventory =
            Owner.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            return;
        }

        inventory.ConsumeItem(
            Data,
            1
        );
    }
}