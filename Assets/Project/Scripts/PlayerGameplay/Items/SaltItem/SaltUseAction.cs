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
    [SerializeField, Min(.01f)] private float _playerSlowDuration = 3f;
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

    // Send the body-relative spray origin, not the orbit camera's setback, through
    // the existing item request. Server validation and authority remain unchanged.
    public bool TryBuildRequest(in ItemUseContext cameraContext, out ItemUseContext request)
    {
        request = cameraContext;
        if (cameraContext.Owner == null || !PlayerActionGuard.CanAct(cameraContext.Owner) ||
            !TryGetSpray(cameraContext.Owner.transform, new Ray(cameraContext.AimOrigin, cameraContext.AimDirection),
                _reach, _solidMask, out var spray, out _)) return false;
        request = new ItemUseContext(cameraContext.Owner, cameraContext.Data, spray.origin, spray.direction, cameraContext.Consume);
        return true;
    }

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null) return false;

        if (!TryGetSpray(context.Owner.transform, new Ray(context.AimOrigin, context.AimDirection),
            _reach, _solidMask, out var ray, out float reach)) return false;

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

        // Player slow is independent of the ghost's multiplier and never damages HP.
        var playerAttack = new ItemAttackSession(context.Owner, 0f,
            new ItemControlEffect { Kind = ItemControlKind.Slow, Duration = _playerSlowDuration, SpeedMultiplier = .75f },
            ItemReactionFallback.None, _targetMask, _contactDuration,
            other => other.GetComponentInParent<PlayerHealth>() != null);
        playerAttack.HitApplied += _ =>
        {
            if (succeeded) return;
            succeeded = true;
            if (consumeOnSuccess) consume?.Invoke(1);
        };
        DamageTrigger.CreateBox(playerAttack, center, halfExtents, rotation, _contactDuration);

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

    // Keep the camera's aim line, but measure the short spray from the player's depth.
    // The horizontal plane avoids moving a first-person origin to the floor when looking down.
    // Neither local camera objects nor animated held meshes are required on the server.
    public static bool TryGetSpray(Transform owner, Ray aim, float maxReach, int solidMask,
        out Ray spray, out float reach)
    {
        spray = aim;
        reach = 0f;
        if (owner == null || !Finite(aim.origin) || !Finite(aim.direction) ||
            aim.direction.sqrMagnitude < .0001f || !float.IsFinite(maxReach) || maxReach <= 0f) return false;
        aim.direction = aim.direction.normalized;
        Vector3 flatDirection = Vector3.ProjectOnPlane(aim.direction, Vector3.up);
        float setback = flatDirection.sqrMagnitude > .01f
            ? Mathf.Max(0f, Vector3.Dot(owner.position - aim.origin, flatDirection) / flatDirection.sqrMagnitude)
            : 0f;
        // Do not advance the origin through a wall between a third-person camera and its player.
        float clear = ClearReach(aim, owner, setback + maxReach, solidMask);
        if (clear <= setback) return false;
        spray = new Ray(aim.GetPoint(setback), aim.direction);
        reach = Mathf.Min(maxReach, clear - setback);
        return reach > .001f;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.x) &&
        float.IsFinite(value.y) && float.IsFinite(value.z);

    private static float ClearReach(Ray ray, Transform owner, float reach, int solidMask)
    {
        int count = Physics.RaycastNonAlloc(ray, _rayHits, reach, solidMask, QueryTriggerInteraction.Ignore);
        // NonAlloc results are unordered. A full buffer may omit the nearest wall.
        var hits = count == _rayHits.Length
            ? Physics.RaycastAll(ray, reach, solidMask, QueryTriggerInteraction.Ignore) : _rayHits;
        if (hits != _rayHits) count = hits.Length;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(owner)) continue;
            reach = Mathf.Min(reach, hits[i].distance + .01f);
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
