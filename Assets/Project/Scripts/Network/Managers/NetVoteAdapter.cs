using UnityEngine;
using Mirror;
using System.Collections.Generic;

[RequireComponent(typeof (NetworkIdentity))]
public class NetVoteAdapter : NetworkBehaviour
{
    // 투표는 공개. key = 투표자 netId, value = NPC VillagerId
    private readonly SyncDictionary<uint, string> _networkVotes = new SyncDictionary<uint, string>();

    // 퇴장한 플레이어는 PlayerRoster.Players에서 사라지므로 이전 목록을 기억합니다.
    private readonly HashSet<uint> _knownPlayerIds = new HashSet<uint>();

    private VoteManager _voteManager;
    private TimeManager _timeManager;

    private bool _clientSubscribed;
    private bool _serverSubscribed;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _voteManager = VoteManager.Instance;
        if (_voteManager == null) return;

        _timeManager = TimeManager.Instance;

        _voteManager.VotesChanged += HandleVotesChanged;
        _timeManager.OnNightStart += HandleNightStart;

        PlayerRoster.Changed += HandleRosterChanged;
        _serverSubscribed = true;

        CopyServerVotes();
        RememberCurrentPlayers();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _voteManager = VoteManager.Instance;
        if (_voteManager == null) return;

        _voteManager.VoteRequested += HandleVoteRequested;
        _clientSubscribed = true;

        _networkVotes.OnChange += HandleNetworkVotsChanged;

        // 구독 시점에 이미 동기화된 표가 있을 수 있음
        if (!isServer)
            ApplyNetworkVotes();
    }

    public override void OnStopServer()
    {
        if(_serverSubscribed)
        {
            if (_voteManager != null)
                _voteManager.VotesChanged -= HandleVotesChanged;

            if (_timeManager != null)
                _timeManager.OnNightStart -= HandleNightStart;

            PlayerRoster.Changed -= HandleRosterChanged;
        }

        _knownPlayerIds.Clear();
        _serverSubscribed = false;

        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        if (_clientSubscribed && _voteManager != null)
        {
            _voteManager.VoteRequested -= HandleVoteRequested;
        }

        _networkVotes.OnChange -= HandleNetworkVotsChanged;
        _clientSubscribed = false;
        base.OnStopClient();
    }

    // 클라 -> 서버 : 투표 요청

    private void HandleVoteRequested(string npcId)
    {
        CmdRequestVote(npcId);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestVote(string npcId, NetworkConnectionToClient sender = null)
    {
        if (sender?.identity == null || _voteManager == null)
            return;

        NetPlayerStatus status = sender.identity.GetComponent<NetPlayerStatus>();

        if (status == null || status.IsDead)
            return;

        uint voterNetId = sender.identity.netId;

        if(npcId == null)
        {
            if (_voteManager.IsVotingOpen)
                _voteManager.RemoveVote(voterNetId);

            return;
        }

        // NPC 유효성과 투표 시간은 VoteManager가 검사
        _voteManager.CastVote(voterNetId, npcId);
    }

    // 서버 -> 클라 : 투표 현황

    private void HandleVotesChanged()
    {
        if (isServer)
            CopyServerVotes();
    }

    // 바뀐 항목만 반영
    // Clear 후 전부 다시 넣으면 클라에서 "표 0개" 상태가 잠깐 생기고 콜백도 N + 1번 발생
    [Server]
    private void CopyServerVotes()
    {
        if (_voteManager == null)
            return;

        _networkVotes.Clear();

        foreach (KeyValuePair<uint, string> vote in _voteManager.Votes)
            _networkVotes[vote.Key] = vote.Value;
    }


    private void HandleNetworkVotsChanged(SyncDictionary<uint, string>.Operation operation, uint key, string value)
    {
        ApplyNetworkVotes();
    }

    private void ApplyNetworkVotes()
    {
        if (isServer || _voteManager == null)
            return;

        var votes = new Dictionary<uint, string>();

        foreach (KeyValuePair<uint, string> vote in _networkVotes)
            votes[vote.Key] = vote.Value;

        _voteManager.ApplyNetworkVotes(votes);
    }

    private void HandleNightStart()
    {
        if (!isServer || _voteManager == null)
            return;

        string votedOutId = _voteManager.ResolveVote();
        RpcApplyVoteResult(votedOutId);
    }

    [ClientRpc]
    private void RpcApplyVoteResult(string npcId)
    {
        if (isServer || _voteManager == null)
            return;

        _voteManager.ApplyVoteResult(npcId);
    }

    private void HandleRosterChanged()
    {
        if (!isServer || _voteManager == null)
            return;

        var currentPlayerIds = new HashSet<uint>();

        foreach (NetPlayerStatus player in PlayerRoster.Players)
        {
            if (player == null || player.netIdentity == null)
                continue;

            uint netId = player.netIdentity.netId;
            currentPlayerIds.Add(netId);
            _knownPlayerIds.Add(netId);

            if (player.IsDead)
                _voteManager.RemoveVote(netId);
        }

        // 이전 목록에는 있지만 현재 목록에는 없는 ID는 퇴장한 플레이어입니다.
        var departedPlayerIds = new List<uint>();

        foreach (uint previousId in _knownPlayerIds)
        {
            if (!currentPlayerIds.Contains(previousId))
                departedPlayerIds.Add(previousId);
        }

        foreach (uint departedId in departedPlayerIds)
        {
            _voteManager.RemoveVote(departedId);
            _knownPlayerIds.Remove(departedId);
        }
    }

    private void RememberCurrentPlayers()
    {
        foreach (NetPlayerStatus player in PlayerRoster.Players)
        {
            if (player != null && player.netIdentity != null)
                _knownPlayerIds.Add(player.netIdentity.netId);
        }
    }
}
