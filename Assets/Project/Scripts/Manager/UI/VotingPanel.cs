using ProjectShaman.AI.Core;
using System.Collections.Generic;
using UnityEngine;

public class VotingPanel : UIPanel
{
    [SerializeField]
    private VoteCellUI _cellPrefab;

    [SerializeField]
    private Transform _gridRoot;

    [SerializeField]
    private Color[] _playerColors = { Color.red, Color.yellow, Color.green, Color.blue };

    [SerializeField]
    private Color _unknownVoterColor = Color.gray;

    private readonly List<uint> _knownPlayers = new List<uint>();

    private readonly Dictionary<string, VoteCellUI> _cells = new Dictionary<string, VoteCellUI>();

    private readonly List<Color> _markColors = new List<Color>();

    private VillagerRegistry _subscribedReg;
}
