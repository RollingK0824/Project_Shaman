using UnityEngine;

public class GongItem : ItemBase
{
    [SerializeField, Min(.1f)] private float _effectRadius = 6f;
    [SerializeField, Min(.01f)] private float _effectDuration = 3f;
    [SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    [SerializeField, Min(0f)] private float _reuseDelay = 2f;
    [SerializeField] private LayerMask _targetMask = ~0;
    [SerializeField] private ItemSense _senses = ItemSense.Vision | ItemSense.Hearing;
    [SerializeField] private ParticleSystem _pulse;
    [SerializeField] private AudioSource _audio;
    private float _nextUse;
    public override void OnUseStarted()
    {
        if (!PlayerActionGuard.CanAct(Owner) || !ItemAttackAuthority.CanResolve(Owner) || Time.time < _nextUse) return;
        var items = Owner.GetComponent<PlayerItemController>();
        if (items != null && !items.TryBeginUseCooldown(Data, _reuseDelay)) return;
        _nextUse = Time.time + _reuseDelay;
        var control = new ItemControlEffect { Kind = ItemControlKind.SensoryInterference, Duration = _effectDuration, Senses = _senses };
        var attack = new ItemAttackSession(Owner, 0f, control, ItemReactionFallback.None, _targetMask, _pulseDuration);
        DamageTrigger.CreateSphere(attack, Owner.transform.position, _effectRadius, _pulseDuration);
        if (_pulse != null) _pulse.Play();
        if (_audio != null && _audio.clip != null) _audio.Play();
        PlayerItemActions.ReportUse(Owner, Data);
    }
}
