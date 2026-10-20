using UnityEngine;
using ProjectShaman.AI.Ghost;

// [판정 전용] 소금 전방 접촉 공격

[CreateAssetMenu(fileName = "SaltUseAction", menuName = "Project Shaman/Item Use/Salt")]
public class SaltUseAction : ItemUseAction
{
    [Header("Contact Attack - Prototype Values")]
    [SerializeField, Min(0f)] private float _damageAmount = 10f;
    [SerializeField, Min(.01f)] private float _contactHalfWidth = .2f;
    [SerializeField, Min(.04f)] private float _contactDuration = .15f;
    [SerializeField] private LayerMask _targetMask = ~0;

    [Header("Reach")]
    // 원래 PlayerInteractor의 Interaction Distance / Interaction Mask 값
    [SerializeField, Min(.1f)] private float _reach = 3f;
    [SerializeField] private LayerMask _solidMask = ~0;

    [Header("Consumption")]
    [SerializeField] private bool _consumeOnSuccessfulUse = false;

    [Header("Remote Presentation")]
    [SerializeField] private GameObject _remoteSprayPrefab;
    [SerializeField, Min(.1f)] private float _remoteSprayLifetime = 1.5f;

    private static readonly RaycastHit[] _rayHits = new RaycastHit[16];

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null) return false;

        var ray = new Ray(context.AimOrigin, context.AimDirection);
        float reach = GetReach(ray, context.Owner.transform);
        if (reach <= 0f) return false;

        var attack = new ItemAttackSession(context.Owner, _damageAmount, default, ItemReactionFallback.Salt, _targetMask, _contactDuration, CanHitGhost);

        // 맞는 시점은 Execute보다 늦을 수 있음(Trigger 접촉 0.15초) → 콜백으로 소모
        // in 파라미터는 람다에서 쓸 수 없으므로 지역 변수로 복사
        var consume = context.Consume;
        bool consumeOnSuccess = _consumeOnSuccessfulUse;
        string itemName = context.Data.DisplayName;
        bool succeeded = false;
        attack.HitApplied += _ =>
        {
            if (succeeded) return;   // 여러 대상을 맞아도 한 번만
            succeeded = true;
            Debug.Log($"[Salt] 사용 성공: {itemName}");
            if (consumeOnSuccess) consume?.Invoke(1);
        };

        if (DamageTrigger.CreateBox(attack, ray.GetPoint(reach * .5f), new Vector3(_contactHalfWidth, _contactHalfWidth, reach * .5f), Quaternion.LookRotation(ray.direction), _contactDuration) == null) return false;
        // Report the resolved spray once; delayed/multiple hits must not count as additional uses.
        // Execute success is reported once by LocalGameplayJudge / NetPlayerItemUse.
        return true;
    }

    private static bool CanHitGhost(Collider other)
    {
        // IDamageable is also implemented by players and ordinary NPCs. IGhostTarget
        // identifies a ghost's prey, not a ghost. Only manifested ghost entities qualify.
        if (other.GetComponentInParent<PlayerHealth>() != null) return false;
        var ghost = other.GetComponentInParent<AI_GhostEntity>();
        return ghost != null && ghost.isActiveAndEnabled;
    }

    private float GetReach(Ray ray, Transform owner)
    {
        float reach = _reach;

        int count = Physics.RaycastNonAlloc(ray, _rayHits, _reach, _solidMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (_rayHits[i].collider.transform.IsChildOf(owner)) continue;
            reach = Mathf.Min(reach, _rayHits[i].distance + .01f);
        }


        return reach;
    }

    // [다른 클라] 소금 분사 VFX만. 3인칭용 프리팹이 아직 없으면 비워 둬도 됨
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        context.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(context.Data);
        Debug.Log($"[Salt] 원격 연출: {context.Owner.name}", context.Owner);
        if (context.Owner.GetComponent<PlayerItemController>()?.PlayThirdPersonSaltSpray(context.AimDirection) != true)
            SpawnEffect(_remoteSprayPrefab, context.AimOrigin + context.AimDirection * .5f, Quaternion.LookRotation(context.AimDirection), _remoteSprayLifetime);
    }
}
