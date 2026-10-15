using UnityEngine;

public class SaltItem : ItemBase
{
    [Header("Salt")]
    [SerializeField]
    private bool _consumeOnSuccessfulUse = false;

    [Header("Contact Attack - Prototype Values")]
    [SerializeField, Min(0f)] private float _damageAmount = 10f;
    [SerializeField, Min(.01f)] private float _contactHalfWidth = .2f;
    [SerializeField, Min(.04f)] private float _contactDuration = .15f;
    [SerializeField] private LayerMask _targetMask = ~0;
    private ParticleSystem _saltSpray;


    private void Awake()
    {
        _saltSpray =
            GetComponentInChildren<ParticleSystem>(true);
    }


    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Salt] 장착: {Data.DisplayName}",
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
            $"[Salt] 장착 해제: {Data.DisplayName}",
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

        PlaySaltSpray();

        PlayerInteractor interactor =
            Owner.GetComponent<PlayerInteractor>();

        if (interactor == null)
        {
            Debug.LogWarning(
                "[Salt] PlayerInteractor가 없습니다.",
                gameObject
            );

            return;
        }

        if (!ItemAttackAuthority.CanResolve(Owner)) return;
        if (!interactor.TryGetAttackRay(out Ray ray, out float reach))
        {
            Debug.Log("[Salt] 소금을 사용할 대상이 없습니다.", gameObject);
            return;
        }
        var attack = new ItemAttackSession(Owner, _damageAmount, default,
            ItemReactionFallback.Salt, _targetMask, _contactDuration);
        var inventory = Owner.GetComponent<PlayerInventory>();
        var data = Data;
        var owner = Owner;
        bool consumed = false;
        bool consumeOnSuccess = _consumeOnSuccessfulUse;
        attack.HitApplied += _ =>
        {
            // One consumable per spray, even when multiple targets/child colliders overlap.
            if (consumed) return;
            consumed = true;
            Debug.Log($"[Salt] 사용 성공: {data?.DisplayName}");
            if (consumeOnSuccess && inventory != null && data != null) inventory.ConsumeItem(data, 1);
            PlayerItemActions.ReportUse(owner, data);
        };
        DamageTrigger.CreateBox(attack, ray.GetPoint(reach * .5f),
            new Vector3(_contactHalfWidth, _contactHalfWidth, reach * .5f),
            Quaternion.LookRotation(ray.direction), _contactDuration);
    }


    public override void OnUseCanceled()
    {
        // 소금은 클릭 순간 한 번 사용하는 방식
    }


    private void PlaySaltSpray()
    {
        if (_saltSpray == null)
        {
            return;
        }

        _saltSpray.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        _saltSpray.Play();
    }


}
