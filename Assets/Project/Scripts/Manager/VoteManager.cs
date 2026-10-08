using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using System;
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
        if (!IsVotingOpen)
        {
            return;
        }

        MyVote = (npcId == MyVote) ? null : npcId;

        VotesChanged?.Invoke();
        VoteRequested?.Invoke(MyVote);
    }

    public string ResolveVote()
    {
        int highest = 0;
        string winner = null;
        foreach (KeyValuePair<string, int> voteCount in _count)
        {
            if (voteCount.Value > highest)
            {
                winner = voteCount.Key;
            }
        }

        _vote.Clear();
        _count.Clear();
        MyVote = null;

        RecordVotedOut(winner);
        NpcVotedOut?.Invoke(winner);
        return winner;
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
