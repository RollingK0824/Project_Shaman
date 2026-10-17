using UnityEngine;

[CreateAssetMenu(fileName = "GongUseAction", menuName = "Project Shaman/Item Use/Gong")]
public sealed class GongUseAction : ItemUseAction
{
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        context.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(context.Data);
    }
    [SerializeField, Min(.1f)] private float _effectRadius = 6f;
    [SerializeField, Min(.01f)] private float _effectDuration = 3f;
    [SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField] private ItemSense _senses = ItemSense.Vision | ItemSense.Hearing;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner)) return false;
        var effect = new ItemControlEffect { Kind = ItemControlKind.SensoryInterference,
            Duration = _effectDuration, Senses = _senses };
        var attack = new ItemAttackSession(context.Owner, 0f, effect,
            ItemReactionFallback.None, _targetMask, _pulseDuration);
        DamageTrigger.CreateSphere(attack, context.Owner.transform.position, _effectRadius, _pulseDuration);
        PlayerItemActions.ReportUse(context.Owner, context.Data);
        return true;
    }
}
