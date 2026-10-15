using UnityEngine;
using Mirror;


// [1인 테스트 전용] 버스 요청 부분을 기존 로컬 함수로 처리
// 네으쿼크 세션에서는 스스로 꺼지기에, 클라이언트가 판정하는 일을 막는다.
public class LocalGameplayJudge : MonoBehaviour
{
    private void OnEnable()
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogError("[LocalGameplayJudge] 네트워크 세션에서는 사용하지 않습니다. 비활성화합니다.", this);
            enabled = false;
            return;
        }

        PlayerEvents.DamageRequested += HandleDamageRequested;
        PlayerEvents.PickupRequested += HandlePickupRequested;
        PlayerEvents.StressRequested += HandleStressRequested;
        PlayerEvents.FlagActivateRequested += HandleFlagActivateRequested;
        PlayerEvents.ItemUseRequested += HandleItemUseRequested;
    }

    private void OnDisable()
    {
        PlayerEvents.DamageRequested -= HandleDamageRequested;
        PlayerEvents.PickupRequested -= HandlePickupRequested;
        PlayerEvents.StressRequested -= HandleStressRequested;
        PlayerEvents.FlagActivateRequested -= HandleFlagActivateRequested;
        PlayerEvents.ItemUseRequested -= HandleItemUseRequested;
    }

    private void HandleDamageRequested(IDamageable target, float amount, GameObject source)
    {
        if (NetworkServer.active || NetworkClient.active || target == null) return;
        target.ReceiveDamage(amount, source);
    }

    private void HandlePickupRequested(GameObject interactor, WorldItem item)
    {
        if (NetworkServer.active || NetworkClient.active || item == null || !item.CanInteract(interactor)) return;
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.TryAddItem(item.ItemData)) return;

        item.ApplyCollected();
    }

    private void HandleStressRequested(PlayerStressController target, float delta, StressCause cause, GameObject source)
    {
        if (NetworkServer.active || NetworkClient.active || target == null || !PlayerActionGuard.CanAct(target.gameObject)) return;
        target.SetStress(target.CurrentStress + delta);
    }

    private void HandleFlagActivateRequested(GameObject interactor, ExorcismFlagInteractable flag)
    {
        if (NetworkServer.active || NetworkClient.active || flag == null) return;

        flag.TryActivate(interactor);
    }

    private void HandleItemUseRequested(ItemUseContext context)
    {
        if (NetworkServer.active || NetworkClient.active || context.Owner == null) return;


        PlayerInventory inventory = context.Owner.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.Contains(context.Data)) return;

        ItemData data = context.Data;
        data.UseAction.Execute(context.WithConsume(amount => inventory.ConsumeItem(data, amount)));
    }
}
