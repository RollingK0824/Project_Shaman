using UnityEngine;
using Mirror;

public class NetTimeManager : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCycleStartChanged))]
    private double _cycleStartTime;


    public override void OnStartServer()
    {
        base.OnStartServer();

        // 게임 시작 시점을 기준으로 고정시키기
        _cycleStartTime = NetworkTime.time;

        TimeManager.Instance.SetCycleStart(_cycleStartTime);
    }

    

    public override void OnStartClient()
    {
        base.OnStartClient();

        OnCycleStartChanged(0, _cycleStartTime);
    }

    private void OnCycleStartChanged(double oldVal, double newVal)
    {
        if (newVal <= 0)
        {
            return;
        }

        TimeManager.Instance.SetCycleStart(newVal);
    }
}
