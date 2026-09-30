using UnityEngine;
using Mirror;

public struct TestSelfDamageMessage : NetworkMessage
{
    public bool kill;
}

public class NetworkTestTrigger : MonoBehaviour
{
    private NetGM _gameNetwork;
    private NetItemSpawner _itemSpawner;

    void Start()
    {
        _gameNetwork = FindFirstObjectByType<NetGM>();
        _itemSpawner = FindFirstObjectByType<NetItemSpawner>();

        if (NetworkServer.active)
            NetworkServer.ReplaceHandler<TestSelfDamageMessage>(OnTestSelfDamage);

        // TimeManager는 이미 public 이벤트가 있으니 그냥 구독해서 로그만 찍음
        TimeManager.Instance.OnDayStart += () =>
            Debug.Log($"[TEST] Day 시작 (Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        TimeManager.Instance.OnNightStart += () =>
            Debug.Log($"[TEST] Night 시작 (Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        TimeManager.Instance.OnNewDay += d =>
            Debug.Log($"[TEST] Day {d}로 증가 (Server={NetworkServer.active}, Time={NetworkTime.time:F2})");

        GameManager.Instance.OnWin += () => Debug.Log($"[TEST] 승리! (Server={NetworkServer.active})");
        GameManager.Instance.OnLose += () => Debug.Log($"[TEST] 패배! (Server={NetworkServer.active})");
    }

    void OnDestroy()
    {
        NetworkServer.UnregisterHandler<TestSelfDamageMessage>();
    }

    void Update()
    {
        // // 호스트(서버)에서만 테스트 입력 허용

        // 7: 자기 캐릭터 25 피해, 8: 자기 캐릭터 즉사 호스트,클라이언트 모두 서버에 요청만 보냄
        if (NetworkClient.active && NetworkClient.ready)
        {
            if (Input.GetKeyDown(KeyCode.Alpha7))
                NetworkClient.Send(new TestSelfDamageMessage { kill = false });

            if (Input.GetKeyDown(KeyCode.Alpha8))
                NetworkClient.Send(new TestSelfDamageMessage { kill = true });
        }

        if (!NetworkServer.active) return;

        // 9: 서버에서 랜덤 아이템 하나 스폰 0: 해당 스포너의 아이템 전부 회수
        if (Input.GetKeyDown(KeyCode.Alpha9) && _itemSpawner != null)
            _itemSpawner.GetComponent<ItemSpawner>().RequestSpawn();

        if (Input.GetKeyDown(KeyCode.Alpha0) && _itemSpawner != null)
            _itemSpawner.ServerDespawnAll();
    }

    private void OnTestSelfDamage(NetworkConnectionToClient conn, TestSelfDamageMessage msg)
    {
        if (!Debug.isDebugBuild) return;
        if (conn.identity == null) return;

        PlayerHealth health = conn.identity.GetComponent<PlayerHealth>();
        NetPlayerStatus status = conn.identity.GetComponent<NetPlayerStatus>();

        if (health == null) return;

        float amount = msg.kill ? health.MaxHealth : 25f;
        string playerName = status != null ? status.PlayerName : conn.identity.name;

        Debug.Log($"[TEST] {playerName} 자기 피해 요청 {amount: 0.##}");
        PlayerEvents.RaiseDamageRequested(health, amount, null);
    }
}
