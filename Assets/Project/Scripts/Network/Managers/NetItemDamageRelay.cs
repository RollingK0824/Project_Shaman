using UnityEngine;
using Mirror;

// [서버] 아이템·환경 피해 요청 중 플레이어가 아닌 대상(잡귀 등)을 실제 피해로 전달
// 플레이어는 NetPlayerHealth가 자기 것만 처리, 오프라인은 LocalGameplayJudge가 처리
public class NetItemDamageRelay : MonoBehaviour
{
    private void OnEnable() => PlayerEvents.DamageRequested += HandleDamageRequested;
    private void OnDisable() => PlayerEvents.DamageRequested -= HandleDamageRequested;

    private static void HandleDamageRequested(IDamageable target, float amount, GameObject source)
    {
        if (!NetworkServer.active || target == null || target is PlayerHealth) return;
        if (target.IsDead || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;

        target.ReceiveDamage(amount, source);
    }
}
