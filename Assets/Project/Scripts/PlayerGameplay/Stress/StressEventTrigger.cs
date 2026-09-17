using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(StressEventSource))]
public class StressEventTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private bool _oncePerEntry = true;

    private StressEventSource _stressEventSource;
    private bool _hasTriggered;

    private void Awake()
    {
        _stressEventSource =
            GetComponent<StressEventSource>();
    }

    private void Reset()
    {
        Collider triggerCollider =
            GetComponent<Collider>();

        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_oncePerEntry &&
            _hasTriggered)
        {
            return;
        }

        IStressReceiver receiver =
            other.GetComponentInParent<IStressReceiver>();

        if (receiver == null)
        {
            return;
        }

        _stressEventSource.ApplyTo(
            other.gameObject
        );

        _hasTriggered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        IStressReceiver receiver =
            other.GetComponentInParent<IStressReceiver>();

        if (receiver == null)
        {
            return;
        }

        if (_oncePerEntry)
        {
            _hasTriggered = false;
        }
    }
}