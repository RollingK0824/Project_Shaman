using UnityEngine;
using Mirror;

public class NetTimeManager : NetworkBehaviour
{
    public struct SpeedState
    {
        public float Speed;
        public double RebaseTime;
        public double ElapsedAtRebase;
    }

    [SyncVar(hook = nameof(OnCycleStartChanged))]
    private double _cycleStartTime;

    // 테스트 빨리 감기. 늦게 들어온 클라도 같은 시계가 되도록 기준 시각과 그때까지 흐른 시간을 같이 보냄
    [SyncVar(hook = nameof(OnSpeedChanged))]
    private SpeedState _speed;


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

    // [호스트] F1 테스트 패널의 빨리 감기. 지금까지 흐른 시간에서 이어서 speed배로 흐름
    [Server]
    public void ServerSetSpeed(float speed)
    {
        TimeManager time = TimeManager.Instance;
        if (time == null || !time.IsRunning)
        {
            return;
        }

        double now = NetworkTime.time;
        _speed = new SpeedState { Speed = speed, RebaseTime = now, ElapsedAtRebase = time.ElapsedAt(now) };
        time.SetSpeed(_speed.Speed, _speed.RebaseTime, _speed.ElapsedAtRebase);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        OnCycleStartChanged(0, _cycleStartTime);
        OnSpeedChanged(default, _speed);
    }

    public override void OnStopClient()
    {
        Time.timeScale = 1f;
        base.OnStopClient();
    }

    private void OnCycleStartChanged(double oldVal, double newVal)
    {
        if (newVal <= 0)
        {
            return;
        }

        TimeManager.Instance.SetCycleStart(newVal);
    }

    private void OnSpeedChanged(SpeedState oldVal, SpeedState newVal)
    {
        if (newVal.Speed <= 0f || isServer)
        {
            return;
        }

        TimeManager.Instance.SetSpeed(newVal.Speed, newVal.RebaseTime, newVal.ElapsedAtRebase);
    }
}
