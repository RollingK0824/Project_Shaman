using UnityEngine;
using System.Collections.Generic;
using Mirror;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Trace;
using Steamworks;

public struct GhostResult
{
    public string hostName;
    public bool exorcised;
}

public struct PlayerResult
{
    public string name;
    public int characterId;
    public bool dead;
}

public struct GameResult
{
    public bool valid;
    public int villagersLost;
    public int tracesHandled;
    public int tracesTotal;
    public int questsCompleted;
    public int questsIssued;    // 주민이 낸 의뢰 (받지 않은 것 포함, 패널티 없이 거둔 것 제외)

    public GhostResult[] ghosts;
    public PlayerResult[] players;
}

// [서버] 게임 중 기록 (원귀, 흔적, 의뢰)를 모으다가 승패가 나는 순간 한번에 확정해서 공개
// [클라] 결과 화면은 Result만 읽음

[RequireComponent(typeof(NetworkIdentity))]
public class NetGameRecord : NetworkBehaviour
{
    public static NetGameRecord Instance { get; private set; }

    [SyncVar] private GameResult _result;
    public GameResult Result => _result;

    // [서버] 퇴마되면 AI_Possession이 사라지므로 원귀 목록은 시작할 때 기록
    private readonly List<string> _ghostIds = new List<string>();
    private readonly Dictionary<string, string> _ghostNames = new Dictionary<string, string>();
    private readonly HashSet<string> _exorcised = new HashSet<string>();

    private int _tracesTotal;
    private int _tracesHandled;
    private int _questsIssued;
    private int _questsCompleted;

    private AIFactory _aiFactory;
    private QuestManager _quests;
    private GameManager _gm;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public override void OnStartServer()
    {
        base.OnStartServer();

        GhostTraceRegistry.OnTraceAdded += HandleTraceAdded;
        GhostTraceRegistry.OnTraceResolved += HandleTraceResolved;
        AIEvents.OnVillagerExorcised += HandleExorcised;

        _quests = QuestManager.Existing;
        if (_quests != null)
        {
            _quests.QuestOffered += HandleQuestOffered;
            _quests.QuestEnded += HandleQuestEnded;
        }

        _gm = GameManager.Instance;
        _gm.OnWin += ServerFinalize;
        _gm.OnLose += ServerFinalize;

        _aiFactory = FindFirstObjectByType<AIFactory>(FindObjectsInactive.Include);
        if (_aiFactory == null) return;

        if (_aiFactory.SpawnedVillagers.Count > 0) RecordGhosts(_aiFactory.SpawnedVillagers);
        else _aiFactory.OnVillagersCreated += HandleVillagersCreated;
    }

    public override void OnStopServer()
    {
        GhostTraceRegistry.OnTraceAdded -= HandleTraceAdded;
        GhostTraceRegistry.OnTraceResolved -= HandleTraceResolved;
        AIEvents.OnVillagerExorcised -= HandleExorcised;

        if (_quests != null)
        {
            _quests.QuestOffered -= HandleQuestOffered;
            _quests.QuestEnded -= HandleQuestEnded;
        }

        if (_gm != null)
        {
            _gm.OnWin -= ServerFinalize;
            _gm.OnLose -= ServerFinalize;
        }

        if (_aiFactory != null) _aiFactory.OnVillagersCreated -= HandleVillagersCreated;

        base.OnStopServer();
    }

    private void HandleVillagersCreated(IReadOnlyList<AI_Core> villagers)
    {
        _aiFactory.OnVillagersCreated -= HandleVillagersCreated;
        RecordGhosts(villagers);
    }

    [Server]
    private void RecordGhosts(IReadOnlyList<AI_Core> villagers)
    {
        foreach(AI_Core villager in villagers)
        {
            if (villager == null || villager.PublicInfo == null || villager.GetComponent<AI_Possession>() == null) continue;

            string id = villager.PublicInfo.VillagerId;
            if (_ghostNames.ContainsKey(id)) continue;

            _ghostIds.Add(id);
            _ghostNames[id] = villager.PublicInfo.DisplayName;
        }
    }

    private void HandleExorcised(string villagerId)
    {
        if (_ghostNames.ContainsKey(villagerId)) _exorcised.Add(villagerId);
    }

    private void HandleTraceAdded(GhostTrace trace) => _tracesTotal++;
    private void HandleTraceResolved(GhostTrace trace, TraceResolveReason reason)
    {
        // 플레이어가 치움 / 아이템으로 정화 / 의뢰로 해결만 셈 (자동 정리, 게임 종료 정리는 제외)
        if (reason == TraceResolveReason.ClearedByPlayer || reason == TraceResolveReason.Item || reason == TraceResolveReason.Request)
        {
            _tracesHandled++;
        }
    }

    // 받지 않아도 밤이 되면 실패하므로 낸 의뢰를 모두 셈. 더 급한 의뢰에 밀리거나 의뢰인 사망 등으로 패널티 없이 거둔 것은 뺌
    private void HandleQuestOffered(IQuestView quest) => _questsIssued++;
    private void HandleQuestEnded(IQuestView quest)
    {
        if (quest.State == QuestState.Completed) _questsCompleted++;
        else if (quest.State == QuestState.Cancelled) _questsIssued--;
    }

    // [서버] 승패가 난 순간 한번에 확정
    [Server]
    private void ServerFinalize()
    {
        if (_result.valid) return;

        // 승리 = 전원 퇴마. 마지막 원귀의 퇴마 이벤트보다 승리 판정이 먼저 올 수 있어서 승리면 전부 퇴마로 처리
        bool won = _gm.CurrentGameState == GameState.Win;

        var ghosts = new GhostResult[_ghostIds.Count];
        for (int i = 0; i < _ghostIds.Count; i++)
        {
            string id = _ghostIds[i];
            ghosts[i] = new GhostResult { hostName = _ghostNames[id], exorcised = won || _exorcised.Contains(id) };
        }

        var players = new PlayerResult[PlayerRoster.Players.Count];
        for (int i = 0; i < players.Length; i++)
        {
            NetPlayerStatus p = PlayerRoster.Players[i];

            players[i] = new PlayerResult { name = p.PlayerName, characterId = p.CharacterId, dead = p.IsDead };
        }

        _result = new GameResult
        {
            valid = true,
            villagersLost = Mathf.Max(0, _gm.NpcTotal - _gm.NpcCount),
            tracesHandled = _tracesHandled,
            tracesTotal = _tracesTotal,
            questsCompleted = _questsCompleted,
            questsIssued = Mathf.Max(0, _questsIssued),
            ghosts = ghosts,
            players = players
        };

        Debug.Log($"[NetGameRecord] 결과 확정: 원귀 {ghosts.Length}, 흔적 {_tracesHandled}/{_tracesTotal}, 의뢰 {_questsCompleted}/{_questsIssued}");
    }
}
