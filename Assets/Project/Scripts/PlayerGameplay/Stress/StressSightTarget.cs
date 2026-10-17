using System.Collections.Generic;
using UnityEngine;

// Opt-in contract for an actually active anomaly visual. Never marks a hidden possessed NPC.
public sealed class StressSightTarget : MonoBehaviour
{
    [SerializeField] private StressCause _cause = StressCause.AnomalySight;
    [SerializeField, Min(0f)] private float _stressAmount = 20f;
    [SerializeField] private Transform _sightPoint;
    [SerializeField] private bool _sightActive = true;
    private static readonly HashSet<StressSightTarget> Targets = new();
    public static IEnumerable<StressSightTarget> ActiveTargets => Targets;
    public StressCause Cause => _cause;
    public float StressAmount => _stressAmount;
    public Transform SightPoint => _sightPoint;
    public bool SightActive => _sightActive && isActiveAndEnabled;
    public void SetSightActive(bool active) => _sightActive = active;
    private void OnEnable() => Targets.Add(this);
    private void OnDisable() => Targets.Remove(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Targets.Clear();
}
