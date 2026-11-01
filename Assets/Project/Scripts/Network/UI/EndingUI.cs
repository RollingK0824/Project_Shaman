using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;
using System;

// 게임 종료 결과 화면 (게임 씬 전용)
// [서버·클라] GameManager가 게임 종료가 되면 표시. 수치는 NetGM 스냅샷·NetGameStartGate에서 읽음
// 로비 복귀: 호스트가 로비 버튼을 누르면 모두 이동
public class EndingUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;

    [Header("결과")]
    [SerializeField] private TMP_Text _resultMessageText;
    [SerializeField] private TMP_Text _playTimeText;
    [SerializeField] private Color _exorcisedColor = Color.white;
    [SerializeField] private Color _remainingColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("버튼")]
    [SerializeField] private Button _lobbyButton;
    [SerializeField] private TMP_Text _lobbyButtonText;

    [Header("기록")]
    [SerializeField] private TMP_Text _deadNpcText;  // 희생된 주민 값
    [SerializeField] private TMP_Text _traceText;    // 흔적 값
    [SerializeField] private TMP_Text _questText;    // 해결 의뢰 값

    [Header("원귀 현황")]
    [SerializeField] private GhostResultSlot[] _ghostResultSlots;
    [SerializeField] private string _exorcisedLabel = "퇴마";
    [SerializeField] private string _remainingLabel = "미퇴마";

    [Header("조사단")]
    [SerializeField] private PlayerResultSlot[] _playerResultSlots;
    [SerializeField] private Sprite[] _characterPortraits; // characterId 순서 (PL_1 ~ PL_4)

    [Serializable]
    private class GhostResultSlot
    {
        public GameObject root;
        public TMP_Text nameText;
        public Image stamp;
        public TMP_Text stampText;
    }

    [Serializable]
    private class PlayerResultSlot
    {
        public GameObject root;
        public Image portrait;
        public TMP_Text nameText;
        public TMP_Text stateText;
    }


    private bool _resultApplied;

    private NetGM _netGM;

    // 결과 화면이 떠 있는지(GameMenuUI가 열리지 않도록)
    public static bool IsShown { get; private set; }

    private void Start()
    {
        _panel.SetActive(false);
        _netGM = FindFirstObjectByType<NetGM>();
    }

    private void Update()
    {
        GameManager gm = GameManager.Existing;
        if (gm == null || !gm.IsGameOver) return;

        if (!IsShown)
        {
            // 마지막 퇴마면 봉인 항아리 연출이 끝난 뒤에 표시 (승리와 성공 단계는 같은 오브젝트 SyncVar라 같이 도착)
            NetDefenseState ritual = NetDefenseState.Instance;
            if (ritual != null && ritual.Phase == RitualPhase.Succeeded && ritual.RemainingTime > 0f) return;

            IsShown = true;
            _panel.SetActive(true);
            Time.timeScale = 0f; // 결과 화면 동안 AI 이동·애니메이션·물리 정지
        }

        HoldLocalPlayer();
        Refresh(gm);
    }

    // timeScale은 씬이 바뀌어도 남으므로 로비 복귀·나가기 때 되돌림
    private void OnDestroy()
    {
        if (IsShown) Time.timeScale = 1f;
        IsShown = false;
    }


    // 수첩 등이 닫히면서 입력 커서를 되돌려도 결과 화면 동안은 계속 잠금
    private static void HoldLocalPlayer()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local != null)
        {
            if (local.TryGetComponent(out PlayerInputReader input)) input.SetGameplayInputBlocked(true);
            if (local.TryGetComponent(out PlayerCameraController look)) look.CanLook = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Refresh(GameManager gm)
    {
        // 패배 사유는 GameManager 판정 규칙(주민 절반 이하 / 플레이어 전멸)과 같아야 함
        _resultMessageText.text = gm.CurrentGameState == GameState.Win ? "모든 원귀를 퇴마하였습니다." : gm.NpcCount <= gm.NpcTotal / 2 ? "마을 주민의 절반이 희생되었습니다." : "조사단이 전멸하였습니다.";


        ApplyResultOnce();

        _playTimeText.text = FormatPlayerTime();

        bool isHost = NetworkServer.active;
        _lobbyButton.interactable = isHost;
        _lobbyButtonText.text = isHost ? "로비로 이동" : "호스트 대기 중";
    }

    // 서버가 확정한 결과는 바뀌지 않으므로 받은 뒤 한 번만 채움
    private void ApplyResultOnce()
    {
        if (_resultApplied || NetGameRecord.Instance == null) return;

        GameResult result = NetGameRecord.Instance.Result;
        if (!result.valid) return; // 아직 도착 전이면 다음 프레임에 다시 확인
        _resultApplied = true;
        _deadNpcText.text = result.villagersLost.ToString();
        _traceText.text = $"{result.tracesHandled} / {result.tracesTotal}";
        _questText.text = $"{result.questsCompleted} / {result.questsIssued}";

        GhostResult[] ghosts = result.ghosts ?? Array.Empty<GhostResult>();
        for (int i = 0; i < _ghostResultSlots.Length; i++)
        {
            GhostResultSlot slot = _ghostResultSlots[i];
            bool used = i < ghosts.Length;
            slot.root.SetActive(used);
            if (!used) continue;

            slot.nameText.text = ghosts[i].hostName;
            slot.stampText.text = ghosts[i].exorcised ? _exorcisedLabel : _remainingLabel;
            slot.stamp.color = ghosts[i].exorcised ? _exorcisedColor : _remainingColor;
        }

        PlayerResult[] players = result.players ?? Array.Empty<PlayerResult>();
        for (int i = 0; i < _playerResultSlots.Length; i++)
        {
            PlayerResultSlot slot = _playerResultSlots[i];
            bool used = i < players.Length;
            slot.root.SetActive(used);
            if (!used) continue;

            int id = players[i].characterId;
            slot.portrait.sprite = id >= 0 && id < _characterPortraits.Length ? _characterPortraits[id] : null;
            slot.nameText.text = players[i].name;
            slot.stateText.text = players[i].dead ? "사망" : "생존";
        }
    }

    private string FormatPlayerTime()
    {
        // The network scene object can be inactive when this UI starts.
        if (_netGM == null) _netGM = FindFirstObjectByType<NetGM>();
        double start = NetGameStartGate.Instance != null ? NetGameStartGate.Instance.StartTime : 0;
        double end = _netGM != null ? _netGM.GameOverTime : 0;
        if (start <= 0 || end <= 0 || end < start) return "-";

        int total = (int)(end - start);
        return $"{total / 60}분 {total % 60:00}초";
    }

    public void OnClickLobby()
    {
        if (_netGM == null) _netGM = FindFirstObjectByType<NetGM>();
        if (NetworkServer.active && _netGM != null) _netGM.ServerReturnToLobby();
    }
    public void OnClickMainMenu()
    {
        if (NetworkManager.singleton is RoomManager roomManager) roomManager.LeaveSession();
    }
}
