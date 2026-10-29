using UnityEngine;

[CreateAssetMenu(fileName = "BeanMugwortUseAction", menuName = "Project Shaman/Item Use/Bean Mugwort")]
public class BeanMugwortUseAction : ItemUseAction
{
    [Header("Throw")]
    [SerializeField] private BeanMugwortProjectile _projectilePrefab;
    [SerializeField] private float _throwForce = 8f;
    [SerializeField] private float _spawnForwardOffset = .6f;
    [SerializeField] private float _spawnDownOffset = .15f;
    [SerializeField] private Material _trajectoryMaterial;
    [SerializeField, Range(0f, 45f)] private float _aimElevationDegrees = 20f;
    public Material TrajectoryMaterial => _trajectoryMaterial;
    public float ThrowSpeed => _throwForce;
    public float FlightLifetime => _projectilePrefab != null ? _projectilePrefab.FlightLifetime : 6f;
    public float CollisionRadius => _projectilePrefab != null ? _projectilePrefab.CollisionRadius : .2f;
    public Vector3 FlightGravity => _projectilePrefab != null && _projectilePrefab.GetComponent<Rigidbody>().useGravity ? Physics.gravity : Vector3.zero;

    [Header("Effect")]
    [SerializeField, Min(0f)] private float _damageAmount = 10f;

    [Header("Consumption")]
    [SerializeField] private bool _consumeOnThrow = true;


    // [판정자] 보이지 않는 판정용 투척물
    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null ||
            (_consumeOnThrow && context.Consume == null)) return false;
        if (_projectilePrefab == null)
        {
            Debug.LogWarning("[BeanMugwort] Projectile Prefab이 연결되지 않았습니다.", this);
            return false;
        }

        GetSpawnPose(context, out var position, out var rotation);
        var projectile = Instantiate(_projectilePrefab, position, rotation);
        projectile.Initialize(context.Owner, context.AimDirection, _throwForce, _damageAmount);

        Debug.Log($"[BeanMugwort] 투척 판정 / Damage={_damageAmount}", context.Owner);

        if (_consumeOnThrow) context.Consume?.Invoke(1);
        // Only the resolving projectile counts. Visual projectiles and later contacts do not report.
        // Execute success is reported once by LocalGameplayJudge / NetPlayerItemUse.
        return true;
    }

    // [다른 클라] 연출용 투척물
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        var timed = context.Owner.GetComponent<PlayerTimedItemUse>() ?? context.Owner.AddComponent<PlayerTimedItemUse>();
        timed.BeginRemote(context, released =>
        {
            released.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(released.Data);
            SpawnVisualProjectile(released);
        }, true);
    }

    // Encode the TP hand origin into the existing request, so authority and visual use the same pose.
    public ItemUseContext WithLocalReleaseOrigin(in ItemUseContext context)
    {
        var view = context.Owner.GetComponent<PlayerViewModeController>();
        var controller = context.Owner.GetComponent<PlayerItemController>();
        // Encode one elevated launch direction in the existing request. Preview, visual and
        // authority all use this same direction; never add elevation again in Execute.
        var aimRotation = Quaternion.LookRotation(context.AimDirection, Vector3.up);
        var direction = aimRotation * Quaternion.Euler(-_aimElevationDegrees, 0f, 0f) * Vector3.forward;
        Vector3 hand = context.AimOrigin + aimRotation * new Vector3(.35f, -.2f, .65f);
        if (controller != null)
        {
            bool thirdPerson = view != null && view.CurrentViewMode == PlayerViewModeController.ViewMode.ThirdPerson;
            if (thirdPerson ? controller.TryGetThirdPersonReleasePoint(out var heldPoint) :
                controller.TryGetFirstPersonReleasePoint(out heldPoint)) hand = heldPoint;
        }
        var up = Quaternion.LookRotation(direction, Vector3.up) * Vector3.up;
        var origin = hand - direction * _spawnForwardOffset + up * _spawnDownOffset;
        return new ItemUseContext(context.Owner, context.Data, origin, direction, context.Consume);
    }

    // Local and remote presentation both invoke this once at their release frame.
    public void SpawnVisualProjectile(in ItemUseContext context)
    {
        if (_projectilePrefab == null) return;

        GetSpawnPose(context, out var position, out var rotation);
        var projectile = Instantiate(_projectilePrefab, position, rotation);
        projectile.InitializeVisual(context.Owner, context.AimDirection, _throwForce);
    }

    // 원래 카메라 기준 계산을 조준값으로 대체 (cameraTransform.up → 조준 방향 기준 위쪽)
    public void GetSpawnPose(in ItemUseContext context, out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.LookRotation(context.AimDirection, Vector3.up);
        Vector3 up = rotation * Vector3.up;
        position = context.AimOrigin + context.AimDirection * _spawnForwardOffset - up * _spawnDownOffset;
        // Do not spawn the bundle behind a nearby wall because of the forward offset.
        Vector3 offset = position - context.AimOrigin;
        float distance = offset.magnitude;
        foreach (var hit in Physics.SphereCastAll(context.AimOrigin, CollisionRadius, offset.normalized,
            distance, ~0, QueryTriggerInteraction.Ignore))
            if (BeanMugwortProjectile.BlocksFlight(hit.collider, context.Owner)) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .01f));
        position = context.AimOrigin + offset.normalized * distance;
    }
}
