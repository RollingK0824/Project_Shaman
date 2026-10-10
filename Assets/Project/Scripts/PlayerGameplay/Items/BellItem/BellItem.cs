using System.Collections.Generic;
using UnityEngine;

public class BellItem : ItemBase
{
    [Header("Bell Effect")]
    [SerializeField] private float _effectRadius = 5f;
    [SerializeField] private LayerMask _targetMask = ~0;

    private ParticleSystem _bellPulse;


    private void Awake()
    {
        Transform pulseTransform =
            transform.Find("BellEffectPoint/BellPulse");

        if (pulseTransform != null)
        {
            _bellPulse =
                pulseTransform.GetComponent<ParticleSystem>();
        }
    }


    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Bell] 장착: {Data.DisplayName}",
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
            $"[Bell] 장착 해제: {Data.DisplayName}",
            gameObject
        );
    }


    public override void OnUseStarted()
    {
        if (!PlayerActionGuard.CanAct(Owner))
        {
            return;
        }

        if (Owner == null)
        {
            return;
        }

        UseBell();
    }


    public override void OnUseCanceled()
    {
        // 방울은 클릭 순간 한 번 사용
    }


    private void UseBell()
    {
        PlayBellPulse();

        Debug.Log(
            $"[Bell] 사용: {Data.DisplayName}",
            gameObject
        );

        Collider[] hits =
            Physics.OverlapSphere(
                Owner.transform.position,
                _effectRadius,
                _targetMask,
                QueryTriggerInteraction.Collide
            );

        HashSet<IBellReactable> reactedTargets =
            new HashSet<IBellReactable>();

        foreach (Collider hit in hits)
        {
            IBellReactable reactable =
                hit.GetComponentInParent<IBellReactable>();

            if (reactable == null)
            {
                continue;
            }

            if (!reactedTargets.Add(reactable))
            {
                continue;
            }

            reactable.ReactToBell(Owner);
        }

        Debug.Log(
            $"[Bell] 반응 대상 수: {reactedTargets.Count}",
            gameObject
        );
    }


    private void PlayBellPulse()
    {
        if (_bellPulse == null)
        {
            return;
        }

        _bellPulse.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        _bellPulse.Play();
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            _effectRadius
        );
    }
}