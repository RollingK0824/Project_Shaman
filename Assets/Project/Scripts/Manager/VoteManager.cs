using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using System;
using Mirror;
using System.Collections.Generic;

public class VoteManager : SceneSingleton<VoteManager>
{
    private VillagerRegistry _reg => AIManager.Instance != null ? AIManager.Instance.Registry : null;
    private VillagerRegistry _subscribedReg;

    private Dictionary<uint, string> _vote = new Dictionary<uint, string>();
    private Dictionary<string, int> _count = new Dictionary<string, int>();
    private Dictionary<string, int> _votedOut = new Dictionary<string, int>();

    
    public string MyVote { get; private set; }
    public bool IsVotingOpen => TimeManager.Instance.CurrentTimePhase == TimeOfDay.Day;

    public IReadOnlyDictionary<uint, string> Votes => _vote;

    public event Action VotesChanged;
    public event Action<string> NpcVotedOut;
    public event Action<string> VoteRequested;


    public int GetVoteCount(string npcId)
    {
        return _count.TryGetValue(npcId, out var value) ? value : 0;
    }

    public bool IsVotable(string npcId)
    {
        return _reg != null && _reg.TryGet(npcId, out var v) && v.IsAlive;
    }

    public bool CastVote(uint voterId, string npcId)
    {
        if (NetworkServer.active && (!NetworkServer.spawned.TryGetValue(voterId, out var voter)
            || !PlayerActionGuard.CanAct(voter.gameObject))) return false;
        if (voterId == 0 || !IsVotingOpen || !IsVotable(npcId))
        {
            return false;
        }

        if (_vote.TryGetValue(voterId, out string oldNpc))
        {
            if (oldNpc == npcId)
            {
                return true;
            }
            ChangeCount(oldNpc, -1);
        }

        _vote[voterId] = npcId;
        ChangeCount(npcId, 1);

        VotesChanged?.Invoke();
        return true;
    }

    private void ChangeCount(string npcId, int delta)
    {
        _count.TryGetValue(npcId, out int count);
        count += delta;

        if (count <= 0)
        {
            _count.Remove(npcId);
        }
        else
        {
            _count[npcId] = count;
        }
    }

    public void RemoveVote(uint voterId)
    {
        if (_vote.TryGetValue(voterId, out string npcId))
        {
            _vote.Remove(voterId);
            ChangeCount(npcId, -1);
            VotesChanged?.Invoke();
        }
    }

    private void HandleVillagerDeath(IVillagerView view)
    {
        RemoveVotesFor(view.VillagerId);
    }

    public void RemoveVotesFor (string villagerId)
    {
        if (MyVote == villagerId)
        {
            MyVote = null;
            VotesChanged?.Invoke();
        }

        if (!(_count.ContainsKey(villagerId)))
        {
            return;
        }

        List<uint> voterIds = new List<uint>();

        foreach (KeyValuePair<uint, string> votes in _vote)
        {
            if (votes.Value == villagerId)
            {
                voterIds.Add(votes.Key);
            }
        }

        foreach(uint ids in voterIds)
        {
            RemoveVote(ids);
        }
    }

    private void Start()
    {
        _subscribedReg = _reg;
        if (_subscribedReg != null)
        {
            _subscribedReg.OnDied += HandleVillagerDeath;
        }
    }

    protected override void OnDestroy()
    {
        if (_subscribedReg != null)
        {
            _subscribedReg.OnDied -= HandleVillagerDeath;
            _subscribedReg = null;
        }

        base.OnDestroy();
    }

    public void RequestVote(string npcId)
    {
        if (NetworkClient.active && (NetworkClient.localPlayer == null ||
            !PlayerActionGuard.CanAct(NetworkClient.localPlayer.gameObject))) return;
        if (!NetworkClient.active)
        {
            foreach (var player in PlayerHealth.ActivePlayers)
                if (player.TryGetComponent<PlayerInputReader>(out var input) && input.isActiveAndEnabled && player.IsDead) return;
        }
        if (!IsVotingOpen)
        {
            return;
        }

        MyVote = (npcId == MyVote) ? null : npcId;

        VotesChanged?.Invoke();
        VoteRequested?.Invoke(MyVote);
    }

    // [서버] 밤이 시작될 때 어댑터가 호출. 최다 득표 NPC를 반환
    // 동점이면 동점자 중 무작위, 표가 하나도 없으면 살아있는 NPC 전체 중 무작위. 살아있는 NPC가 없으면 null
    // 무작위는 서버에서 한 번만 정하고 결과만 클라이언트로 보내므로 모든 화면에서 같은 결과가 나온다
    public string ResolveVote()
    {
        int highest = 0;
        List<string> candidates = new List<string>();

        foreach (KeyValuePair<string, int> voteCount in _count)
        {
            if (!IsVotable(voteCount.Key))
            {
                continue;
            }

            if (voteCount.Value > highest)
            {
                highest = voteCount.Value;
                candidates.Clear();
                candidates.Add(voteCount.Key);
            }
            else if (voteCount.Value == highest)
            {
                candidates.Add(voteCount.Key);
            }
        }

        // 표가 없으면 살아있는 NPC 전체가 후보
        if (candidates.Count == 0 && _reg != null)
        {
            foreach (IVillagerView villager in _reg.Villagers)
            {
                if (villager.IsAlive)
                {
                    candidates.Add(villager.VillagerId);
                }
            }
        }

        string winner = candidates.Count > 0
            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : null;

        FinishVote(winner);
        return winner;
    }

    // [클라이언트] 서버에서 받은 투표 결과 적용. 호스트에서는 호출하지 않음
    public void ApplyVoteResult(string npcId)
    {
        FinishVote(npcId);
    }

    // [클라이언트] 서버에서 받은 투표 표로 교체하고 득표 수를 다시 계산. 호스트에서는 호출하지 않음
    public void ApplyNetworkVotes(IReadOnlyDictionary<uint, string> votes)
    {
        _vote.Clear();
        _count.Clear();

        if (votes != null)
        {
            foreach (KeyValuePair<uint, string> vote in votes)
            {
                _vote[vote.Key] = vote.Value;
                ChangeCount(vote.Value, 1);
            }
        }

        VotesChanged?.Invoke();
    }

    // 해당 NPC에 투표한 플레이어들
    public IReadOnlyList<uint> GetVotersFor(string npcId)
    {
        List<uint> voters = new List<uint>();

        foreach (KeyValuePair<uint, string> vote in _vote)
        {
            if (vote.Value == npcId)
            {
                voters.Add(vote.Key);
            }
        }

        return voters;
    }

    // 하루 투표 마무리. 추방 기록을 NpcVotedOut보다 먼저 남겨야 OnDied 시점에 WasVotedOut이 맞게 나온다
    private void FinishVote(string winner)
    {
        _vote.Clear();
        _count.Clear();
        MyVote = null;

        RecordVotedOut(winner);
        VotesChanged?.Invoke();
        NpcVotedOut?.Invoke(winner);
    }

    public bool WasVotedOut(string npcId)
    {
        return npcId != null && _votedOut.ContainsKey(npcId);
    }

    public bool TryGetVotedOutDay(string npcId, out int day)
    {
        day = 0;
        return npcId != null && _votedOut.TryGetValue(npcId, out day);
    }

    private void RecordVotedOut(string npcId)
    {
        if (npcId == null)
        {
            return;
        }

        _votedOut[npcId] = TimeManager.Instance.DayCount;
    }
}
