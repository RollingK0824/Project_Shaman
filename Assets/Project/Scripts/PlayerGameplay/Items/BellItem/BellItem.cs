using UnityEngine;

public class BellItem : ItemBase
{
    //[Header("Bell Effect")]
    //[SerializeField] private float _effectRadius = 5f;
    //[SerializeField] private LayerMask _targetMask = ~0;
    //
    //[SerializeField, Min(.04f)] private float _pulseDuration = .15f;
    //[SerializeField, Min(0f)] private float _staggerDuration = .5f;
    private ParticleSystem _bellPulse;


    private void Awake()
    {
        Transform pulseTransform = transform.Find("BellEffectPoint/BellPulse");

        if (pulseTransform != null)
        {
            _bellPulse = pulseTransform.GetComponent<ParticleSystem>();
        }
    }


    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log($"[Bell] 장착: {Data.DisplayName}", gameObject);
    }


    public override void OnUnequipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log($"[Bell] 장착 해제: {Data.DisplayName}", gameObject);
    }


    public override void OnUseStarted()
    {
        //if (!PlayerActionGuard.CanAct(Owner))
        //{
        //    return;
        //}
        //if (Owner == null)
        //{
        //    return;
        //}
        //
        //UseBell();

        if (!TryBuildUseRequest(out var context)) return;

        ItemVfxPresentation.Chime(Owner, transform);
        Debug.Log($"[Bell] 사용: {Data.DisplayName}", gameObject);
        PlayerEvents.RaiseItemUseRequested(context);
    }


    public override void OnUseCanceled()
    {
        // 방울은 클릭 순간 한 번 사용
    }


    //private void UseBell()
    //{
    //    PlayBellPulse();
    //
    //    Debug.Log(
    //        $"[Bell] 사용: {Data.DisplayName}",
    //        gameObject
    //    );
    //
    //    if (!ItemAttackAuthority.CanResolve(Owner)) return;
    //    var control = new ItemControlEffect { Kind = ItemControlKind.Stagger, Duration = _staggerDuration };
    //    var attack = new ItemAttackSession(Owner, 0f, control, ItemReactionFallback.Bell, _targetMask, _pulseDuration);
    //    DamageTrigger.CreateSphere(attack, Owner.transform.position, _effectRadius, _pulseDuration);
    //    Debug.Log($"[Bell] 반응 대상 수: {attack.AppliedTargetCount}", gameObject);
    //}


    private void PlayBellPulse()
    {
        if (_bellPulse == null)
        {
            return;
        }

        _bellPulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _bellPulse.Play();
    }


    //private void OnDrawGizmosSelected()
    //{
    //    Gizmos.DrawWireSphere(
    //        transform.position,
    //        _effectRadius
    //    );
    //}
}
