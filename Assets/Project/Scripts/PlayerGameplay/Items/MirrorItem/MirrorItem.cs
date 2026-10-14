using UnityEngine;

public class MirrorItem : ItemBase
{
    [Header("Observation")]
    [SerializeField]
    private MirrorObservationView _observationView;


    public override void OnUseStarted()
    {
        if (Owner == null ||
            !PlayerActionGuard.CanAct(Owner))
        {
            return;
        }

        if (_observationView == null)
        {
            _observationView =
                GetComponentInChildren<MirrorObservationView>(true);
        }

        if (_observationView == null)
        {
            Debug.LogWarning(
                "[Mirror] MirrorObservationView를 찾을 수 없습니다.",
                this
            );

            return;
        }

        _observationView.SetLive(true);

        Debug.Log("[Mirror] LIVE");
    }


    public override void OnUseCanceled()
    {
        if (_observationView == null)
        {
            return;
        }

        _observationView.SetLive(false);

        Debug.Log("[Mirror] FREEZE");
    }
}