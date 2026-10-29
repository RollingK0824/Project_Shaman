using UnityEngine;
using System.Collections.Generic;
using ProjectShaman.AI;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Minion;

// [판정 전용] 소금 전방 접촉 공격

[CreateAssetMenu(fileName = "SaltUseAction", menuName = "Project Shaman/Item Use/Salt")]
public class SaltUseAction : ItemUseAction
{
    [Header("Contact Attack - Prototype Values")]
    [SerializeField, Range(.1f, 1f)] private float _slowMultiplier = .6f;
    [SerializeField, Min(.01f)] private float _slowDuration = 3f;
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

    public float VisualReach => _reach;
    public float VisualHalfWidth => _contactHalfWidth;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null) return false;

        var ray = new Ray(context.AimOrigin, context.AimDirection);
        float reach = GetReach(ray, context.Owner.transform);
        if (reach <= 0f) return false;

        // Cache the contact before the receiver applies the slow.
        var contacts = new Dictionary<GameObject, (Vector3 point, Vector3 normal)>();
        bool CaptureContact(Collider other)
        {
            if (!CanHitGhost(other)) return false;
            var receiver = other.GetComponentInParent<IDamageable>() as Component ??
                other.GetComponentInParent<IItemControlReceiver>() as Component ??
                other.GetComponentInParent<IBeanMugwortReactable>() as Component;
            if (receiver != null && !contacts.ContainsKey(receiver.gameObject))
            {
                var point = other.ClosestPoint(ray.origin);
                var normal = ray.origin - point;
                if (normal.sqrMagnitude < .0001f) normal = -ray.direction;
                contacts.Add(receiver.gameObject, (point, normal.normalized));
            }
            return true;
        }
        var attack = new ItemAttackSession(context.Owner, 0f,
            new ItemControlEffect { Kind = ItemControlKind.Slow, Duration = _slowDuration, SpeedMultiplier = _slowMultiplier },
            ItemReactionFallback.BeanMugwort, _targetMask, _contactDuration, CaptureContact);
        var presentation = context.Owner.GetComponent<PlayerSaltHitPresentation>() ?? context.Owner.AddComponent<PlayerSaltHitPresentation>();
        long attackId = presentation.BeginAttack();
        int targetIndex = 0;
        attack.HitApplied += target =>
        {
            if (!contacts.TryGetValue(target, out var contact)) return;
            // HitApplied now confirms an accepted slow; HP must remain unchanged.
            if (presentation != null) presentation.PublishConfirmedHit(attackId, targetIndex++, contact.point, contact.normal);
        };

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

        Vector3 center = ray.GetPoint(reach * .5f);
        Vector3 halfExtents = new Vector3(_contactHalfWidth, _contactHalfWidth, reach * .5f);
        Quaternion rotation = Quaternion.LookRotation(ray.direction);
        if (DamageTrigger.CreateBox(attack, center, halfExtents, rotation, _contactDuration) == null) return false;

        // Villagers are not hurt by salt: a damage-free contact on the same volume lets them react (ISaltReactable).
        // It neither counts as a hit nor consumes salt.
        var villagerTouch = new ItemAttackSession(context.Owner, 0f, default, ItemReactionFallback.Salt, _targetMask, _contactDuration, IsVillager);
        DamageTrigger.CreateBox(villagerTouch, center, halfExtents, rotation, _contactDuration);
        // Report the resolved spray once; delayed/multiple hits must not count as additional uses.
        // Execute success is reported once by LocalGameplayJudge / NetPlayerItemUse.
        return true;
    }

    internal static bool CanHitGhost(Collider other)
    {
        // IDamageable is also implemented by players and ordinary NPCs. IGhostTarget
        // identifies a ghost's prey, not a ghost. Allow manifested ghosts and minions.
        if (other.GetComponentInParent<PlayerHealth>() != null) return false;
        var ghost = other.GetComponentInParent<AI_GhostEntity>();
        if (ghost != null && ghost.isActiveAndEnabled) return true;
        var minion = other.GetComponentInParent<AI_Minion>();
        return minion != null && minion.isActiveAndEnabled;
    }

    private static bool IsVillager(Collider other)
    {
        var villager = other.GetComponentInParent<AI_ItemReactionReceiver>();
        return villager != null && villager.isActiveAndEnabled;
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
        var timed = context.Owner.GetComponent<PlayerTimedItemUse>() ?? context.Owner.AddComponent<PlayerTimedItemUse>();
        timed.BeginRemote(context, released =>
        {
            released.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(released.Data);
            if (released.Owner.GetComponent<PlayerItemController>()?.PlayThirdPersonSaltSpray(released.AimDirection, released.AimOrigin, _reach, _contactHalfWidth) != true)
                SaltVfxBurst.Spray(released.Owner, released.AimOrigin + released.AimDirection * .25f,
                    new Ray(released.AimOrigin, released.AimDirection), _reach, _contactHalfWidth);
        });
    }
}
