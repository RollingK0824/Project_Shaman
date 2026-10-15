using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Test/Net Test Bell Use Action")]
public class NetTestBellUseAction : ItemUseAction
{
    [SerializeField] private float _effectRadius = 5f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    [SerializeField, Min(0f)] private float _staggerDuration = .5f;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner)) return false;

        var control = new ItemControlEffect { Kind = ItemControlKind.Stagger, Duration = _staggerDuration };
        var attack = new ItemAttackSession(context.Owner, 0f, control,
            ItemReactionFallback.Bell, _targetMask, _pulseDuration);

        DamageTrigger.CreateSphere(attack, context.Owner.transform.position, _effectRadius, _pulseDuration);
        Debug.Log($"[NetTestBell] 반응 대상 수: {attack.AppliedTargetCount}", context.Owner);
        return true;
    }

    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        Debug.Log($"[NetTestBell] 원격 연출: {context.Owner.name}", context.Owner);
    }
}
