using UnityEngine;
using Mirror;

public class NetworkTestTrigger : MonoBehaviour
{
    private NetGM _gameNetwork;
    private NetItemSpawner _itemSpawner;

    void Start()
    {
        _gameNetwork = FindFirstObjectByType<NetGM>();
        _itemSpawner = FindFirstObjectByType<NetItemSpawner>();

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

        // 8: 살아 있는 첫 플레이어를 서버의 PlayerHealth API로 사망 처리
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            foreach (NetPlayerStatus player in PlayerRoster.Players)
            {
                if (player.IsDead) continue;

                var health = player.GetComponent<PlayerHealth>();
                if (health == null) continue;

                Debug.Log($"[TEST] {player.PlayerName} 사망 처리");
                health.SetHealth(0f);
                break;
            }
        }

        // 9: 서버에서 랜덤 아이템 하나 스폰. 0: 해당 스포너의 아이템 전부 회수.
        if (Input.GetKeyDown(KeyCode.Alpha9) && _itemSpawner != null)
            _itemSpawner.GetComponent<ItemSpawner>().RequestSpawn();

        if (Input.GetKeyDown(KeyCode.Alpha0) && _itemSpawner != null)
            _itemSpawner.ServerDespawnAll();
    }
}
