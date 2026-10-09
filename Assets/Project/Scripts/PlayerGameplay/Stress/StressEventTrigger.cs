using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StressEventTrigger : MonoBehaviour
{
    [Header("Stress Event")]
    [SerializeField] private StressCause _cause = StressCause.Unknown;
    [SerializeField, Min(0f)] private float _stressAmount = 20f;
    [Header("Trigger")]
    [SerializeField] private bool _oncePerEntry = true;
    private readonly Dictionary<Component, HashSet<Collider>> _occupants = new();
    private readonly List<Component> _leaving = new();

    private void Reset() => GetComponent<Collider>().isTrigger = true;
    private void OnValidate() => _stressAmount = Mathf.Max(0f, _stressAmount);

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled) return;
        var receiver = other.GetComponentInParent<IStressReceiver>();
        var component = receiver as Component;
        if (component == null) return;
        if (!_occupants.TryGetValue(component, out var colliders))
        {
            colliders = new HashSet<Collider>();
            _occupants.Add(component, colliders);
        }
        bool alreadyInside = colliders.Count > 0;
        if (!colliders.Add(other) || (_oncePerEntry && alreadyInside)) return;
        receiver.ReceiveStress(_stressAmount, _cause, gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        var component = other.GetComponentInParent<IStressReceiver>() as Component;
        if (component == null || !_occupants.TryGetValue(component, out var colliders)) return;
        colliders.Remove(other);
        if (colliders.Count == 0) _occupants.Remove(component);
    }

    private void FixedUpdate()
    {
        var trigger = GetComponent<Collider>();
        if (!trigger.enabled || !trigger.isTrigger) { _occupants.Clear(); return; }
        _leaving.Clear();
        foreach (var entry in _occupants)
        {
            // Disabling a collider while teleporting can skip OnTriggerExit.
            // Bounds rejection only removes definitely separated colliders.
            entry.Value.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy
                || !trigger.bounds.Intersects(c.bounds));
            if (entry.Key == null || entry.Value.Count == 0) _leaving.Add(entry.Key);
        }
        foreach (var receiver in _leaving) _occupants.Remove(receiver);
    }
    private void OnDisable() => _occupants.Clear();
}
