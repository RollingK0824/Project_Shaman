using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StressZone : MonoBehaviour
{
    public enum StressZoneType
    {
        Increase,
        Reduce
    }

    [Header("Stress Zone")]
    [SerializeField]
    private StressZoneType _zoneType =
        StressZoneType.Increase;

    [SerializeField] private float _stressPerSecond = 10f;

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        if (_stressPerSecond < 0f)
        {
            _stressPerSecond = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerStressController stressController =
            other.GetComponentInParent<PlayerStressController>();

        if (stressController == null)
        {
            return;
        }

        Debug.Log(
            $"[StressZone] Player 진입: {gameObject.name} / " +
            $"Type = {_zoneType}",
            gameObject
        );
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerStressController stressController =
            other.GetComponentInParent<PlayerStressController>();

        if (stressController == null)
        {
            return;
        }

        float amount =
            _stressPerSecond * Time.deltaTime;

        // 직접 수정 x / 서버에 요청보내기
        float delta = _zoneType == StressZoneType.Increase ? amount : -amount;

        PlayerEvents.RaiseStressRequested(stressController, delta, StressCause.Other, gameObject);

        //switch (_zoneType)
        //{
        //    case StressZoneType.Increase:
        //        stressController.AddStress(amount);
        //        break;
        //
        //    case StressZoneType.Reduce:
        //        stressController.ReduceStress(amount);
        //        break;
        //}
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerStressController stressController =
            other.GetComponentInParent<PlayerStressController>();

        if (stressController == null)
        {
            return;
        }

        Debug.Log(
            $"[StressZone] Player 이탈: {gameObject.name} / " +
            $"Type = {_zoneType}",
            gameObject
        );
    }
}