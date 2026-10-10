using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StressZone : MonoBehaviour
{
    public enum StressZoneType { Increase, Reduce }
    [Header("Stress Zone")]
    [SerializeField] private StressZoneType _zoneType = StressZoneType.Increase;
    [SerializeField, Min(0f)] private float _stressPerSecond = 10f;
    [Tooltip("Exposure classification; safe zones always recover stress.")]
    [SerializeField] private StressCause _cause = StressCause.Darkness;
    public StressCause Cause => _cause;

    private readonly Dictionary<PlayerStressController, HashSet<Collider>> _occupants = new();
    private readonly List<PlayerStressController> _leaving = new();
    private void Reset() => GetComponent<Collider>().isTrigger = true;
    private void OnValidate() => _stressPerSecond = Mathf.Max(0f, _stressPerSecond);

    private void Track(Collider other)
    {
        if (!isActiveAndEnabled) return;
        var player = other.GetComponentInParent<PlayerStressController>();
        if (player == null || !player.isActiveAndEnabled) return;
        if (!_occupants.TryGetValue(player, out var colliders))
        {
            colliders = new HashSet<Collider>();
            _occupants.Add(player, colliders);
        }
        colliders.Add(other);
        RegisterRate(player);
    }

    private void RegisterRate(PlayerStressController player)
    {
        player.SetSourceRate(this, _zoneType == StressZoneType.Reduce ? -_stressPerSecond : _stressPerSecond);
    }

    private void OnTriggerEnter(Collider other) => Track(other);
    private void OnTriggerStay(Collider other) => Track(other);

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<PlayerStressController>();
        if (player == null || !_occupants.TryGetValue(player, out var colliders)) return;
        colliders.Remove(other);
        if (colliders.Count == 0)
        {
            player.RemoveSource(this);
            _occupants.Remove(player);
        }
    }

    private void FixedUpdate()
    {
        var zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.enabled || !zoneCollider.isTrigger) { ClearOccupants(); return; }
        _leaving.Clear();
        foreach (var entry in _occupants)
        {
            // Disabling a collider while teleporting can skip OnTriggerExit.
            // Bounds rejection only removes definitely separated colliders.
            entry.Value.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy
                || !zoneCollider.bounds.Intersects(c.bounds));
            if (entry.Key == null || !entry.Key.isActiveAndEnabled || entry.Value.Count == 0)
                _leaving.Add(entry.Key);
            else RegisterRate(entry.Key);
        }
        foreach (var player in _leaving)
        {
            if (player != null) player.RemoveSource(this);
            _occupants.Remove(player);
        }
    }

    private void ClearOccupants()
    {
        foreach (var player in _occupants.Keys)
            if (player != null) player.RemoveSource(this);
        _occupants.Clear();
    }
    private void OnDisable() => ClearOccupants();
}
