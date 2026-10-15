using UnityEngine;

public class YinIncenseDetector : MonoBehaviour
{
    [Header("Smoke Direction")]
    [SerializeField]
    [Min(0f)]
    private float _horizontalForce = 0.25f;

    [SerializeField]
    [Min(0f)]
    private float _directionSmoothSpeed = 3f;

    private ParticleSystem _smoke;
    private Vector3 _currentDirection;


    private void Awake()
    {
        _smoke =
            GetComponentInChildren<ParticleSystem>(true);

        if (_smoke == null)
        {
            Debug.LogError(
                "[YinIncense] ParticleSystem을 찾지 못했습니다.",
                this
            );
        }
    }


    private void Update()
    {
        if (_smoke == null)
        {
            return;
        }

        UpdateSmokeDirection();
    }


    private void UpdateSmokeDirection()
    {
        YinEnergySource source =
            FindStrongestSource();

        Vector3 targetDirection =
            Vector3.zero;

        if (source != null)
        {
            targetDirection =
                source.transform.position -
                _smoke.transform.position;

            // 음기의 높이는 무시하고
            // 지면 기준 방향만 사용한다.
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.001f)
            {
                targetDirection.Normalize();
            }
        }

        _currentDirection =
            Vector3.Lerp(
                _currentDirection,
                targetDirection,
                _directionSmoothSpeed * Time.deltaTime
            );

        ApplySmokeForce();
    }


    private void ApplySmokeForce()
    {
        ParticleSystem.ForceOverLifetimeModule force =
            _smoke.forceOverLifetime;

        force.enabled = true;

        // Particle 자체는 Local Simulation을 사용하지만
        // 음기 방향은 월드 기준으로 적용한다.
        force.space =
            ParticleSystemSimulationSpace.World;

        force.x =
            new ParticleSystem.MinMaxCurve(
                _currentDirection.x *
                _horizontalForce
            );

        force.z =
            new ParticleSystem.MinMaxCurve(
                _currentDirection.z *
                _horizontalForce
            );
    }


    private YinEnergySource FindStrongestSource()
    {
        YinEnergySource strongestSource = null;
        float strongestInfluence = 0f;

        foreach (
            YinEnergySource source
            in YinEnergySource.ActiveSources)
        {
            if (source == null)
            {
                continue;
            }

            float influence =
                source.GetInfluence(
                    transform.position
                );

            if (influence <= strongestInfluence)
            {
                continue;
            }

            strongestInfluence =
                influence;

            strongestSource =
                source;
        }

        return strongestSource;
    }
}