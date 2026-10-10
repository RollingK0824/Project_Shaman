using System.Collections.Generic;
using UnityEngine;

public class CompassItem : ItemBase
{
    [Header("Compass")]
    [SerializeField]
    private Transform _needlePivot;

    [SerializeField]
    [Min(0f)]
    private float _needleTurnSpeed = 240f;

    [SerializeField]
    private float _needleAngleOffset;

    private bool _isEquipped;

    public override void OnEquipped()
    {
        _isEquipped = true;

        if (Data != null)
        {
            Debug.Log(
                $"[Compass] 장착: {Data.DisplayName}",
                gameObject
            );
        }
    }

    public override void OnUnequipped()
    {
        _isEquipped = false;

        if (Data != null)
        {
            Debug.Log(
                $"[Compass] 장착 해제: {Data.DisplayName}",
                gameObject
            );
        }
    }

    public override void OnUseStarted()
    {
        if (!PlayerActionGuard.CanAct(Owner)) return;
        // 나침반은 클릭형 아이템이 아니라
        // 장착 중 지속적으로 방향을 표시한다.
    }

    public override void OnUseCanceled()
    {
    }

    private void Update()
    {
        if (!_isEquipped ||
            Owner == null ||
            _needlePivot == null)
        {
            return;
        }

        Vector3 signalDirection =
            CalculateSignalDirection();

        // 감지되는 음기가 없으면
        // 현재 바늘 방향을 유지한다.
        if (signalDirection.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        // 나침반은 수평 방향만 사용한다.
        signalDirection.y = 0f;

        if (signalDirection.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        signalDirection.Normalize();

        // 월드 방향을 현재 나침반의
        // 로컬 방향으로 변환한다.
        Vector3 localDirection =
            transform.InverseTransformDirection(
                signalDirection
            );

        float targetAngle =
            Mathf.Atan2(
                localDirection.x,
                localDirection.z
            ) *
            Mathf.Rad2Deg;

        targetAngle +=
            _needleAngleOffset;

        Quaternion targetRotation =
            Quaternion.Euler(
                0f,
                targetAngle,
                0f
            );

        _needlePivot.localRotation =
            Quaternion.RotateTowards(
                _needlePivot.localRotation,
                targetRotation,
                _needleTurnSpeed *
                Time.deltaTime
            );
    }

    private Vector3 CalculateSignalDirection()
    {
        Vector3 combinedDirection =
            Vector3.zero;

        IReadOnlyList<YinEnergySource> sources =
            YinEnergySource.ActiveSources;

        for (int i = 0;
             i < sources.Count;
             i++)
        {
            YinEnergySource source =
                sources[i];

            if (source == null)
            {
                continue;
            }

            Vector3 direction =
                source.transform.position -
                Owner.transform.position;

            // 상하 위치는 나침반 판정에서 제외한다.
            direction.y = 0f;

            if (direction.sqrMagnitude <
                0.0001f)
            {
                continue;
            }

            float influence =
                source.GetInfluence(
                    Owner.transform.position
                );

            if (influence <= 0f)
            {
                continue;
            }

            combinedDirection +=
                direction.normalized *
                influence;
        }

        return combinedDirection;
    }
}