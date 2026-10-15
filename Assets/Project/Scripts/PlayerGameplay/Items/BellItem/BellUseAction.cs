using UnityEngine;


// [판정 전용] 방울 범위 경직. 판정자(오프라인 LocalGameplayJudge / 서버 NetPlayerItemUse)만 Execute 호출
[CreateAssetMenu(fileName = "BellUseAction", menuName = "Project Shaman/Item Use/Bell")]
public class BellUseAction : ItemUseAction
{
    [Header("Bell Effect")]
    [SerializeField] private float _effectRadius = 5f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    [SerializeField, Min(0f)] private float _staggerDuration = .5f;

    [Header("Remote Presentation")]
    [SerializeField] private GameObject _remotePulsePrefab;
    [SerializeField, Min(.1f)] private float _remotePulseLifetime = 2f;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner)) return false;

        var control = new ItemControlEffect { Kind = ItemControlKind.Stagger, Duration = _staggerDuration };
        var attack = new ItemAttackSession(context.Owner, 0f, control,
            ItemReactionFallback.Bell, _targetMask, _pulseDuration);

        // 위치는 판정자 쪽 플레이어 위치 (서버에서는 서버가 가진 위치)
        DamageTrigger.CreateSphere(attack, context.Owner.transform.position, _effectRadius, _pulseDuration);
        Debug.Log($"[Bell] 반응 대상 수: {attack.AppliedTargetCount}", context.Owner);
        return true;
    }

    // [다른 클라] 방울 파동 VFX만. 3인칭용 프리팹이 아직 없으면 비워 둬도 됨
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        Debug.Log($"[Bell] 원격 연출: {context.Owner.name}", context.Owner);
        SpawnEffect(_remotePulsePrefab, context.Owner.transform.position, Quaternion.identity, _remotePulseLifetime);
    }
}
