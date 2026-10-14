using UnityEngine;
using Mirror;

public class NetTimeManager : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCycleStartChanged))]
    private double _cycleStartTime;


    public override void OnStartServer()
    {
        base.OnStartServer();

        // 게이트가 있으면 게이트가 정한 시작 시각에 시작. 없는 씬(테스트)에서는 기존처럼 바로 시작
        if (NetGameStartGate.Instance == null)
        {
            ServerSetCycleStart(NetworkTime.time);
        }
    }

    [Server]
    public void ServerSetCycleStart(double startTime)
    {
        // 게임 시작 시점을 기준으로 고정시키기
        _cycleStartTime = startTime;

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
