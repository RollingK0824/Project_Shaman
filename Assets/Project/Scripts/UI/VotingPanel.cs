using Mirror;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public class VotingPanel : UIPanel
{
    [SerializeField]
    private VoteCellUI _cellPrefab;

    [SerializeField]
    private Transform _gridRoot;

    // 플레이어 색. netId가 낮은 순서대로 빨강, 노랑, 초록, 파랑 (최대 4명)
    [SerializeField]
    private Color[] _playerColors = { Color.red, Color.yellow, Color.green, Color.blue };

    // 목록에 없는 투표자 (테스트용 가짜 투표자 등)
    [SerializeField]
    private Color _unknownVoterColor = Color.gray;

    // 이번 게임에서 본 플레이어 netId. 정렬해서 색 순서로 쓰고, 나간 플레이어도 지우지 않음 (색이 밀리지 않도록)
    private readonly List<uint> _knownPlayers = new List<uint>();

    private readonly Dictionary<string, VoteCellUI> _cells = new Dictionary<string, VoteCellUI>();
    private readonly HashSet<string> _deadIds = new HashSet<string>();

    // RefreshCells에서 셀마다 다시 채워 쓰는 색 목록 (매번 새로 만들지 않도록)
    private readonly List<Color> _markColors = new List<Color>();

    // 구독한 레지스트리. OnDisable에서 같은 것에서 구독 해제
    private VillagerRegistry _subscribedReg;

    private void OnEnable()
    {
        VoteManager voteManager = VoteManager.Instance;
        if (voteManager != null)
        {
            voteManager.VotesChanged += RefreshCells;
            voteManager.NpcVotedOut += HandleNpcVotedOut;
        }

        // 클라 시계는 서버보다 조금 늦어서, 결과가 낮일 때 도착할 수 있다. 낮이 시작되면 다시 그리고, 밤이 되면 창을 닫는다
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager != null)
        {
            timeManager.OnDayStart += RefreshCells;
            timeManager.OnNightStart += HandleNightStart;
        }

        SubscribeRegistry();
        BuildCells();
    }

    private void Update()
    {
        // 씬 시작 시 이미 켜져 있던 패널(월드 스페이스)은 OnEnable 때 레지스트리가 아직 없을 수 있다
        // AIManager/AIFactory는 씬 네트워크 오브젝트라 Mirror가 스폰할 때까지 꺼져 있어서 레지스트리도 그때 생긴다
        // 호스트는 씬 로드 직후 스폰되지만, 클라이언트는 서버 스폰 메시지를 받을 때까지 늦어지므로 생길 때까지 매 프레임 다시 시도한다
        if (_subscribedReg == null)
        {
            SubscribeRegistry();
            if (_subscribedReg != null)
            {
                BuildCells();
            }
        }
    }

    private void SubscribeRegistry()
    {
        _subscribedReg = AIManager.Instance != null ? AIManager.Instance.Registry : null;
        if (_subscribedReg != null)
        {
            _subscribedReg.OnRegistered += HandleVillagerChanged;
            _subscribedReg.OnDeathKnown += HandleVillagerChanged;
            _subscribedReg.OnExorcised += HandleVillagerChanged;
            _subscribedReg.OnCleared += BuildCells;
        }
    }

    private void OnDisable()
    {
        // 씬 종료 중이면 SceneSingleton이 null을 돌려준다
        VoteManager voteManager = VoteManager.Existing;
        if (voteManager != null)
        {
            voteManager.VotesChanged -= RefreshCells;
            voteManager.NpcVotedOut -= HandleNpcVotedOut;
        }

        TimeManager timeManager = TimeManager.Existing;
        if (timeManager != null)
        {
            timeManager.OnDayStart -= RefreshCells;
            timeManager.OnNightStart -= HandleNightStart;
        }

        if (_subscribedReg != null)
        {
            _subscribedReg.OnRegistered -= HandleVillagerChanged;
            _subscribedReg.OnDeathKnown -= HandleVillagerChanged;
            _subscribedReg.OnExorcised -= HandleVillagerChanged;
            _subscribedReg.OnCleared -= BuildCells;
            _subscribedReg = null;
        }
    }

    // NPC 목록이 바뀔 수 있을 때만 호출 (패널 열기, NPC 등록/사망, 추방). 셀을 새로 만든다
    private void BuildCells()
    {
        foreach (VoteCellUI oldCell in _cells.Values)
        {
            // Destroy는 프레임 끝에 처리되므로 먼저 꺼서 그리드 배치에서 바로 빠지게 한다
            oldCell.gameObject.SetActive(false);
            Destroy(oldCell.gameObject);
        }
        _cells.Clear();
        _deadIds.Clear();

        if (_subscribedReg != null)
        {
            foreach (IVillagerView villager in _subscribedReg.Villagers)
            {
                // 사망이 알려진 주민은 목록에서 빼지 않고 X로 표시 (투표 불가). 퇴마된 주민은 이름 옆에 표시
                // (둘 다 AI 쪽 레지스트리 이벤트로 다시 그림)
                bool isDead = villager.IsDeathKnown;
                string label = isDead ? $"<color=#B03030>X</color> <s>{villager.DisplayName}</s>"
                    : villager.IsExorcised ? $"{villager.DisplayName} <size=70%>퇴마</size>"
                    : villager.DisplayName;

                VoteCellUI cell = Instantiate(_cellPrefab, _gridRoot);
                cell.Setup(villager.VillagerId, label, OnCellClicked);
                _cells.Add(villager.VillagerId, cell);
                if (isDead)
                {
                    _deadIds.Add(villager.VillagerId);
                }
            }
        }

        RefreshCells();
    }

    // 투표가 바뀔 때마다 호출. 셀은 그대로 두고 X와 득표 수만 갱신
    private void RefreshCells()
    {
        VoteManager voteManager = VoteManager.Instance;
        if (voteManager == null)
        {
            return;
        }

        UpdateKnownPlayers();

        uint localId = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;
        bool isVotingOpen = voteManager.IsVotingOpen;

        foreach (KeyValuePair<string, VoteCellUI> pair in _cells)
        {
            string npcId = pair.Key;
            _markColors.Clear();

            // 다른 플레이어의 표. 내 표는 서버 응답을 기다리지 않도록 MyVote로 따로 그린다 (두 번 그리지 않게 여기선 제외)
            foreach (KeyValuePair<uint, string> vote in voteManager.Votes)
            {
                if (vote.Value == npcId && vote.Key != localId)
                {
                    _markColors.Add(GetVoterColor(vote.Key));
                }
            }

            if (npcId == voteManager.MyVote)
            {
                _markColors.Add(GetVoterColor(localId));
            }

            pair.Value.Refresh(_markColors.Count, _markColors);
            pair.Value.SetInteractable(isVotingOpen && !_deadIds.Contains(npcId));
        }
    }

    // 새로 보인 플레이어만 추가하고 정렬. 한 번 들어간 플레이어는 지우지 않는다
    private void UpdateKnownPlayers()
    {
        bool added = false;

        foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
        {
            if (identity == null || _knownPlayers.Contains(identity.netId))
            {
                continue;
            }

            if (identity.TryGetComponent(out NetPlayerStatus _))
            {
                _knownPlayers.Add(identity.netId);
                added = true;
            }
        }

        if (added)
        {
            _knownPlayers.Sort();
        }
    }

    private Color GetVoterColor(uint voterId)
    {
        //int slot = _knownPlayers.IndexOf(voterId);
        //if (slot < 0 || _playerColors == null || _playerColors.Length == 0)
        //{
        //    return _unknownVoterColor;
        //}
        //
        //return _playerColors[slot % _playerColors.Length];

        if (NetworkClient.spawned.TryGetValue(voterId, out NetworkIdentity identity)
            && identity != null
            && identity.TryGetComponent(out NetPlayerStatus status)
            && status.ColorId >= 0)
        {
            return PlayerColors.Get(status.ColorId);
        }

        return _unknownVoterColor;
    }

    private void OnCellClicked(string npcId)
    {
        VoteManager.Instance.RequestVote(npcId);
    }

    private void HandleVillagerChanged(IVillagerView villager)
    {
        BuildCells();
    }

    private void HandleNpcVotedOut(string npcId)
    {
        BuildCells();
    }

    // 밤이 되면 투표 창을 닫는다. 각자 자기 TimeManager 기준으로 닫히므로 네트워크 없이 모든 플레이어에게 적용된다
    // 닫히면서 OnDisable이 구독을 해제하므로, 열려 있는 패널에서만 호출된다
    private void HandleNightStart()
    {
        Close();
    }
}
