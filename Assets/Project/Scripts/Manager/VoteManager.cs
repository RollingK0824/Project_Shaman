using System;
using System.Collections.Generic;
using UnityEngine;

public class VoteManager : SceneSingleton<VoteManager>
{
    private Dictionary<int, int> _votes;
    public bool IsVotingOpen => TimeManager.Instance.CurrentTimePhase == TimeOfDay.Day;

    public event Action<int> VoteRequested;
    public event Action VotesChanged;
    public event Action<int> NpcVotedOut;

    public bool CastVote(uint voterId, int npcId)
    {
        return false;
    }

    public void RemoveVote(uint voterId)
    {

    }

    public uint ResolveVote()
    {
        return 0;
    }

    public void RequestVote(uint npcId)
    {

    }

    public void ApplyNetworkVotes(IReadOnlyDictionary<int, int> votes)
    {
        
    }

    public int GetVoteCount(int npcId)
    {
        return _votes[npcId];
    }

    //public bool TryGetVote(int voterId, out int npcId)
    //{
        
    //}
}
