using UnityEngine;

public class CoinItem : ItemBase
{
    [Header("Coin")]
    [SerializeField]
    private bool _consumeOnSuccessfulUse;

    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Coin] 장착: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseStarted()
    {
        if (Owner == null)
        {
            return;
        }

        PlayerInteractor interactor =
            Owner.GetComponent<PlayerInteractor>();

        if (interactor == null)
        {
            Debug.LogWarning(
                "[Coin] PlayerInteractor를 찾을 수 없습니다.",
                gameObject
            );

            return;
        }

        if (!interactor.TryGetTargetComponent(
                out ICoinReactable target))
        {
            Debug.Log(
                "[Coin] 엽전을 건넬 대상이 없습니다.",
                gameObject
            );

            return;
        }

        bool reacted =
            target.ReactToCoin(Owner);

        if (!reacted)
        {
            Debug.Log(
                "[Coin] 대상이 엽전에 반응하지 않았습니다.",
                gameObject
            );

            return;
        }

        Debug.Log(
            $"[Coin] 사용 성공: {Data.DisplayName}",
            gameObject
        );

        if (_consumeOnSuccessfulUse)
        {
            ConsumeOne();
        }
    }

    private void ConsumeOne()
    {
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