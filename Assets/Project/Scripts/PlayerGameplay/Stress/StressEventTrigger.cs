using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StressEventTrigger : MonoBehaviour
{
    [Header("Stress Event")]
    [SerializeField] private StressCause _cause = StressCause.Unknown;
    [SerializeField] private float _stressAmount = 20f;

    [Header("Trigger")]
    [SerializeField] private bool _oncePerEntry = true;

    private bool _hasTriggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        if (_stressAmount < 0f)
        {
            _stressAmount = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_oncePerEntry && _hasTriggered)
        {
            return;
        }

        PlayerStressController stressController = other.GetComponentInParent<PlayerStressController>();

        if (stressController == null)
        {
            return;
        }

        // 서버에 요청 보내는 거로 수정
        PlayerEvents.RaiseStressRequested(stressController, _stressAmount, _cause, gameObject);

        //IStressReceiver receiver = other.GetComponentInParent<IStressReceiver>();
        //if (receiver == null)
        //{
        //    return;
        //}
        //
        //receiver.ReceiveStress(
        //    _stressAmount,
        //    _cause,
        //    gameObject
        //);

        _hasTriggered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_oncePerEntry)
        {
            return;
        }

        IStressReceiver receiver = other.GetComponentInParent<IStressReceiver>();
        if (receiver != null)
        {
            _hasTriggered = false;
        }
    }
}
