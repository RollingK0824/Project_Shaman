using System.Collections.Generic;
using UnityEngine;

public class VotingPanel : UIPanel
{
    [SerializeField]
    private VoteCellUI _cellPrefab;
    [SerializeField]
    private Transform _gridRoot;

    private Dictionary<string, VoteCellUI> _cells;
}
