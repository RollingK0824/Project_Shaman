using UnityEngine;

// [판정 전용] 징 범위 감각 교란. 판정자(오프라인 LocalGameplayJudge / 서버 NetPlayerItemUse)만 Execute 호출
[CreateAssetMenu(fileName = "GongUseAction", menuName = "Project Shaman/Item Use/Gong")]
public class GongUseAction : ItemUseAction
{
    [Header("Gong Effect")]
    [SerializeField, Min(.1f)] private float _effectRadius = 6f;
    [SerializeField, Min(.01f)] private float _effectDuration = 3f;
    [SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField] private ItemSense _senses = ItemSense.Vision | ItemSense.Hearing;

    [Header("Remote Presentation")]
    [SerializeField] private GameObject _remotePulsePrefab;
    [SerializeField, Min(.1f)] private float _remotePulseLifetime = 2f;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null) return false;

        var control = new ItemControlEffect { Kind = ItemControlKind.SensoryInterference, Duration = _effectDuration, Senses = _senses };
        var attack = new ItemAttackSession(context.Owner, 0f, control, ItemReactionFallback.None, _targetMask, _pulseDuration);

        // 위치는 판정자 쪽 플레이어 위치 (서버에서는 서버가 가진 위치)
        if (DamageTrigger.CreateSphere(attack, context.Owner.transform.position, _effectRadius, _pulseDuration) == null) return false;
        Debug.Log($"[Gong] 반응 대상 수: {attack.AppliedTargetCount}", context.Owner);
        return true;
    }


    // [다른 클라] 징 파동, 소리. 3인칭용 프리팹이 아직 없으면 비워 둬도 됨
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        context.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(context.Data);
        if (!ItemVfxPresentation.Pulse(context.Owner, true))
            SpawnEffect(_remotePulsePrefab, context.Owner.transform.position, Quaternion.identity, _remotePulseLifetime);
    }
}
