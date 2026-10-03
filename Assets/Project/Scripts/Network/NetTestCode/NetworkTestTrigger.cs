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

    private GameManager _gameManager;
    private GUIStyle _resultStyle;

    // 테스트 패널 (F1)
    private bool _panelOpen;
    private bool _lookBeforePanel;

    private NetGM GameNetwork
    {
        get
        {
            if (_gameNetwork == null) _gameNetwork = FindFirstObjectByType<NetGM>();
            return _gameNetwork;
        }
    }

    private GameObject LocalPlayer => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;

    void Start()
    {
        _gameManager = GameManager.Instance;
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
        if (!Debug.isDebugBuild) return;
        if (Input.GetKeyDown(KeyCode.F1)) TogglePanel();

        //// 모두 (자기 캐릭터 피해 요청,, 판정은 서버)
        //if (Input.GetKeyDown(KeyCode.Alpha7)) RequestSelfDamage(false);
        //if (Input.GetKeyDown(KeyCode.Alpha8)) RequestSelfDamage(true);
        //
        //// 호스트 전용 ( 각 함수 내에서 서버 여부 확인)
        //if (Input.GetKeyDown(KeyCode.Alpha9)) ServerSpawnItem();
        //if (Input.GetKeyDown(KeyCode.Alpha0)) ServerDespawnAllItems();
        //if (Input.GetKeyDown(KeyCode.Minus)) ServerExorciseGhost();
    }

    // ---- 테스트 동작 ----

    // [모두] 7: 25피해, 8: 즉사 피해. 서버에 요청만 보내는 방식
    private void RequestSelfDamage(bool kill)
    {
        if (!NetworkClient.active || !NetworkClient.ready) return;

        NetworkClient.Send(new TestSelfDamageMessage { kill = kill });
    }

    // [호스트] 9 : 랜덤 아이템 하나 스폰
    private void ServerSpawnItem()
    {
        if (!NetworkServer.active || _itemSpawner == null) return;

        _itemSpawner.GetComponent<ItemSpawner>().RequestSpawn();
    }


    // [호스트] 0: 스포너 아이템 전체 회수
    private void ServerDespawnAllItems()
    {
        if (!NetworkServer.active || _itemSpawner == null) return;

        _itemSpawner.ServerDespawnAll();
    }

    // [호스트] -: 귀신 하나 퇴마. 귀신 수만큼 퇴마하면 승리
    private void ServerExorciseGhost()
    {
        if (!NetworkServer.active || GameNetwork == null) return;

        Debug.Log("[TEST] 귀신 퇴마 처리");
        GameNetwork.ServerReportGhostExorcised();
    }


    // ---- 테스트 패널 ----
    
    // 패널 열면 커서를 풀고 시점 회전만 멈춤. 닫으면 원래대로
    private void TogglePanel()
    {
        _panelOpen = !_panelOpen;

        PlayerCameraController look = LocalPlayer != null ? LocalPlayer.GetComponent<PlayerCameraController>() : null;
        PlayerHealth health = LocalPlayer != null ? LocalPlayer.GetComponent<PlayerHealth>() : null;

        if (_panelOpen)
        {
            if (look != null)
            {
                _lookBeforePanel = look.CanLook;
                look.CanLook = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (look != null) look.CanLook = _lookBeforePanel && (health == null || !health.IsDead);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }



    void OnGUI()
    {
        DrawResult();

        if (!Debug.isDebugBuild) return;

        if (!_panelOpen)
        {
            GUI.Label(new Rect(10, 10, 200, 22), "F1: 테스트 패널");
            return;
        }

        DrawPanel();
    }

    private void DrawPanel()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 360), GUI.skin.box);
        GUILayout.Label("테스트 패널 (F1 닫기)");

        // 상태
        string role = NetworkServer.active ? "호스트" : "클라";
        string transport = Transport.active != null ? Transport.active.GetType().Name : "-";
        GUILayout.Label($"{role} / {transport}");

        if (_gameManager != null)
        {
            GUILayout.Label($"게임: {_gameManager.CurrentGameState} 귀신 {_gameManager.GhostCount} NPC {_gameManager.NpcCount}/{_gameManager.NpcTotal}");
        }

        PlayerHealth health = LocalPlayer != null ? LocalPlayer.GetComponent<PlayerHealth>() : null;

        if (health != null)
        {
            string dead = health.IsDead ? "  (사망)" : "";
            GUILayout.Label($"내 HP {health.CurrentHealth:0} / {health.MaxHealth:0}{dead}");
        }

        if (NetworkServer.active)
        {
            GUILayout.Label($"참가 {PlayerRoster.Players.Count} / 생존 {PlayerRoster.AliveCount}");
        }


        // 모두
        GUILayout.Space(6);
        GUILayout.Label("── 모두 ──");
        if (GUILayout.Button("7  자기 25 피해")) RequestSelfDamage(false);
        if (GUILayout.Button("8  자기 즉사")) RequestSelfDamage(true);

        // 호스트 전용
        GUILayout.Space(6);
        GUILayout.Label("── 호스트 전용 ──");
        GUI.enabled = NetworkServer.active;
        if (GUILayout.Button("9  아이템 스폰")) ServerSpawnItem();
        if (GUILayout.Button("0  아이템 전부 회수")) ServerDespawnAllItems();
        if (GUILayout.Button("-  귀신 퇴마")) ServerExorciseGhost();
        GUI.enabled = true;

        GUILayout.EndArea();
    }

    // [테스트] 승패가 나면 모든 PC 화면에 결과롸 로비 복귀 카운트다운 포시.. 정식 결과 화면이 생기면 추후에 삭제
    private void DrawResult()
    {
        if (_gameManager == null || !_gameManager.IsGameOver) return;

        if (_resultStyle == null)
        {
            _resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter
            };
        }

        string result = _gameManager.CurrentGameState == GameState.Win ? "승리" : "패배";

        double remaining = 0;
        if (GameNetwork != null && GameNetwork.ReturnToLobbyTime > 0)
        {
            remaining = System.Math.Max(0, GameNetwork.ReturnToLobbyTime - NetworkTime.time);
        }

        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 140), $"{result}\n{System.Math.Ceiling(remaining):0}초 후 로비로 돌아갑니다.", _resultStyle);
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
