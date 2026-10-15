using System.Collections.Generic;
using UnityEngine;

public class YinEnergySource : MonoBehaviour
{
    private static readonly List<YinEnergySource> _activeSources =
        new List<YinEnergySource>();

    [Header("Yin Energy")]
    [SerializeField]
    [Min(0f)]
    private float _strength = 1f;

    [SerializeField]
    [Min(0.1f)]
    private float _effectiveRadius = 15f;

    public static IReadOnlyList<YinEnergySource> ActiveSources =>
        _activeSources;

    public float Strength => _strength;
    public float EffectiveRadius => _effectiveRadius;

    private void OnEnable()
    {
        if (!_activeSources.Contains(this))
        {
            _activeSources.Add(this);
        }
    }

    private void OnDisable()
    {
        _activeSources.Remove(this);
    }

    public float GetInfluence(
        Vector3 observerPosition)
    {
        float distance =
            Vector3.Distance(
                observerPosition,
                transform.position
            );

        if (distance > _effectiveRadius)
        {
            return 0f;
        }

        float normalizedDistance =
            Mathf.Clamp01(
                distance / _effectiveRadius
            );

        float distanceFactor =
            1f - normalizedDistance;

        return
            _strength *
            distanceFactor *
            distanceFactor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            _effectiveRadius
        );
    }
}