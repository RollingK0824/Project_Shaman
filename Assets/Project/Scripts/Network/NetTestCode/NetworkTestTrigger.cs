using UnityEngine;
using Mirror;

public class NetworkTestTrigger : MonoBehaviour
{
    private NetGM _gameNetwork;

    void Start()
    {
        _gameNetwork = FindFirstObjectByType<NetGM>();

        // TimeManager는 이미 public 이벤트가 있으니 그냥 구독해서 로그만 찍음
        TimeManager.Instance.OnDayStart += () =>
            Debug.Log(
                $"[TEST] Day 시작 " +
                $"(Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        TimeManager.Instance.OnNightStart += () =>
            Debug.Log(
                $"[TEST] Night 시작 " +
                $"(Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        TimeManager.Instance.OnNewDay += d =>
            Debug.Log(
                $"[TEST] Day {d}로 증가 " +
                $"(Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        GameManager.Instance.OnWin += () => Debug.Log($"[TEST] 승리! (Server={NetworkServer.active})");
        GameManager.Instance.OnLose += () => Debug.Log($"[TEST] 패배! (Server={NetworkServer.active})");
    }

    void Update()
    {
        if (!NetworkServer.active) return; // 호스트(서버)에서만 테스트 입력 허용

        //if (Input.GetKeyDown(KeyCode.Alpha9))
        //    _gameNetwork.ServerReportGhostExorcised();
        //
        //if (Input.GetKeyDown(KeyCode.Alpha0))
        //    _gameNetwork.ServerReportNpcDied();
    }
}
