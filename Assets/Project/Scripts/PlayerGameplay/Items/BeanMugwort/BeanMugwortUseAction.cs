using UnityEngine;

[CreateAssetMenu(fileName = "BeanMugwortUseAction", menuName = "Project Shaman/Item Use/Bean Mugwort")]
public class BeanMugwortUseAction : ItemUseAction
{
    [Header("Throw")]
    [SerializeField] private BeanMugwortProjectile _projectilePrefab;
    [SerializeField] private float _throwForce = 8f;
    [SerializeField] private float _spawnForwardOffset = .6f;
    [SerializeField] private float _spawnDownOffset = .15f;

    [Header("Effect")]
    [SerializeField, Range(.1f, 1f)] private float _slowMultiplier = .6f;
    [SerializeField] private float _slowDuration = 3f;

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
        projectile.Initialize(context.Owner, context.AimDirection, _throwForce, _slowMultiplier, _slowDuration);

        Debug.Log($"[BeanMugwort] 투척 판정 / Slow={_slowMultiplier}, Duration={_slowDuration}", context.Owner);

        if (_consumeOnThrow) context.Consume?.Invoke(1);
        // Only the resolving projectile counts. Visual projectiles and later contacts do not report.
        PlayerItemActions.ReportUse(context.Owner, context.Data);
        return true;
    }

    // [다른 클라] 연출용 투척물
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        context.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(context.Data);
        Debug.Log($"[BeanMugwort] 원격 연출: {context.Owner.name}", context.Owner);
        SpawnVisualProjectile(context);
    }

    // [사용한 클라] BeanMugwortItem.OnUseStarted에서 즉시 호출 / [다른 클라] 위 Rpc에서 호출
    public void SpawnVisualProjectile(in ItemUseContext context)
    {
        if (_projectilePrefab == null) return;

        GetSpawnPose(context, out var position, out var rotation);
        var projectile = Instantiate(_projectilePrefab, position, rotation);
        projectile.InitializeVisual(context.Owner, context.AimDirection, _throwForce);
    }

    // 원래 카메라 기준 계산을 조준값으로 대체 (cameraTransform.up → 조준 방향 기준 위쪽)
    private void GetSpawnPose(in ItemUseContext context, out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.LookRotation(context.AimDirection, Vector3.up);
        Vector3 up = rotation * Vector3.up;
        position = context.AimOrigin + context.AimDirection * _spawnForwardOffset - up * _spawnDownOffset;
    }
}
