using UnityEngine;

public class BeanMugwortItem : ItemBase
{
    [Header("Throw")]
    [SerializeField]
    private BeanMugwortProjectile _projectilePrefab;

    [SerializeField]
    private float _throwForce = 8f;

    [SerializeField]
    private float _spawnForwardOffset = 0.6f;

    [SerializeField]
    private float _spawnDownOffset = 0.15f;

    [SerializeField]
    private float _reuseDelay = 0.5f;

    [Header("Effect")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float _slowMultiplier = 0.6f;

    [SerializeField]
    private float _slowDuration = 3f;

    [Header("Consumption")]
    [SerializeField]
    private bool _consumeOnThrow = true;

    private float _nextUseTime;


    public override void OnUseStarted()
    {
        if (Owner == null ||
            !PlayerActionGuard.CanAct(Owner))
        {
            return;
        }

        if (!ItemAttackAuthority.CanResolve(Owner)) return;

        if (Time.time < _nextUseTime)
        {
            return;
        }

        if (_projectilePrefab == null)
        {
            Debug.LogWarning(
                "[BeanMugwort] Projectile Prefab이 연결되지 않았습니다.",
                this
            );

            return;
        }

        Camera ownerCamera =
            Owner.GetComponentInChildren<Camera>(true);

        if (ownerCamera == null)
        {
            Debug.LogWarning(
                "[BeanMugwort] 플레이어 카메라를 찾을 수 없습니다.",
                this
            );

            return;
        }

        _nextUseTime =
            Time.time + _reuseDelay;

        ThrowProjectile(ownerCamera);

        if (_consumeOnThrow)
        {
            ConsumeOne();
        }
        PlayerItemActions.ReportUse(Owner, Data);
    }


    private void ThrowProjectile(
        Camera ownerCamera)
    {
        Transform cameraTransform =
            ownerCamera.transform;

        Vector3 spawnPosition =
            cameraTransform.position +
            cameraTransform.forward *
            _spawnForwardOffset -
            cameraTransform.up *
            _spawnDownOffset;

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                cameraTransform.forward,
                Vector3.up
            );

        BeanMugwortProjectile projectile =
            Instantiate(
                _projectilePrefab,
                spawnPosition,
                spawnRotation
            );

        projectile.Initialize(
            Owner,
            cameraTransform.forward,
            _throwForce,
            _slowMultiplier,
            _slowDuration
        );

        Debug.Log(
            $"[BeanMugwort] 투척 / " +
            $"Slow={_slowMultiplier}, " +
            $"Duration={_slowDuration}"
        );
    }


    private void ConsumeOne()
    {
        PlayerInventory inventory =
            Owner.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogWarning(
                "[BeanMugwort] PlayerInventory를 찾을 수 없습니다.",
                this
            );

            return;
        }

        inventory.ConsumeItem(
            Data,
            1
        );
    }
}