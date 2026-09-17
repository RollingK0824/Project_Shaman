using UnityEngine;

public class StressEventSource : MonoBehaviour
{
    [Header("Stress Event")]
    [SerializeField]
    private StressCause _cause =
        StressCause.Unknown;

    [SerializeField] private float _stressAmount = 20f;

    public StressCause Cause =>
        _cause;

    public float StressAmount =>
        _stressAmount;

    public void ApplyTo(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        IStressReceiver receiver =
            target.GetComponentInParent<IStressReceiver>();

        if (receiver == null)
        {
            Debug.LogWarning(
                $"[Stress] {target.name}에 " +
                "IStressReceiver가 없습니다.",
                target
            );

            return;
        }

        receiver.ReceiveStress(
            _stressAmount,
            _cause,
            gameObject
        );
    }

    private void OnValidate()
    {
        if (_stressAmount < 0f)
        {
            _stressAmount = 0f;
        }
    }
}